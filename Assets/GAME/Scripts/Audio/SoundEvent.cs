using System;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Un son du jeu : un ou plusieurs clips (tirés au hasard), volume, variation de hauteur, anti-spam.</summary>
    [Serializable]
    public class SoundEvent
    {
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 0.5f;
        public float pitchMin = 0.95f;
        public float pitchMax = 1.05f;
        [Tooltip("Délai minimum entre deux lectures (évite la cacophonie quand 50 ennemis meurent d'un coup)")]
        public float minInterval = 0.03f;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                foreach (var c in clips)
                    if (c != null) return true;
                return false;
            }
        }
    }
}
