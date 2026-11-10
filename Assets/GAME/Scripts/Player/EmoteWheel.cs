using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Roue des emotes autour d'un joueur, et bulle de l'emote jouée au-dessus de sa tête.
    /// Sprites du monde (comme DownedIndicator). Prefab Resources/EmoteWheel (créé par EmoteBuilder),
    /// repli par code s'il manque. Chaque slot : enfant 0 = fond, enfant 1 = icône.
    /// Les emotes et les tailles sont dans EmoteSet.
    /// </summary>
    public class EmoteWheel : MonoBehaviour
    {
        const string PrefabName = "EmoteWheel";
        const int Order = 30500;
        const int DefaultSlots = 8;
        static readonly Color SlotColor = new(0.05f, 0.05f, 0.08f, 0.8f);

        [SerializeField] Transform wheel;
        [SerializeField] Transform[] slots = new Transform[0];
        [SerializeField] Transform bubble;
        [SerializeField] SpriteRenderer bubbleBack;
        [SerializeField] SpriteRenderer bubbleIcon;

        SpriteRenderer[] backs, icons;
        float[] slotScales;
        EmoteSet set;
        int count;
        float open01;
        float bubbleTime = -1f;
        Color playerColor = Color.white;

        public bool IsOpen { get; private set; }
        public int Selected { get; private set; } = -1;

        public static EmoteWheel Create(Transform parent)
        {
            var template = Resources.Load<EmoteWheel>(PrefabName);
            var self = template != null ? Instantiate(template) : BuildDefault(EmoteSet.Instance != null ? EmoteSet.Instance.slotSprite : null);
            self.name = PrefabName;
            self.transform.SetParent(parent, false);
            self.transform.localPosition = Vector3.zero;
            self.Init();
            return self;
        }

        /// <summary>Construit la roue par code : repli à l'exécution, et modèle du prefab créé par EmoteBuilder.</summary>
        public static EmoteWheel BuildDefault(Sprite slotSprite)
        {
            var self = new GameObject(PrefabName).AddComponent<EmoteWheel>();
            self.wheel = new GameObject("Wheel").transform;
            self.wheel.SetParent(self.transform, false);
            // Centrée sur le corps plutôt que sur les pieds
            self.wheel.localPosition = new Vector3(0f, 0.4f, 0f);
            self.slots = new Transform[DefaultSlots];
            for (int i = 0; i < DefaultSlots; i++)
            {
                var slot = new GameObject($"Slot{i + 1}").transform;
                slot.SetParent(self.wheel, false);
                MakeSprite("Back", slot, slotSprite, SlotColor, Order);
                MakeSprite("Icon", slot, null, Color.white, Order + 1);
                self.slots[i] = slot;
            }
            self.bubble = new GameObject("Bubble").transform;
            self.bubble.SetParent(self.transform, false);
            self.bubbleBack = MakeSprite("Back", self.bubble, slotSprite, new Color(1f, 1f, 1f, 0.9f), Order + 2);
            self.bubbleIcon = MakeSprite("Icon", self.bubble, null, Color.white, Order + 3);
            self.wheel.gameObject.SetActive(false);
            self.bubble.gameObject.SetActive(false);
            return self;
        }

        static SpriteRenderer MakeSprite(string objectName, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void Init()
        {
            set = EmoteSet.Instance;
            count = set != null ? set.emotes.Length : 0;
            // Plus d'emotes que de slots dans le prefab : on duplique le premier
            if (slots.Length > 0 && slots.Length < count)
            {
                var list = new List<Transform>(slots);
                while (list.Count < count) list.Add(Instantiate(slots[0], wheel));
                slots = list.ToArray();
            }
            count = Mathf.Min(count, slots.Length);

            backs = new SpriteRenderer[slots.Length];
            icons = new SpriteRenderer[slots.Length];
            slotScales = new float[slots.Length];
            float step = count > 0 ? 360f / count : 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                backs[i] = slots[i].GetChild(0).GetComponent<SpriteRenderer>();
                icons[i] = slots[i].GetChild(1).GetComponent<SpriteRenderer>();
                bool used = i < count;
                slots[i].gameObject.SetActive(used);
                if (!used) continue;
                // Dans le sens des aiguilles d'une montre en partant du haut
                float a = (90f - i * step) * Mathf.Deg2Rad;
                slots[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * set.wheelRadius;
                FitBack(backs[i], set.slotSize);
                icons[i].sprite = set.emotes[i];
                icons[i].transform.localScale = Vector3.one * set.iconScale;
                slotScales[i] = 1f;
            }
            if (set == null) return;
            FitBack(bubbleBack, set.bubbleSize);
            bubbleIcon.transform.localScale = Vector3.one * set.bubbleIconScale;
        }

        /// <summary>Met le fond à la taille voulue, quel que soit son sprite (rond pixel, ou carré de Debris en repli).</summary>
        static void FitBack(SpriteRenderer sr, float size)
        {
            if (sr.sprite == null) sr.sprite = Debris.Pixel;
            float unit = Mathf.Max(0.001f, sr.sprite.bounds.size.x);
            sr.transform.localScale = Vector3.one * (size / unit);
        }

        public void Open(Color color)
        {
            if (count == 0) return;
            playerColor = color;
            IsOpen = true;
            Selected = -1;
        }

        /// <summary>Vise l'emote dans cette direction (stick ou touches). Stick relâché : la sélection reste.</summary>
        public void Aim(Vector2 direction)
        {
            if (!IsOpen || direction.sqrMagnitude < 0.25f) return;
            // 0° = haut, 90° = droite (même ordre que les slots)
            float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / (360f / count)) % count;
            if (index == Selected) return;
            Selected = index;
            AudioManager.Play(SoundId.UiSelect, 0.6f);
        }

        /// <summary>Ferme la roue et renvoie l'emote choisie (-1 : aucune).</summary>
        public int Close()
        {
            if (!IsOpen) return -1;
            IsOpen = false;
            return Selected;
        }

        /// <summary>Affiche l'emote dans une bulle au-dessus du joueur.</summary>
        public void Show(int id)
        {
            if (id < 0 || id >= count) return;
            bubbleIcon.sprite = set.emotes[id];
            bubbleTime = 0f;
            bubble.gameObject.SetActive(true);
        }

        void Update()
        {
            // Temps réel : la micro-pause à l'impact ne doit pas figer la roue
            float dt = Time.unscaledDeltaTime;
            UpdateWheel(dt);
            UpdateBubble(dt);
        }

        void UpdateWheel(float dt)
        {
            open01 = Mathf.MoveTowards(open01, IsOpen ? 1f : 0f, dt * (IsOpen ? 7f : 10f));
            bool visible = open01 > 0f;
            if (wheel.gameObject.activeSelf != visible) wheel.gameObject.SetActive(visible);
            if (!visible) return;

            wheel.localScale = Vector3.one * (IsOpen ? EaseOutBack(open01) : open01);
            var selectedColor = new Color(playerColor.r, playerColor.g, playerColor.b, 0.95f);
            for (int i = 0; i < count; i++)
            {
                bool selected = i == Selected;
                slotScales[i] = Mathf.Lerp(slotScales[i], selected ? set.selectedScale : 1f, 1f - Mathf.Exp(-18f * dt));
                slots[i].localScale = Vector3.one * slotScales[i];
                backs[i].color = selected ? selectedColor : SlotColor;
                icons[i].color = selected ? Color.white : new Color(1f, 1f, 1f, 0.75f);
            }
        }

        void UpdateBubble(float dt)
        {
            if (bubbleTime < 0f) return;
            bubbleTime += dt;
            float duration = set.showDuration;
            if (bubbleTime >= duration)
            {
                bubbleTime = -1f;
                bubble.gameObject.SetActive(false);
                return;
            }
            // Pop à l'apparition, léger flottement, rétrécit à la fin
            float pop = bubbleTime < 0.25f ? EaseOutBack(bubbleTime / 0.25f) : 1f;
            float end = Mathf.Clamp01((duration - bubbleTime) / 0.2f);
            bubble.localScale = Vector3.one * (pop * end);
            bubble.localPosition = new Vector3(0f, set.bubbleHeight + Mathf.Sin(bubbleTime * 5f) * 0.05f, 0f);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}
