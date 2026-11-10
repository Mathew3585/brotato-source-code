using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Projectiles tirés par les ennemis : tous mis à jour dans une seule boucle, recyclés.</summary>
    public class EnemyBullets : MonoBehaviour
    {
        const float Lifetime = 6f;
        const float HitRadius = 0.2f;

        class Bullet
        {
            public SpriteRenderer renderer;
            public Vector2 position, velocity;
            public float damage, life, radius;
        }

        static EnemyBullets instance;

        readonly Stack<SpriteRenderer> free = new();
        readonly List<Bullet> active = new();

        static EnemyBullets Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("EnemyBullets").AddComponent<EnemyBullets>();
                return instance;
            }
        }

        public static void Fire(Vector2 position, Vector2 direction, float speed, float damage, Sprite sprite, Color color, float scale)
        {
            var self = Instance;
            var sr = self.free.Count > 0 ? self.free.Pop() : self.Create();
            sr.gameObject.SetActive(true);
            sr.sprite = sprite;
            sr.color = color;
            sr.transform.localScale = Vector3.one * scale;
            sr.transform.position = position;
            self.active.Add(new Bullet
            {
                renderer = sr,
                position = position,
                velocity = direction.normalized * speed,
                damage = damage,
                life = Lifetime,
                radius = HitRadius * scale,
            });
        }

        public static void ClearAll()
        {
            if (instance == null) return;
            for (int i = instance.active.Count - 1; i >= 0; i--) instance.Release(i);
        }

        SpriteRenderer Create()
        {
            var go = new GameObject("EnemyBullet");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 30500;
            return sr;
        }

        void Release(int index)
        {
            var b = active[index];
            b.renderer.gameObject.SetActive(false);
            free.Push(b.renderer);
            active[index] = active[^1];
            active.RemoveAt(active.Count - 1);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var arena = Arena.Instance;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                b.position += b.velocity * dt;
                b.life -= dt;
                b.renderer.transform.position = b.position;
                b.renderer.transform.Rotate(0f, 0f, 360f * dt);

                bool hit = false;
                foreach (var p in Player.All)
                {
                    if (!p.IsAlive) continue;
                    float r = b.radius + p.Radius;
                    if ((p.Position - b.position).sqrMagnitude > r * r) continue;
                    p.TakeDamage(b.damage);
                    hit = true;
                    break;
                }

                if (hit || b.life <= 0f || (arena != null && !arena.Contains(b.position, 1f))) Release(i);
            }
        }
    }
}
