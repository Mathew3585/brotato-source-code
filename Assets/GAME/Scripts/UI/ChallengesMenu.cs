using System;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Écran des défis (menu principal) : à gauche les cartes (récompense + cadenas tant que le défi n'est pas réussi),
    /// à droite le détail du défi sélectionné. L'écran est dans la scène MainMenu ; les cartes sont créées
    /// au premier affichage depuis le prefab ChallengeCard, une par défi de ChallengeSet.
    /// </summary>
    public class ChallengesMenu : MonoBehaviour
    {
        static readonly Color LockedTint = new(0.08f, 0.08f, 0.1f, 1f);
        static readonly Color DoneColor = new(0.45f, 1f, 0.5f);

        [SerializeField] RectTransform panel;
        [SerializeField] TextMeshProUGUI counter;

        [Header("Cartes (gauche)")]
        [SerializeField] ChallengeCardView cardPrefab;
        [Tooltip("Conteneur des cartes (GridLayoutGroup)")]
        [SerializeField] RectTransform cardGrid;
        [SerializeField] int cardsPerRow = 5;

        [Header("Détail (droite)")]
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI goal;
        [SerializeField] TextMeshProUGUI progress;
        [SerializeField] Image rewardIcon;
        [SerializeField] TextMeshProUGUI rewardName;
        [SerializeField] TextMeshProUGUI rewardText;

        EventSystem eventSystem;
        Action onClosed;
        int openedFrame;
        bool initialized;
        GameObject lastSelected;
        List<ChallengeSet.Challenge> list = new();
        readonly List<ChallengeCardView> cards = new();

        public bool IsOpen => gameObject.activeSelf;

        void Init()
        {
            if (initialized) return;
            initialized = true;
            var set = ChallengeSet.Instance;
            list = set != null ? set.challenges : new List<ChallengeSet.Challenge>();

            // Une carte par défi, et la navigation en grille
            var rows = new List<List<Selectable>>();
            for (int i = 0; i < list.Count; i++)
            {
                var card = Instantiate(cardPrefab, cardGrid);
                card.name = "Card " + list[i].id;
                cards.Add(card);
                if (i % cardsPerRow == 0) rows.Add(new List<Selectable>());
                rows[^1].Add(card.button);
            }
            if (rows.Count > 0) UIFactory.LinkNavigation(rows);
        }

        public void Open(EventSystem es, Action closed)
        {
            Init();
            eventSystem = es;
            onClosed = closed;
            openedFrame = Time.frameCount;

            int done = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                bool isDone = Challenges.IsDone(list[i]);
                if (isDone) done++;
                cards[i].Show(list[i], isDone);
            }
            counter.text = $"{done} / {list.Count}";

            gameObject.SetActive(true);
            lastSelected = null;
            if (eventSystem != null && cards.Count > 0) eventSystem.SetSelectedGameObject(cards[0].gameObject);
            RefreshDetail();

            panel.localScale = Vector3.one * 0.9f;
            Tween.Scale(panel, Vector3.one, 0.25f, Ease.OutBack, useUnscaledTime: true);
        }

        void Close()
        {
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        void Update()
        {
            RefreshDetail();
            if (Time.frameCount == openedFrame) return;
            var kb = Keyboard.current;
            bool back = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame);
            foreach (var pad in Gamepad.all) back |= pad.buttonEast.wasPressedThisFrame;
            if (!back) return;
            AudioManager.Play(SoundId.UiBack);
            Close();
        }

        /// <summary>Panneau de droite : le défi de la carte sélectionnée.</summary>
        void RefreshDetail()
        {
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == lastSelected) return;
            lastSelected = selected;
            int index = cards.FindIndex(c => c.gameObject == selected);
            if (index < 0) return;

            var c = list[index];
            bool isDone = Challenges.IsDone(c);
            title.text = c.displayName;
            title.color = isDone ? DoneColor : Color.white;
            goal.text = c.Goal;

            if (isDone) progress.text = $"<color=#{ColorUtility.ToHtmlStringRGB(DoneColor)}>COMPLETED!</color>";
            else if (c.HasProgress) progress.text = $"Best: {Mathf.Min(Challenges.Best(c), c.target)} / {c.target}";
            else progress.text = "<color=#FF6666>Not completed yet</color>";

            rewardIcon.sprite = c.reward != null ? c.reward.Icon : null;
            rewardIcon.enabled = rewardIcon.sprite != null;
            rewardIcon.color = isDone ? Color.white : LockedTint;
            if (c.reward != null)
            {
                rewardName.text = c.reward.displayName;
                rewardName.color = ShopItemData.TierColor(c.reward.tier);
                string kind = c.reward is WeaponData ? "weapon" : "item";
                rewardText.text = isDone
                    ? $"Unlocked: this {kind} can now appear in the shop.\n<color=#BBBBBB>{c.reward.Description}</color>"
                    : $"Unlocks this {kind} in the shop.\n<color=#BBBBBB>{c.reward.Description}</color>";
            }
            else rewardName.text = rewardText.text = "";
        }
    }
}
