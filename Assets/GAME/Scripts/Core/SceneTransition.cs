using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Transition « iris » entre les scènes : un cercle se referme au centre de l'écran, la scène change
    /// derrière, puis le cercle se rouvre. L'image plein écran utilise le shader BrotatoLike/UI/IrisTransition
    /// (sans lui : simple fondu au noir).
    /// Prefab « SceneTransition » posé dans chaque scène ; le premier exemplaire est gardé entre les scènes,
    /// les suivants se détruisent. En ligne, le cercle se ferme aussi chez les clients quand l'hôte change de scène.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        [Tooltip("Image plein écran avec le matériau IrisTransition")]
        [SerializeField] RectTransform curtain;
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI label;

        [Header("Réglages")]
        [SerializeField] float coverTime = 0.45f;
        [SerializeField] float revealTime = 0.5f;
        [Tooltip("Attente une fois la nouvelle scène chargée (cache les à-coups du démarrage)")]
        [SerializeField] float revealDelay = 0.15f;
        [SerializeField] Ease coverEase = Ease.InQuad;
        [SerializeField] Ease revealEase = Ease.OutQuad;

        enum Phase { Idle, Covering, Covered, Revealing }

        static readonly int RadiusId = Shader.PropertyToID("_Radius");

        public static SceneTransition Instance { get; private set; }
        /// <summary>Une transition est en cours (évite de lancer deux chargements).</summary>
        public static bool Busy => Instance != null && Instance.phase != Phase.Idle;

        Phase phase;
        bool revealWhenCovered;
        Material material;
        Tween tween;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Copie du matériau : on anime son rayon sans toucher à l'asset
            var image = curtain.GetComponent<Image>();
            if (image != null && image.material != null && image.material.HasProperty(RadiusId))
            {
                material = new Material(image.material);
                image.material = material;
            }
            Hide();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            tween.Stop();
            if (material != null) Destroy(material);
        }

        // ---------------------------------------------------------------- API

        public static void LoadScene(string sceneName) => Cover(() => SceneManager.LoadScene(sceneName));

        public static void LoadScene(int buildIndex) => Cover(() => SceneManager.LoadScene(buildIndex));

        /// <summary>
        /// Ferme le cercle puis appelle <paramref name="onCovered"/> (qui change de scène).
        /// Le cercle se rouvre tout seul quand la nouvelle scène est chargée. Sans transition dans la scène : appel direct.
        /// </summary>
        public static void Cover(Action onCovered)
        {
            if (Instance == null)
            {
                onCovered?.Invoke();
                return;
            }
            Instance.StartCover(onCovered);
        }

        // ---------------------------------------------------------------- Animation

        void StartCover(Action onCovered)
        {
            switch (phase)
            {
                case Phase.Covering: return; // déjà en route : on ignore le double appui
                case Phase.Covered:
                    onCovered?.Invoke();
                    return;
            }

            phase = Phase.Covering;
            revealWhenCovered = false;
            group.blocksRaycasts = true;
            label.enabled = false;
            Animate(1f, 0f, coverTime, coverEase, () =>
            {
                phase = Phase.Covered;
                label.enabled = true;
                onCovered?.Invoke();
                if (revealWhenCovered) RevealSoon();
            });
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // En ligne (FishNet charge en mode additif), la scène peut arriver avant la fin de la fermeture : on rouvre juste après
            if (phase == Phase.Covering) revealWhenCovered = true;
            else if (phase == Phase.Covered) RevealSoon();
        }

        void RevealSoon() => Tween.Delay(revealDelay, Reveal, useUnscaledTime: true);

        void Reveal()
        {
            if (phase != Phase.Covered) return;
            phase = Phase.Revealing;
            label.enabled = false;
            Animate(0f, 1f, revealTime, revealEase, Hide);
        }

        /// <summary>Anime l'ouverture : 1 = ouvert, 0 = fermé.</summary>
        void Animate(float from, float to, float duration, Ease ease, Action done)
        {
            tween.Stop();
            tween = Tween.Custom(from, to, new TweenSettings(duration, ease, useUnscaledTime: true), SetOpening)
                .OnComplete(done);
        }

        void SetOpening(float opening)
        {
            if (material != null)
            {
                // Rayon du cercle (shader) ; l'image reste opaque autour
                material.SetFloat(RadiusId, opening);
                group.alpha = opening >= 1f ? 0f : 1f;
            }
            else group.alpha = 1f - opening; // pas de shader : fondu au noir
        }

        void Hide()
        {
            phase = Phase.Idle;
            group.blocksRaycasts = false;
            label.enabled = false;
            SetOpening(1f);
        }
    }
}
