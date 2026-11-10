using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Une arme tenue par le joueur. Elle vise toute seule l'ennemi le plus proche (ou la direction de visée
    /// en mode manuel) et attaque dès que son temps de recharge est écoulé :
    /// tir de projectiles, coup d'estoc ou balayage selon <see cref="WeaponData.kind"/>.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        const float MeleeHitRadius = 0.4f;
        const float SweepHalfAngle = 75f;

        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float recoilDistance = 0.15f;

        public WeaponData Data { get; private set; }

        Player owner;
        float cooldownTimer;
        float recoil;
        Vector2 restPosition;
        Vector2 aimDirection = Vector2.right;

        // Rafales
        int burstRemaining;
        float burstTimer;

        // Corps à corps
        bool attacking;
        float attackTime;
        Vector2 attackDirection;
        float bladeLength;
        readonly List<Enemy> meleeHits = new();
        static readonly List<Enemy> buffer = new();

        public void Init(Player player, WeaponData data)
        {
            owner = player;
            Data = data;
            spriteRenderer.sprite = data.sprite;
            transform.localScale = Vector3.one * data.spriteScale;
            cooldownTimer = Random.Range(0f, data.cooldown);
            attacking = false;
            burstRemaining = 0;
            // Longueur approximative de l'arme, du manche (pivot) jusqu'à la pointe
            bladeLength = data.sprite != null ? Mathf.Max(data.sprite.bounds.size.x, data.sprite.bounds.size.y) * 0.9f * data.spriteScale : 0.5f;
        }

        public void SetRestPosition(Vector2 localPosition)
        {
            restPosition = localPosition;
            transform.localPosition = localPosition;
        }

        float EffectiveRange => Data.IsMelee
            ? Data.range + owner.Stats[StatType.Range] * 0.5f // la portée compte moitié pour la mêlée
            : Data.range + owner.Stats[StatType.Range];

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateSorting();
            if (attacking)
            {
                UpdateMelee(dt);
                return;
            }

            float range = EffectiveRange;
            Vector2 origin = (Vector2)transform.parent.position + restPosition;

            // Suite d'une rafale en cours
            if (burstRemaining > 0)
            {
                burstTimer -= dt;
                if (burstTimer <= 0f && GameManager.Instance.State == GameState.Wave)
                {
                    Fire(range);
                    burstRemaining--;
                    burstTimer = Data.burstInterval;
                }
            }

            bool shouldAttack = false;
            if (owner.ManualAim)
            {
                aimDirection = owner.AimDirection;
                shouldAttack = true;
            }
            else
            {
                float searchRange = Data.IsMelee ? range + bladeLength : range;
                var target = EnemyGrid.FindNearest(origin, searchRange);
                if (target != null)
                {
                    aimDirection = (target.Position - origin).normalized;
                    shouldAttack = true;
                }
            }

            PointTowards(aimDirection);

            cooldownTimer -= dt * owner.Stats.AttackSpeedMultiplier;
            if (shouldAttack && cooldownTimer <= 0f && GameManager.Instance.State == GameState.Wave)
            {
                if (Data.IsMelee) StartMelee();
                else if (Data.kind == WeaponKind.Turret) DeployTurret();
                else
                {
                    Fire(range);
                    burstRemaining = Mathf.Max(0, Data.burstCount - 1);
                    burstTimer = Data.burstInterval;
                }
                cooldownTimer = Data.cooldown;
            }

            // Recul visuel
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 2f);
            transform.localPosition = restPosition - aimDirection * recoil;
        }

        void PointTowards(Vector2 dir)
        {
            if (Data.kind == WeaponKind.Turret)
            {
                // La tourelle (dans la main) reste droite et se retourne simplement
                transform.rotation = Quaternion.identity;
                spriteRenderer.flipX = dir.x < 0f;
                spriteRenderer.flipY = false;
                return;
            }
            spriteRenderer.flipX = false;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + Data.spriteAngleOffset);
            // Les sprites horizontaux (fusils) sont retournés pour ne pas être à l'envers vers la gauche
            spriteRenderer.flipY = Mathf.Approximately(Data.spriteAngleOffset, 0f) && dir.x < 0f;
        }

        void UpdateSorting()
        {
            // Les armes au-dessus du perso passent derrière lui
            int baseOrder = SortingUtil.OrderFromY(owner.Position.y);
            spriteRenderer.sortingOrder = restPosition.y > 0.1f && !attacking ? baseOrder - 1 : baseOrder + 1;
        }

        // ------------------------------------------------------------ Distance

        void Fire(float range)
        {
            recoil = recoilDistance;
            Vector2 muzzle = (Vector2)transform.position + aimDirection * (0.45f * Data.spriteScale);
            AudioManager.Play(SoundId.Shoot);
            if (Data.projectileSpeed >= 12f) // pas de flash pour l'arrosoir, le bâton...
                SpriteEffects.PlayRotated(GameManager.Instance.muzzleFlashFrames, muzzle,
                    Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f, 0.35f, 30f);

            int count = Mathf.Max(1, Data.projectilesPerShot);
            float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0f : Mathf.Lerp(-Data.spreadAngle / 2f, Data.spreadAngle / 2f, i / (count - 1f));
                offset += Random.Range(-Data.inaccuracy, Data.inaccuracy);
                float a = (baseAngle + offset) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                float damage = RollDamage(Data, owner, out bool crit);
                var projectile = GameManager.Instance.ProjectilePool.Get(muzzle);
                projectile.Launch(dir, Data, damage, crit, range, owner);
            }
        }

        // ------------------------------------------------------------ Corps à corps

        void StartMelee()
        {
            AudioManager.Play(SoundId.MeleeSwing);
            attacking = true;
            attackTime = 0f;
            attackDirection = aimDirection;
            meleeHits.Clear();
        }

        void UpdateMelee(float dt)
        {
            // Les coups sont aussi accélérés par la vitesse d'attaque (sans devenir illisibles)
            attackTime += dt * Mathf.Min(2f, owner.Stats.AttackSpeedMultiplier);
            float t = Mathf.Clamp01(attackTime / Data.attackDuration);
            float range = EffectiveRange;

            Vector2 dir;
            Vector2 offset;
            bool canHit;
            if (Data.kind == WeaponKind.Sweep)
            {
                // Arc de cercle de -75° à +75° autour de la direction visée
                float sweep = Mathf.Lerp(-SweepHalfAngle, SweepHalfAngle, t) * Mathf.Deg2Rad;
                float baseAngle = Mathf.Atan2(attackDirection.y, attackDirection.x);
                dir = new Vector2(Mathf.Cos(baseAngle + sweep), Mathf.Sin(baseAngle + sweep));
                float extend = Mathf.Sin(t * Mathf.PI); // sort puis revient
                offset = dir * (range * 0.6f * extend);
                canHit = true;
            }
            else
            {
                // Estoc : aller rapide, retour plus lent ; ne touche qu'à l'aller
                dir = attackDirection;
                float extend = t < 0.4f ? 1f - (1f - t / 0.4f) * (1f - t / 0.4f) : 1f - (t - 0.4f) / 0.6f;
                offset = dir * (range * extend);
                canHit = t < 0.5f;
            }

            transform.localPosition = restPosition + offset;
            PointTowards(dir);
            if (canHit) MeleeHitCheck(dir);

            if (t >= 1f)
            {
                attacking = false;
                transform.localPosition = restPosition;
            }
        }

        void MeleeHitCheck(Vector2 dir)
        {
            Vector2 pivot = transform.position;
            // On teste le milieu et la pointe de l'arme
            for (int k = 1; k <= 2; k++)
            {
                Vector2 point = pivot + dir * (bladeLength * k * 0.5f);
                EnemyGrid.Query(point, MeleeHitRadius * Data.spriteScale, buffer);
                foreach (var enemy in buffer)
                {
                    if (meleeHits.Contains(enemy)) continue;
                    meleeHits.Add(enemy);
                    Vector2 away = enemy.Position - owner.Position;
                    var knockDir = away.sqrMagnitude > 0.0001f ? away.normalized : dir;
                    float damage = RollDamage(Data, owner, out bool crit);
                    enemy.TakeDamage(damage, knockDir, Data.knockback, crit, owner, Data);
                }
            }
        }

        // ------------------------------------------------------------ Tourelle

        void DeployTurret()
        {
            recoil = recoilDistance;
            // Posée un peu devant le joueur, en direction des ennemis
            var pos = Arena.Instance.Clamp(owner.Position + aimDirection * 1.2f, 0.5f);
            DeployedTurrets.Deploy(owner, Data, pos);
            AudioManager.Play(SoundId.TurretDeploy);
        }

        /// <summary>Dégâts d'une attaque avec les stats du joueur (critique compris).</summary>
        public static float RollDamage(WeaponData data, Player owner, out bool crit)
        {
            float critChance = data.critChance / 100f + owner.Stats.CritChance01;
            crit = Random.value < critChance;
            // Dégâts de l'arme + dégâts plats (mêlée / distance / ingénierie) x part de l'arme, puis le % de dégâts
            float baseDamage = Mathf.Max(1f, data.damage + owner.Stats.FlatDamage(data.kind) * data.statScaling);
            return baseDamage * owner.Stats.DamageMultiplier * (crit ? data.critMultiplier : 1f);
        }
    }
}
