using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Notification « Challenge complete! » en haut de l'écran de jeu (une à la fois, les suivantes attendent).
    /// À poser sur un objet toujours actif ; le bandeau (toast) est caché entre deux notifications.
    /// </summary>
    public class ChallengeToast : MonoBehaviour
    {
        const float ShowDuration = 3.5f;

        [SerializeField] RectTransform toast;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI text;

        readonly Queue<ChallengeSet.Challenge> queue = new();
        float timer;
        Vector2 shownPosition;

        void Awake()
        {
            shownPosition = toast.anchoredPosition;
            toast.gameObject.SetActive(false);
        }

        void OnEnable() => Challenges.Completed += Enqueue;
        void OnDisable() => Challenges.Completed -= Enqueue;

        void Enqueue(ChallengeSet.Challenge c) => queue.Enqueue(c);

        void Update()
        {
            if (timer > 0f)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f) Hide();
                return;
            }
            if (queue.Count > 0 && !toast.gameObject.activeSelf) Show(queue.Dequeue());
        }

        void Show(ChallengeSet.Challenge c)
        {
            icon.sprite = c.reward != null ? c.reward.Icon : null;
            icon.enabled = icon.sprite != null;
            string reward = c.reward != null ? $"\n<size=75%><color=#BBBBBB>Unlocked:</color> {c.reward.displayName}</size>" : "";
            text.text = $"<color=#7CFF7C>Challenge complete!</color> {c.displayName}{reward}";

            toast.gameObject.SetActive(true);
            Tween.StopAll(toast);
            toast.anchoredPosition = shownPosition + new Vector2(0f, 200f);
            Tween.UIAnchoredPosition(toast, shownPosition, 0.4f, Ease.OutBack, useUnscaledTime: true);
            AudioManager.Play(SoundId.UiReady);
            timer = ShowDuration;
        }

        void Hide()
        {
            Tween.StopAll(toast);
            Tween.UIAnchoredPosition(toast, shownPosition + new Vector2(0f, 200f), 0.3f, Ease.InBack, useUnscaledTime: true)
                .OnComplete(toast, t => t.gameObject.SetActive(false));
        }
    }
}
