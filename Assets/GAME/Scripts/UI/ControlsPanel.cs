using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>Écran des contrôles (clavier / manette), ouvert depuis les options. L'UI est dans la scène.</summary>
    public class ControlsPanel : MonoBehaviour
    {
        [SerializeField] RectTransform panel;
        [SerializeField] Button backButton;

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>Frame de fermeture (la touche qui ferme ne doit pas aussi fermer les options).</summary>
        public int LastClosedFrame { get; private set; } = -1;

        EventSystem eventSystem;
        Action onClosed;
        int openedFrame;
        bool initialized;

        public void Open(EventSystem es, Action closed)
        {
            if (!initialized)
            {
                initialized = true;
                backButton.onClick.AddListener(Close);
                backButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            eventSystem = es;
            onClosed = closed;
            openedFrame = Time.frameCount;
            gameObject.SetActive(true);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(backButton.gameObject);
            panel.localScale = Vector3.one * 0.9f;
            Tween.Scale(panel, Vector3.one, 0.25f, Ease.OutBack, useUnscaledTime: true);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            AudioManager.Play(SoundId.UiBack);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            gameObject.SetActive(false);
            LastClosedFrame = Time.frameCount;
            onClosed?.Invoke();
        }

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            var kb = Keyboard.current;
            bool back = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame);
            foreach (var pad in Gamepad.all) back |= pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            if (back) Close();
        }
    }
}
