using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float hitRadius = 0.15f;

        Vector2 direction;
        float speed;
        float maxDistance;
        float traveled;
        float damage;
        float knockback;
        float explosionRadius;
        float slowAmount, slowDuration;
        bool crit;
        int pierceLeft;
        readonly List<Enemy> alreadyHit = new();
        Player owner;
        WeaponData weaponData;

        static readonly List<Enemy> hits = new();
        static readonly List<Enemy> explosionHits = new();

        public void Launch(Vector2 dir, WeaponData weapon, float dmg, bool isCrit, float range, Player shooter)
        {
            owner = shooter;
            weaponData = weapon;
            direction = dir;
            speed = weapon.projectileSpeed;
            maxDistance = range + 1f;
            traveled = 0f;
            damage = dmg;
            crit = isCrit;
            knockback = weapon.knockback;
            pierceLeft = weapon.pierce;
            explosionRadius = weapon.explosionRadius;
            slowAmount = weapon.slowAmount;
            slowDuration = weapon.slowDuration;
            alreadyHit.Clear();

            spriteRenderer.sprite = weapon.projectileSprite;
            spriteRenderer.color = isCrit ? new Color(1f, 0.85f, 0.4f) : Color.white;
            transform.localScale = Vector3.one * (weapon.projectileScale * (isCrit ? 1.3f : 1f));
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + weapon.projectileAngleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float step = speed * dt;
            Vector2 pos = (Vector2)transform.position + direction * step;
            transform.position = pos;
            traveled += step;

            EnemyGrid.Query(pos, hitRadius, hits);
            foreach (var enemy in hits)
            {
                if (alreadyHit.Contains(enemy)) continue;

                if (explosionRadius > 0f)
                {
                    Explode(pos);
                    return;
                }

                alreadyHit.Add(enemy);
                enemy.TakeDamage(damage, direction, knockback, crit, owner, weaponData);
                if (slowAmount > 0f) enemy.ApplySlow(slowAmount, slowDuration);

                if (pierceLeft-- <= 0)
                {
                    Release();
                    return;
                }
                damage *= 0.8f; // chaque ennemi traversé réduit un peu les dégâts
            }

            if (traveled >= maxDistance || !Arena.Instance.Contains(pos, 2f))
            {
                // Les projectiles explosifs explosent aussi en bout de course
                if (explosionRadius > 0f) Explode(pos);
                else Release();
            }
        }

        void Explode(Vector2 center)
        {
            EnemyGrid.Query(center, explosionRadius, explosionHits);
            foreach (var enemy in explosionHits)
            {
                Vector2 away = enemy.Position - center;
                var dir = away.sqrMagnitude > 0.0001f ? away.normalized : direction;
                enemy.TakeDamage(damage, dir, knockback, crit, owner, weaponData);
            }

            var gm = GameManager.Instance;
            SpriteEffects.Play(gm.explosionFrames, center, explosionRadius * 1.6f, 18f);
            AudioManager.Play(SoundId.Explosion);
            if (CameraController.Instance) CameraController.Instance.Shake(0.12f);
            Release();
        }

        void Release() => GameManager.Instance.ProjectilePool.Release(this);
    }
}
