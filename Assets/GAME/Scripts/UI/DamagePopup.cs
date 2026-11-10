using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Chiffres de dégâts / textes flottants dans le monde (créés et recyclés automatiquement).</summary>
    public class DamagePopup : MonoBehaviour
    {
        const float Lifetime = 0.6f;
        const int MaxActive = 80;

        class Entry
        {
            public TextMeshPro text;
            public float time;
            public Vector3 velocity;
            public float scale;
        }

        const string PrefabName = "DamagePopup";

        static DamagePopup instance;
        TextMeshPro template;

        readonly Stack<Entry> free = new();
        readonly List<Entry> active = new();

        static DamagePopup Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("DamagePopups").AddComponent<DamagePopup>();
                return instance;
            }
        }

        public static void Show(Vector2 position, float amount, bool crit)
        {
            string text = Mathf.Max(1, Mathf.RoundToInt(amount)).ToString();
            ShowText(position, crit ? text + "!" : text, crit ? new Color(1f, 0.8f, 0.2f) : Color.white, crit ? 1.4f : 1f);
        }

        /// <summary>En ligne (hôte) : branché par NetGame pour envoyer les textes aux clients.</summary>
        public static System.Action<Vector2, string, Color, float> Broadcast;

        public static void ShowText(Vector2 position, string text, Color color, float scale = 1f)
        {
            if (NetMode.IsOnlineHost) Broadcast?.Invoke(position, text, color, scale);
            ShowTextLocal(position, text, color, scale);
        }

        /// <summary>Affiche sans diffuser (utilisé par les clients pour les textes reçus de l'hôte).</summary>
        public static void ShowTextLocal(Vector2 position, string text, Color color, float scale = 1f)
        {
            var self = Instance;
            if (self.active.Count >= MaxActive) return;

            var e = self.free.Count > 0 ? self.free.Pop() : self.Create();
            e.text.gameObject.SetActive(true);
            e.text.text = text;
            e.text.color = color;
            e.text.transform.position = position + Random.insideUnitCircle * 0.15f;
            e.time = 0f;
            e.scale = scale;
            e.velocity = new Vector3(Random.Range(-0.5f, 0.5f), 2.5f, 0f);
            self.active.Add(e);
        }

        Entry Create()
        {
            // Prefab « Resources/DamagePopup » (police, taille, contour réglables dans l'inspecteur)
            if (template == null) template = Resources.Load<TextMeshPro>(PrefabName);
            if (template != null) return new Entry { text = Instantiate(template, transform) };

            var go = new GameObject("Popup");
            go.transform.SetParent(transform);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 4.5f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.sortingOrder = 32000;
            return new Entry { text = tmp };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var e = active[i];
                e.time += dt;
                e.velocity.y -= 6f * dt;
                e.text.transform.position += e.velocity * dt;

                float t = e.time / Lifetime;
                float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
                e.text.transform.localScale = Vector3.one * (e.scale * pop);
                e.text.alpha = t < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, t);

                if (e.time >= Lifetime)
                {
                    e.text.gameObject.SetActive(false);
                    active.RemoveAt(i);
                    free.Push(e);
                }
            }
        }
    }
}
