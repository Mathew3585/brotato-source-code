using System;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Panneau d'options (volumes, limite de FPS, plein écran, secousses). Utilisé depuis le menu principal et le menu pause.
    /// L'UI est dans la scène ; ce script branche les contrôles sur <see cref="GameSettings"/>.
    /// </summary>
    public class OptionsMenu : MonoBehaviour
    {
        /// <summary>Vrai tant qu'un panneau d'options est ouvert (bloque la touche pause).</summary>
        public static bool AnyOpen { get; private set; }
        /// <summary>Frame de la dernière fermeture (la touche qui ferme ne doit pas aussi enlever la pause).</summary>
        public static int LastClosedFrame { get; private set; } = -1;

        [SerializeField] RectTransform panel;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TextMeshProUGUI sfxValue;
        [SerializeField] Slider musicSlider;
        [SerializeField] TextMeshProUGUI musicValue;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Toggle shakeToggle;
        [Tooltip("Limite de FPS : index dans GameSettings.FpsOptions (optionnel)")]
        [SerializeField] Slider fpsSlider;
        [SerializeField] TextMeshProUGUI fpsValue;
        [SerializeField] Button backButton;
        [Tooltip("Bouton qui ouvre l'écran des contrôles (optionnel)")]
        [SerializeField] Button controlsButton;
        [SerializeField] ControlsPanel controls;

        EventSystem eventSystem;
        GameObject previousPlayerRoot;
        Action onClosed;
        bool initialized;
        int openedFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AnyOpen = false;

        void Init()
        {
            if (initialized) return;
            initialized = true;
            sfxSlider.onValueChanged.AddListener(v =>
            {
                GameSettings.SetSfxVolume(v);
                RefreshLabels();
                AudioManager.Play(SoundId.UiSelect);
            });
            musicSlider.onValueChanged.AddListener(v =>
            {
                GameSettings.SetMusicVolume(v);
                RefreshLabels();
            });
            fullscreenToggle.onValueChanged.AddListener(v =>
            {
                GameSettings.SetFullscreen(v);
                AudioManager.Play(SoundId.UiConfirm);
            });
            shakeToggle.onValueChanged.AddListener(v =>
            {
                GameSettings.SetScreenShake(v);
                AudioManager.Play(SoundId.UiConfirm);
                if (v && CameraController.Instance) CameraController.Instance.Shake(0.3f);
            });
            if (fpsSlider != null)
            {
                fpsSlider.wholeNumbers = true;
                fpsSlider.minValue = 0;
                fpsSlider.maxValue = GameSettings.FpsOptions.Length - 1;
                fpsSlider.onValueChanged.AddListener(v =>
                {
                    GameSettings.SetFpsLimit(GameSettings.FpsOptions[Mathf.RoundToInt(v)]);
                    RefreshLabels();
                    AudioManager.Play(SoundId.UiSelect);
                });
            }
            backButton.onClick.AddListener(Close);
            var lastRow = new List<Selectable>();
            if (controlsButton != null && controls != null)
            {
                controlsButton.onClick.AddListener(OpenControls);
                lastRow.Add(controlsButton);
                controls.gameObject.SetActive(false);
            }
            lastRow.Add(backButton);
            var rows = new List<List<Selectable>> { new() { sfxSlider }, new() { musicSlider } };
            if (fpsSlider != null) rows.Add(new() { fpsSlider });
            rows.Add(new() { fullscreenToggle });
            rows.Add(new() { shakeToggle });
            rows.Add(lastRow);
            UIFactory.LinkNavigation(rows);
            // Gauche/droite règlent les sliders (pas de voisin horizontal)
            foreach (var s in new Selectable[] { sfxSlider, musicSlider, fpsSlider })
            {
                if (s == null) continue;
                var nav = s.navigation;
                nav.selectOnLeft = nav.selectOnRight = null;
                s.navigation = nav;
            }
        }

        /// <param name="es">Système d'événements de celui qui ouvre le menu (manette / clavier du joueur).</param>
        /// <param name="closed">Appelé à la fermeture (pour revenir au menu précédent).</param>
        public void Open(EventSystem es, Action closed)
        {
            Init();
            eventSystem = es;
            onClosed = closed;
            openedFrame = Time.frameCount;

            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            shakeToggle.SetIsOnWithoutNotify(GameSettings.ScreenShake);
            if (fpsSlider != null) fpsSlider.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(GameSettings.FpsOptions, GameSettings.FpsLimit)));
            RefreshLabels();

            gameObject.SetActive(true);
            AnyOpen = true;

            if (eventSystem is MultiplayerEventSystem mes)
            {
                previousPlayerRoot = mes.playerRoot;
                mes.playerRoot = gameObject;
            }
            if (eventSystem != null) eventSystem.SetSelectedGameObject(sfxSlider.gameObject);

            panel.localScale = Vector3.one * 0.9f;
            Tween.Scale(panel, Vector3.one, 0.25f, Ease.OutBack, useUnscaledTime: true);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            GameSettings.Save();
            AudioManager.Play(SoundId.UiBack);
            if (eventSystem is MultiplayerEventSystem mes) mes.playerRoot = previousPlayerRoot;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            gameObject.SetActive(false);
            AnyOpen = false;
            LastClosedFrame = Time.frameCount;
            onClosed?.Invoke();
        }

        void OpenControls()
        {
            AudioManager.Play(SoundId.UiConfirm);
            panel.gameObject.SetActive(false);
            controls.Open(eventSystem, () =>
            {
                panel.gameObject.SetActive(true);
                if (eventSystem != null) eventSystem.SetSelectedGameObject(controlsButton.gameObject);
            });
        }

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            // L'écran des contrôles gère lui-même le retour
            if (controls != null && (controls.IsOpen || controls.LastClosedFrame == Time.frameCount)) return;
            // Retour : Échap / B
            bool back = Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame);
            foreach (var pad in Gamepad.all) back |= pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            if (back) Close();
        }

        void OnDisable() => AnyOpen = false;

        void RefreshLabels()
        {
            sfxValue.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
            musicValue.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
            if (fpsValue != null) fpsValue.text = GameSettings.FpsLabel(GameSettings.FpsLimit);
        }
    }
}
