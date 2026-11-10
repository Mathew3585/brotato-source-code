using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Ennemi : son déplacement et ses attaques dépendent de <see cref="EnemyData.behavior"/>.
    /// Pas d'Update : c'est l'EnemySpawner qui appelle Tick() sur tous les ennemis actifs.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> Active = new();

        const float HitFlashDuration = 0.1f;
        const float DeathDuration = 0.4f;
        static readonly Color HitTint = new(1f, 0.35f, 0.35f);
        static readonly Color WindupTint = new(1f, 0.55f, 0.2f);
        static readonly Color SlowTint = new(0.6f, 0.8f, 1f);

        enum ChargeState { Approach, Windup, Dash, Recover }

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer shadow;

        public EnemyData Data { get; private set; }
        public Vector2 Position { get; private set; }
        public float Radius { get; private set; }
        public bool IsAlive { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }

        float contactDamage;
        float projectileDamage;
        int waveIndex;
        Vector2 knockbackVelocity;
        float squashTimer;
        float hitTimer;
        float deathTimer;
        float slowTimer;
        float slowAmount;
        int activeIndex = -1;

        // Réseau
        static ushort nextNetId;
        public ushort NetId { get; private set; }
        /// <summary>Client en ligne : copie d'affichage d'un ennemi de l'hôte (aucune logique de jeu).</summary>
        public bool IsReplica { get; private set; }
        Vector2 replicaTarget;
        bool replicaWindup, replicaSlowed;
        /// <summary>Mort de fin de vague : pas de son (seules les morts par les armes en ont un).</summary>
        bool silentDeath;
        FrameAnimator anim;
        float age;

        // Comportements
        float actionTimer;
        float strafeSign;
        ChargeState chargeState;
        float chargeTimer;
        Vector2 dashDirection;
        int bossPattern;

        static readonly List<Enemy> neighbours = new();

        public void Spawn(EnemyData data, int wave, float healthMultiplier = 1f)
        {
            Data = data;
            waveIndex = wave;
            IsReplica = false;
            silentDeath = false;
            NetId = ++nextNetId == 0 ? ++nextNetId : nextNetId;
            var danger = DifficultySet.Current;
            MaxHealth = Health = data.HealthAtWave(wave) * (data.isBoss ? 1f : healthMultiplier)
                                 * GameManager.Instance.CoopHealthMultiplier * danger.enemyHealth;
            contactDamage = data.DamageAtWave(wave) * danger.enemyDamage;
            projectileDamage = data.ProjectileDamageAtWave(wave) * danger.enemyDamage;
            Radius = data.radius * data.scale;
            transform.localScale = Vector3.one * data.scale;
            Position = transform.position;
            IsAlive = true;
            hitTimer = squashTimer = 0f;
            deathTimer = 0f;
            slowTimer = 0f;
            slowAmount = 0f;
            age = Random.value * 10f;
            knockbackVelocity = Vector2.zero;
            actionTimer = data.fireInterval * Random.Range(0.5f, 1f);
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            chargeState = ChargeState.Approach;
            bossPattern = 0;

            body.color = data.tint;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one;
            if (shadow) shadow.enabled = true;
            anim = default;
            anim.Play(data.runFrames, data.animFps);
            body.sprite = data.runFrames.Length > 0 ? data.runFrames[0] : null;

            activeIndex = Active.Count;
            Active.Add(this);

            if (data.isBoss) GameManager.Instance.RegisterBoss(this);
        }

        // ------------------------------------------------------------ Réseau

        /// <summary>Drapeaux envoyés aux clients : 1 = regarde à gauche, 2 = touché, 4 = mourant, 8 = prépare une charge,
        /// 16 = ralenti, 32 = mort silencieuse (fin de vague).</summary>
        public byte NetFlags =>
            (byte)((body.flipX ? 1 : 0) | (hitTimer > 0f ? 2 : 0) | (!IsAlive ? 4 : 0)
                   | (chargeState == ChargeState.Windup ? 8 : 0) | (slowTimer > 0f ? 16 : 0)
                   | (!IsAlive && silentDeath ? 32 : 0));

        /// <summary>Client en ligne : crée la copie d'affichage d'un ennemi de l'hôte.</summary>
        public void SpawnReplica(EnemyData data, ushort netId, Vector2 position)
        {
            Data = data;
            NetId = netId;
            IsReplica = true;
            MaxHealth = Health = 1f;
            Radius = data.radius * data.scale;
            transform.localScale = Vector3.one * data.scale;
            transform.position = position;
            Position = replicaTarget = position;
            IsAlive = true;
            hitTimer = deathTimer = slowTimer = squashTimer = 0f;
            age = Random.value * 10f;
            chargeState = ChargeState.Approach;

            body.color = data.tint;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one;
            if (shadow) shadow.enabled = true;
            anim = default;
            anim.Play(data.runFrames, data.animFps);
            body.sprite = data.runFrames.Length > 0 ? data.runFrames[0] : null;

            activeIndex = Active.Count;
            Active.Add(this);
            if (data.isBoss) GameManager.Instance.RegisterBoss(this);
        }

        public void ApplySnapshot(Vector2 position, byte flags)
        {
            replicaTarget = position;
            body.flipX = (flags & 1) != 0;
            if ((flags & 2) != 0)
            {
                if (hitTimer <= 0f) squashTimer = JuiceSettings.Settings.hitSquashTime;
                hitTimer = HitFlashDuration;
            }
            replicaWindup = (flags & 8) != 0;
            replicaSlowed = (flags & 16) != 0;
            if ((flags & 4) != 0 && IsAlive) ReplicaDie((flags & 32) != 0);
        }

        public void SetReplicaHealth(float health, float maxHealth)
        {
            Health = health;
            MaxHealth = Mathf.Max(1f, maxHealth);
        }

        void ReplicaDie(bool silent)
        {
            IsAlive = false;
            deathTimer = DeathDuration;
            if (Data.deadSprite != null) body.sprite = Data.deadSprite;
            body.transform.localRotation = Quaternion.identity;
            if (shadow) shadow.enabled = false;
            bool big = Data.isBoss || Data.isElite;
            Debris.Burst(Position, Data.debrisColor * Data.tint, big ? 30 : 7, big ? 7f : 4f, big ? 1.6f : 1f);
            if (!silent) AudioManager.Play(SoundId.EnemyDeath, big ? 1.5f : 1f, big ? 0.7f : 1f);
        }

        void TickReplica(float dt)
        {
            Vector2 previous = Position;
            Position = Vector2.Lerp(Position, replicaTarget, 1f - Mathf.Exp(-15f * dt));
            transform.position = Position;

            if (!IsAlive)
            {
                // L'hôte retire l'ennemi après son animation de mort : on garde juste le fondu
                deathTimer = Mathf.Max(0f, deathTimer - dt);
                var c = Data.tint;
                c.a = Mathf.Clamp01(deathTimer / DeathDuration);
                body.color = c;
                if (Data.deadSprite == null) body.transform.localScale = DeathScale(c.a);
                // Normalement retirée par l'hôte ; sécurité si la réplique n'est plus suivie
                if (deathTimer <= 0f) Despawn();
                return;
            }

            Vector2 velocity = dt > 0f ? (Position - previous) / dt : Vector2.zero;
            chargeState = replicaWindup ? ChargeState.Windup : ChargeState.Approach;
            slowTimer = replicaSlowed ? 0.1f : 0f;
            UpdateVisuals(velocity, Vector2.zero, dt, keepFlip: true);
        }

        public void Tick(float dt)
        {
            age += dt;
            if (IsReplica)
            {
                TickReplica(dt);
                return;
            }
            if (!IsAlive)
            {
                TickDeath(dt);
                return;
            }

            var target = Player.FindNearestAlive(Position);
            Vector2 toTarget = Vector2.zero;
            float distance = float.MaxValue;
            if (target != null)
            {
                Vector2 d = target.Position - Position;
                distance = d.magnitude;
                toTarget = distance > 0.0001f ? d / distance : Vector2.zero;
            }

            Vector2 desired = Data.behavior switch
            {
                EnemyBehavior.Ranged => TickRanged(dt, toTarget, distance),
                EnemyBehavior.Charger => TickCharger(dt, toTarget, distance),
                EnemyBehavior.Turret => TickTurret(dt, toTarget, distance),
                EnemyBehavior.Boss => TickBoss(dt, toTarget, distance),
                _ => toTarget * Data.moveSpeed,
            };

            if (slowTimer > 0f)
            {
                slowTimer -= dt;
                desired *= 1f - slowAmount;
            }
            else slowAmount = 0f;

            Move(desired, dt);
            DealContactDamage();
            UpdateVisuals(desired, toTarget, dt);
        }

        // ------------------------------------------------------------ Comportements

        Vector2 TickRanged(float dt, Vector2 toTarget, float distance)
        {
            actionTimer -= dt;
            if (actionTimer <= 0f && distance < Data.preferredDistance + 3f)
            {
                Shoot(toTarget);
                actionTimer = Data.fireInterval;
            }

            if (distance > Data.preferredDistance + 0.5f) return toTarget * Data.moveSpeed;
            if (distance < Data.preferredDistance - 1.5f) return -toTarget * Data.moveSpeed;
            // À bonne distance : se déplace sur le côté
            return new Vector2(-toTarget.y, toTarget.x) * (strafeSign * Data.moveSpeed * 0.4f);
        }

        Vector2 TickCharger(float dt, Vector2 toTarget, float distance)
        {
            chargeTimer -= dt;
            switch (chargeState)
            {
                case ChargeState.Approach:
                    if (distance < Data.chargeTriggerDistance)
                    {
                        chargeState = ChargeState.Windup;
                        chargeTimer = Data.chargeWindup;
                    }
                    return toTarget * Data.moveSpeed;

                case ChargeState.Windup:
                    dashDirection = toTarget;
                    if (chargeTimer <= 0f)
                    {
                        chargeState = ChargeState.Dash;
                        chargeTimer = Data.chargeDuration;
                    }
                    return Vector2.zero;

                case ChargeState.Dash:
                    if (chargeTimer <= 0f)
                    {
                        chargeState = ChargeState.Recover;
                        chargeTimer = 0.6f;
                    }
                    return dashDirection * Data.chargeSpeed;

                default: // Recover
                    if (chargeTimer <= 0f) chargeState = ChargeState.Approach;
                    return toTarget * (Data.moveSpeed * 0.3f);
            }
        }

        Vector2 TickTurret(float dt, Vector2 toTarget, float distance)
        {
            actionTimer -= dt;
            if (actionTimer <= 0f && distance < 14f)
            {
                Shoot(toTarget);
                actionTimer = Data.fireInterval;
            }
            return Vector2.zero;
        }

        Vector2 TickBoss(float dt, Vector2 toTarget, float distance)
        {
            // Pendant une charge, on laisse la logique de charge piloter
            if (chargeState != ChargeState.Approach) return TickCharger(dt, toTarget, distance);

            actionTimer -= dt;
            if (actionTimer <= 0f)
            {
                actionTimer = Data.fireInterval;
                switch (bossPattern++ % 4)
                {
                    case 0: // salve en étoile
                        ShootPattern(toTarget, 18, 360f);
                        break;
                    case 1: // charge
                        chargeState = ChargeState.Windup;
                        chargeTimer = Data.chargeWindup;
                        break;
                    case 2: // invocation
                        Summon();
                        break;
                    default: // tir visé en éventail
                        ShootPattern(toTarget, 5, 50f);
                        break;
                }
            }
            return toTarget * Data.moveSpeed;
        }

        void Shoot(Vector2 direction) => ShootPattern(direction, Data.projectilesPerShot, Data.projectileSpread);

        /// <summary>En ligne (hôte) : un ennemi a tiré (NetGame le rejoue chez les clients).</summary>
        public static event System.Action<EnemyData, Vector2, Vector2, int, float> VolleyFired;

        void ShootPattern(Vector2 direction, int count, float spread)
        {
            if (Data.projectileSprite == null) return;
            FireVolley(Data, Position, Radius, direction, count, spread, projectileDamage);
            if (NetMode.IsOnlineHost) VolleyFired?.Invoke(Data, Position, direction, count, spread);
        }

        /// <summary>Client en ligne : tir d'ennemi purement visuel (les dégâts sont calculés par l'hôte).</summary>
        public static void FireVolleyVisual(EnemyData data, Vector2 position, Vector2 direction, int count, float spread) =>
            FireVolley(data, position, data.radius * data.scale, direction, count, spread, 0f);

        static void FireVolley(EnemyData data, Vector2 position, float radius, Vector2 direction, int count, float spread, float damage)
        {
            count = Mathf.Max(1, count);
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            // En étoile (360°) on ne double pas le premier et le dernier projectile
            float step = count == 1 ? 0f : spread >= 360f ? 360f / count : spread / (count - 1);
            float start = spread >= 360f ? 0f : -spread / 2f;
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + start + step * i) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                EnemyBullets.Fire(position + dir * radius, dir, data.projectileSpeed, damage,
                    data.projectileSprite, data.projectileColor, data.projectileScale);
            }
            AudioManager.Play(SoundId.EnemyShoot);
        }

        void Summon()
        {
            if (Data.splitInto == null) return;
            for (int i = 0; i < Mathf.Max(1, Data.splitCount); i++)
            {
                var pos = Arena.Instance.Clamp(Position + Random.insideUnitCircle.normalized * (Radius + 1f), 1f);
                GameManager.Instance.EnemyPool.Get(pos).Spawn(Data.splitInto, waveIndex);
            }
        }

        // ------------------------------------------------------------ Déplacement & contact

        void Move(Vector2 desired, float dt)
        {
            // Séparation : les ennemis se repoussent pour ne pas tous s'empiler
            Vector2 push = Vector2.zero;
            EnemyGrid.Query(Position, Radius, neighbours);
            foreach (var other in neighbours)
            {
                if (other == this) continue;
                Vector2 d = Position - other.Position;
                float dist = d.magnitude;
                float minDist = Radius + other.Radius;
                if (dist < 0.0001f) push += Random.insideUnitCircle * 0.5f;
                else if (dist < minDist) push += d / dist * (minDist - dist);
            }
            // Les gros ennemis (boss, élites) poussent les petits sans être bloqués
            if (Data.isBoss) push *= 0.1f;

            Vector2 velocity = desired + push * 8f + knockbackVelocity;
            knockbackVelocity *= Mathf.Exp(-10f * dt);
            Position = Arena.Instance.Clamp(Position + velocity * dt, Radius);
            transform.position = Position;
        }

        void DealContactDamage()
        {
            foreach (var p in Player.All)
            {
                if (!p.IsAlive) continue;
                float r = Radius + p.Radius;
                if ((p.Position - Position).sqrMagnitude <= r * r) p.TakeDamage(contactDamage);
            }
        }

        // ------------------------------------------------------------ Visuel

        void UpdateVisuals(Vector2 desired, Vector2 toTarget, float dt, bool keepFlip = false)
        {
            var bt = body.transform;
            bool singleFrame = Data.runFrames.Length <= 1;

            if (hitTimer > 0f)
            {
                hitTimer -= dt;
                if (Data.hitSprite != null) body.sprite = Data.hitSprite;
                body.color = Data.hitSprite != null ? Data.tint : HitTint;
            }
            else
            {
                if (!singleFrame) body.sprite = anim.Tick(dt);
                bool windup = chargeState == ChargeState.Windup && Mathf.Repeat(age, 0.15f) < 0.075f;
                body.color = windup ? WindupTint : slowTimer > 0f ? Data.tint * SlowTint : Data.tint;
            }

            // Orientation : vers la cible pour ceux qui tirent, sinon vers le mouvement
            bool looksAtTarget = Data.behavior is EnemyBehavior.Ranged or EnemyBehavior.Turret;
            Vector2 facing = looksAtTarget ? toTarget : desired;

            if (Data.rotateToDirection)
            {
                if (desired.sqrMagnitude > 0.01f)
                {
                    float angle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg + Data.rotationOffset;
                    bt.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
            else if (!keepFlip && Mathf.Abs(facing.x) > 0.05f)
            {
                body.flipX = Data.facesLeft ? facing.x > 0f : facing.x < 0f;
            }

            // Rebond de marche pour les sprites sans animation
            if (singleFrame && Data.bobAmount > 0f)
            {
                float moving = desired.sqrMagnitude > 0.01f ? 1f : 0.3f;
                float bob = Mathf.Abs(Mathf.Sin(age * 10f)) * Data.bobAmount * moving;
                bt.localPosition = new Vector3(0f, bob, 0f);
                bt.localScale = new Vector3(1f + bob * 0.5f, 1f - bob * 0.5f, 1f);
            }
            else bt.localScale = Vector3.one;

            // Écrasement à l'impact, qui revient vite à la normale
            if (squashTimer > 0f)
            {
                var juice = JuiceSettings.Settings;
                squashTimer -= dt;
                float s = juice.hitSquash * Mathf.Clamp01(squashTimer / Mathf.Max(0.01f, juice.hitSquashTime));
                var scale = bt.localScale;
                bt.localScale = new Vector3(scale.x * (1f + s), scale.y * (1f - s), 1f);
            }

            body.sortingOrder = SortingUtil.OrderFromY(Position.y);
        }

        void TickDeath(float dt)
        {
            deathTimer -= dt;
            float k = Mathf.Clamp01(deathTimer / DeathDuration);
            var c = Data.tint;
            c.a = k;
            body.color = c;
            if (Data.deadSprite == null) body.transform.localScale = DeathScale(k);
            if (deathTimer <= 0f) Despawn();
        }

        /// <summary>Gonfle un peu puis rétrécit (k : 1 → 0 pendant la mort).</summary>
        static Vector3 DeathScale(float k) =>
            Vector3.one * (k * (1f + JuiceSettings.Settings.deathPop * Mathf.Sin((1f - k) * Mathf.PI)));

        /// <summary>Flash + écrasement du sprite à l'impact.</summary>
        void Squash()
        {
            hitTimer = HitFlashDuration;
            squashTimer = JuiceSettings.Settings.hitSquashTime;
        }

        // ------------------------------------------------------------ Dégâts & mort

        public void TakeDamage(float amount, Vector2 direction, float knockback, bool crit, Player source = null, WeaponData weapon = null)
        {
            // Client en ligne : les dégâts sont calculés par l'hôte (les balles locales ne sont que visuelles)
            if (!IsAlive || IsReplica) return;

            if (source != null) source.RunStats.AddDamage(weapon, UnityEngine.Mathf.Min(amount, Health));
            Health -= amount;
            Squash();
            // Pas de recul pendant une charge : elle doit rester lisible
            if (chargeState != ChargeState.Dash)
                knockbackVelocity += direction * (knockback * 12f * (1f - Data.knockbackResistance));
            DamagePopup.Show(Position + Vector2.up * Radius, amount, crit);
            AudioManager.Play(SoundId.EnemyHit, crit ? 1.3f : 1f);
            int sparks = JuiceSettings.Settings.hitSparks * (crit ? 2 : 1);
            if (sparks > 0) Debris.Burst(Position, crit ? new Color(1f, 0.85f, 0.3f) : Color.white, sparks, 5f, 0.6f);

            // Vol de vie : pas pour les structures (tourelles)
            if (source != null && (weapon == null || weapon.kind != WeaponKind.Turret)) source.TryLifeSteal();

            if (Health <= 0f)
            {
                if (source != null) source.RunStats.kills++;
                Die(true);
            }
        }

        /// <summary>Ralentit l'ennemi (on garde le plus fort des ralentissements en cours).</summary>
        public void ApplySlow(float amount, float duration)
        {
            if (!IsAlive) return;
            if (Data.isBoss) amount *= 0.5f; // les boss résistent
            slowAmount = Mathf.Max(slowAmount, Mathf.Clamp01(amount));
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        /// <param name="dropLoot">Lâche matériaux / caisse (et se divise pour le scarabée géant).</param>
        /// <param name="silent">Pas de son de mort (fin de vague).</param>
        public void Die(bool dropLoot, bool silent = false)
        {
            if (!IsAlive) return;
            IsAlive = false;
            silentDeath = silent;
            deathTimer = DeathDuration;
            if (Data.deadSprite != null) body.sprite = Data.deadSprite;
            body.transform.localRotation = Quaternion.identity;
            if (shadow) shadow.enabled = false;

            // Ressenti : débris de la couleur de l'ennemi, son, et micro-pause sur les gros
            bool big = Data.isBoss || Data.isElite;
            Debris.Burst(Position, Data.debrisColor * Data.tint, big ? 30 : 7, big ? 7f : 4f, big ? 1.6f : 1f);
            if (!silent) AudioManager.Play(SoundId.EnemyDeath, big ? 1.5f : 1f, big ? 0.7f : 1f);
            if (big && !Data.isBoss) GameManager.Instance.HitStop(0.08f);
            if (!silent && CameraController.Instance) CameraController.Instance.Shake(JuiceSettings.Settings.killShake);

            if (!dropLoot) return;
            var gm = GameManager.Instance;
            // Les élites et les boss lâchent toujours leur butin ; les ennemis normaux, moins souvent (WaveSet.materialDropMultiplier)
            float dropChance = Data.dropChance * DifficultySet.Current.materials * (big ? 1f : gm.waveSet.materialDropMultiplier);
            if (Random.value <= dropChance) gm.DropMaterials(Position, Data.materialDrop);
            // Caisses rares : le boss en donne toujours une, les élites parfois, les autres très rarement
            if (Data.isBoss) gm.DropCrate(Position, force: true);
            else if (Random.value < Mathf.Max(Data.crateChance, gm.waveSet.crateChancePerKill)) gm.DropCrate(Position);

            // Division : le scarabée géant libère des larves
            if (Data.behavior != EnemyBehavior.Boss && Data.splitInto != null)
            {
                for (int i = 0; i < Data.splitCount; i++)
                {
                    var pos = Arena.Instance.Clamp(Position + Random.insideUnitCircle * Radius, 0.5f);
                    gm.EnemyPool.Get(pos).Spawn(Data.splitInto, waveIndex);
                }
            }

            if (Data.isBoss) gm.OnBossKilled(this);
        }

        /// <summary>Retire immédiatement l'ennemi du jeu et le rend au pool.</summary>
        public void Despawn()
        {
            if (activeIndex < 0) return;

            // Retrait en O(1) : on échange avec le dernier élément
            int last = Active.Count - 1;
            var moved = Active[last];
            Active[activeIndex] = moved;
            moved.activeIndex = activeIndex;
            Active.RemoveAt(last);
            activeIndex = -1;
            IsAlive = false;

            GameManager.Instance.EnemyPool.Release(this);
        }
    }
}
