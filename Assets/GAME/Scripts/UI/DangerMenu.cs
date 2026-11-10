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
    /// Choix de la difficulté (« Danger 0 à 5 », façon Brotato), après le lobby.
    /// Les niveaux pas encore débloqués sont visibles mais ne peuvent pas être choisis.
    /// L'UI est dans la scène MainMenu ; ce script la remplit.
    /// </summary>
    public class DangerMenu : MonoBehaviour
    {
        [SerializeField] RectTransform panel;
        [SerializeField] Button[] buttons;
        [SerializeField] TextMeshProUGUI[] labels;
        [SerializeField] TextMeshProUGUI levelName;
        [SerializeField] TextMeshProUGUI description;
        [SerializeField] TextMeshProUGUI footer;

        Action<int> onChosen;
        Action onCancelled;
        EventSystem eventSystem;
        int openedFrame;
        bool initialized;
        GameObject lastSelected;

        public bool IsOpen => gameObject.activeSelf;

        void Init()
        {
            if (initialized) return;
            initialized = true;

            // Plus de niveaux dans DifficultySet que de boutons dans la scène : on duplique le premier
            // (le conteneur a un HorizontalLayoutGroup qui les aligne)
            if (buttons.Length > 0 && DifficultySet.Count > buttons.Length)
            {
                var allButtons = new List<Button>(buttons);
                var allLabels = new List<TextMeshProUGUI>(labels);
                while (allButtons.Count < DifficultySet.Count)
                {
                    var copy = Instantiate(buttons[0], buttons[0].transform.parent);
                    copy.name = "Danger " + allButtons.Count;
                    allButtons.Add(copy);
                    allLabels.Add(copy.GetComponentInChildren<TextMeshProUGUI>(true));
                }
                buttons = allButtons.ToArray();
                labels = allLabels.ToArray();
            }

            var row = new List<Selectable>();
            for (int i = 0; i < buttons.Length; i++)
            {
                int level = i;
                buttons[i].onClick.AddListener(() => Choose(level));
                row.Add(buttons[i]);
            }
            UIFactory.LinkNavigation(new List<List<Selectable>> { row });
        }

        /// <param name="es">Système d'événements qui pilote le menu.</param>
        /// <param name="chosen">Appelé avec le niveau choisi.</param>
        /// <param name="cancelled">Retour (Échap / B).</param>
        public void Open(EventSystem es, Action<int> chosen, Action cancelled)
        {
            Init();
            eventSystem = es;
            onChosen = chosen;
            onCancelled = cancelled;
            openedFrame = Time.frameCount;

            int unlocked = DifficultySet.Unlocked;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool exists = i < DifficultySet.Count;
                buttons[i].gameObject.SetActive(exists);
                if (!exists) continue;
                var level = DifficultySet.Get(i);
                bool open = i <= unlocked;
                UIFactory.SetAccent(buttons[i], level.color);
                labels[i].text = open ? i.ToString() : "<size=60%>LOCKED</size>";
                labels[i].color = open ? level.color : new Color(1f, 1f, 1f, 0.35f);
            }
            if (footer != null) footer.text = "Enter / A: start   -   Esc / B: back";

            gameObject.SetActive(true);
            lastSelected = null;
            var first = buttons[Mathf.Clamp(Mathf.Min(Session.Difficulty, unlocked), 0, buttons.Length - 1)];
            if (eventSystem != null) eventSystem.SetSelectedGameObject(first.gameObject);
            RefreshDescription();

            panel.localScale = Vector3.one * 0.9f;
            Tween.Scale(panel, Vector3.one, 0.25f, Ease.OutBack, useUnscaledTime: true);
        }

        void Close()
        {
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            gameObject.SetActive(false);
        }

        void Choose(int level)
        {
            // La touche qui a ouvert le menu ne doit pas aussi valider
            if (Time.frameCount == openedFrame) return;
            if (level > DifficultySet.Unlocked)
            {
                AudioManager.Play(SoundId.BuyFail);
                var t = buttons[level].transform;
                Tween.StopAll(t);
                Tween.ShakeLocalPosition(t, new Vector3(12f, 0f, 0f), 0.3f, useUnscaledTime: true);
                return;
            }
            Session.Difficulty = level;
            Close();
            onChosen?.Invoke(level);
        }

        void Update()
        {
            RefreshDescription();
            if (Time.frameCount == openedFrame) return;

            var kb = Keyboard.current;
            bool back = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame);
            foreach (var pad in Gamepad.all) back |= pad.buttonEast.wasPressedThisFrame;
            if (!back) return;
            AudioManager.Play(SoundId.UiBack);
            Close();
            onCancelled?.Invoke();
        }

        /// <summary>Nom et effets du niveau sélectionné.</summary>
        void RefreshDescription()
        {
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == lastSelected) return;
            lastSelected = selected;
            int index = -1;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].gameObject == selected) index = i;
            if (index < 0) return;

            var level = DifficultySet.Get(index);
            levelName.text = level.displayName;
            levelName.color = level.color;
            string text = level.description + Effects(level);
            description.text = index <= DifficultySet.Unlocked
                ? text
                : $"{text}\n<color=#FF6666>Locked: win a run on {DifficultySet.Get(index - 1).displayName} to unlock.</color>";
        }

        /// <summary>Effets chiffrés, calculés depuis les données (toujours à jour avec l'équilibrage).</summary>
        static string Effects(DifficultySet.Level level)
        {
            var parts = new List<string>();
            void Percent(string label, float multiplier)
            {
                int p = Mathf.RoundToInt((multiplier - 1f) * 100f);
                if (p != 0) parts.Add($"{label} {(p > 0 ? "+" : "")}{p}%");
            }
            Percent("Enemy health", level.enemyHealth);
            Percent("Enemy damage", level.enemyDamage);
            Percent("Enemies", level.enemyCount);
            Percent("Materials", level.materials);
            if (level.eliteWaves.Count > 0) parts.Add($"Elites on waves {string.Join(", ", level.eliteWaves)}");
            if (level.doubleBoss) parts.Add("Two bosses on the final wave");
            return parts.Count == 0 ? "" : "\n<color=#BBBBBB><size=85%>" + string.Join("   -   ", parts) + "</size></color>";
        }
    }
}
