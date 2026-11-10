using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Effets animés jetables (explosions...) : créés et recyclés automatiquement.</summary>
    public class SpriteEffects : MonoBehaviour
    {
        class Fx
        {
            public SpriteRenderer renderer;
            public Sprite[] frames;
            public float fps;
            public float time;
        }

        static SpriteEffects instance;

        readonly Stack<SpriteRenderer> free = new();
        readonly List<Fx> active = new();

        static SpriteEffects Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("SpriteEffects").AddComponent<SpriteEffects>();
                return instance;
            }
        }

        public static void Play(Sprite[] frames, Vector2 position, float scale, float fps = 16f, Color? color = null) =>
            PlayRotated(frames, position, Random.Range(0, 4) * 90f, scale, fps, color);

        /// <summary>Effet orienté (ex : flash de tir dans la direction du canon).</summary>
        public static void PlayRotated(Sprite[] frames, Vector2 position, float angle, float scale, float fps = 16f, Color? color = null)
        {
            if (frames == null || frames.Length == 0) return;
            var self = Instance;
            var sr = self.free.Count > 0 ? self.free.Pop() : self.Create();
            sr.gameObject.SetActive(true);
            sr.transform.position = position;
            sr.transform.localScale = Vector3.one * scale;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            sr.color = color ?? Color.white;
            sr.sprite = frames[0];
            self.active.Add(new Fx { renderer = sr, frames = frames, fps = fps });
        }

        SpriteRenderer Create()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 31500;
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var fx = active[i];
                fx.time += dt;
                int frame = (int)(fx.time * fx.fps);
                if (frame < fx.frames.Length)
                {
                    fx.renderer.sprite = fx.frames[frame];
                    continue;
                }
                fx.renderer.gameObject.SetActive(false);
                free.Push(fx.renderer);
                active.RemoveAt(i);
            }
        }
    }
}
