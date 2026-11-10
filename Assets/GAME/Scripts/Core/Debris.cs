using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Petits pixels qui giclent à la mort des ennemis (sans système de particules, tout est recyclé).</summary>
    public class Debris : MonoBehaviour
    {
        const int MaxActive = 400;

        class Bit
        {
            public SpriteRenderer renderer;
            public Vector2 position, velocity;
            public float life, maxLife, size;
        }

        static Debris instance;
        static Sprite pixel;

        readonly Stack<SpriteRenderer> free = new();
        readonly List<Bit> active = new();

        static Debris Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("Debris").AddComponent<Debris>();
                return instance;
            }
        }

        /// <summary>Carré blanc partagé (aussi utilisé par l'indicateur de réanimation).</summary>
        public static Sprite Pixel
        {
            get
            {
                // Carré blanc généré une fois (4x4 px à 32 px/unité = 0,125 unité)
                if (pixel == null)
                    pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                return pixel;
            }
        }

        public static void Burst(Vector2 position, Color color, int count, float speed = 4f, float size = 1f)
        {
            var self = Instance;
            for (int i = 0; i < count && self.active.Count < MaxActive; i++)
            {
                var sr = self.free.Count > 0 ? self.free.Pop() : self.Create();
                sr.gameObject.SetActive(true);
                sr.color = Color.Lerp(color, Color.white, Random.value * 0.3f);
                float life = Random.Range(0.25f, 0.5f);
                self.active.Add(new Bit
                {
                    renderer = sr,
                    position = position,
                    velocity = Random.insideUnitCircle.normalized * (speed * Random.Range(0.4f, 1f)),
                    life = life,
                    maxLife = life,
                    size = size * Random.Range(0.6f, 1.3f),
                });
            }
        }

        SpriteRenderer Create()
        {
            var go = new GameObject("Bit");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Pixel;
            sr.sortingOrder = 30000;
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                b.life -= dt;
                if (b.life <= 0f)
                {
                    b.renderer.gameObject.SetActive(false);
                    free.Push(b.renderer);
                    active[i] = active[^1];
                    active.RemoveAt(active.Count - 1);
                    continue;
                }
                b.position += b.velocity * dt;
                b.velocity *= Mathf.Exp(-6f * dt);
                float k = b.life / b.maxLife;
                b.renderer.transform.position = b.position;
                b.renderer.transform.localScale = Vector3.one * (b.size * k);
            }
        }
    }
}
