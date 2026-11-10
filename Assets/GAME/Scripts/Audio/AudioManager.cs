using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Joue les bruitages (pool de sources) et la musique de la scène.
    /// À placer dans chaque scène avec la configuration audio ; appel : AudioManager.Play(SoundId.Shoot).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int VoiceCount = 24;

        public static AudioManager Instance { get; private set; }

        [Tooltip("Configuration générale (pointe vers les sons Combat, Interface, Boutique, Partie, Ramassage)")]
        [SerializeField] AudioConfig config;
        [Tooltip("Musiques, jouées l'une après l'autre dans un ordre aléatoire (même liste dans toutes les scènes : modifier le prefab Audio). " +
                 "Une nouvelle piste est tirée à chaque changement de scène.")]
        [SerializeField] AudioClip[] playlist;
        [Tooltip("Ancienne musique unique, jouée en boucle si la playlist est vide")]
        [SerializeField] AudioClip music;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;
        [Tooltip("Montée du volume (s) au début de chaque piste")]
        [SerializeField] float musicFadeIn = 1.5f;

        readonly AudioSource[] voices = new AudioSource[VoiceCount];
        readonly Dictionary<SoundId, float> lastPlayed = new();
        AudioSource musicSource;
        int nextVoice;
        readonly List<int> trackOrder = new();
        int trackPos;
        float trackFade;
        /// <summary>Dernière piste jouée (gardée d'une scène à l'autre pour ne pas la rejouer tout de suite).</summary>
        static int lastTrack = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            lastTrack = -1;
        }

        void Awake()
        {
            Instance = this;
            // Sécurité : si la configuration n'est pas assignée, on prend celle du dossier Resources
            if (config == null) config = Resources.Load<AudioConfig>("AudioConfig");
            if (config == null) Debug.LogWarning("[BrotatoLike] Aucune configuration audio : le jeu sera muet.");
            for (int i = 0; i < VoiceCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                voices[i] = src;
            }

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            if (HasPlaylist) PlayNextTrack();
            else if (music != null)
            {
                musicSource.loop = true;
                musicSource.clip = music;
                musicSource.Play();
            }
        }

        bool HasPlaylist => playlist != null && System.Array.Exists(playlist, c => c != null);

        void Update()
        {
            // Piste finie : on enchaîne (pas pendant que la fenêtre n'a pas le focus, le son est alors en pause)
            if (HasPlaylist && !musicSource.isPlaying && Application.isFocused) PlayNextTrack();
            trackFade = musicFadeIn > 0f ? Mathf.Min(1f, trackFade + Time.unscaledDeltaTime / musicFadeIn) : 1f;
            musicSource.volume = musicVolume * GameSettings.MusicVolume * trackFade;
        }

        /// <summary>Piste suivante de la playlist, mélangée (toutes les pistes passent avant qu'une revienne).</summary>
        void PlayNextTrack()
        {
            if (trackPos >= trackOrder.Count) ShuffleTracks();
            if (trackOrder.Count == 0) return;
            int index = trackOrder[trackPos++];
            lastTrack = index;
            musicSource.loop = false;
            musicSource.clip = playlist[index];
            musicSource.Play();
            trackFade = 0f;
        }

        void ShuffleTracks()
        {
            trackOrder.Clear();
            trackPos = 0;
            for (int i = 0; i < playlist.Length; i++)
                if (playlist[i] != null) trackOrder.Add(i);
            for (int i = trackOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (trackOrder[i], trackOrder[j]) = (trackOrder[j], trackOrder[i]);
            }
            // Pas deux fois la même piste d'affilée (fin de liste ou changement de scène)
            if (trackOrder.Count > 1 && trackOrder[0] == lastTrack)
                (trackOrder[0], trackOrder[^1]) = (trackOrder[^1], trackOrder[0]);
        }

        /// <summary>Joue un bruitage (sans effet s'il n'y a pas d'AudioManager ou pas de clip).</summary>
        public static void Play(SoundId id, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (Instance != null) Instance.PlayInternal(id, volumeScale, pitchScale);
        }

        void PlayInternal(SoundId id, float volumeScale, float pitchScale)
        {
            if (config == null) return;
            var entry = config.Get(id);
            if (entry == null || !entry.HasClips) return;

            // Anti-spam (temps réel : fonctionne aussi en pause et pendant les micro-pauses)
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < entry.minInterval) return;
            lastPlayed[id] = now;

            var clip = entry.clips[Random.Range(0, entry.clips.Length)];
            if (clip == null) return;

            var src = voices[nextVoice];
            nextVoice = (nextVoice + 1) % VoiceCount;
            src.pitch = Random.Range(entry.pitchMin, entry.pitchMax) * pitchScale;
            src.PlayOneShot(clip, entry.volume * volumeScale * GameSettings.SfxVolume);
        }
    }
}
