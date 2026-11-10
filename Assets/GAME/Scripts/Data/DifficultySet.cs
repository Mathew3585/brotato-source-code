using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Niveaux de difficulté (« Danger », façon Brotato). Gagner une partie débloque le niveau suivant.
    /// L'asset vit dans Data/Resources pour être chargé de n'importe quelle scène.
    /// </summary>
    [CreateAssetMenu(menuName = "BrotatoLike/Difficultés", fileName = "DifficultySet")]
    public class DifficultySet : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            public string displayName = "Danger";
            [TextArea] public string description = "";
            public Color color = Color.white;
            [Tooltip("Multiplicateur de PV des ennemis (boss compris)")]
            public float enemyHealth = 1f;
            [Tooltip("Multiplicateur des dégâts des ennemis (contact et projectiles)")]
            public float enemyDamage = 1f;
            [Tooltip("Multiplicateur du nombre d'ennemis (taille des groupes et maximum en vie)")]
            public float enemyCount = 1f;
            [Tooltip("Multiplicateur de la chance de lâcher des matériaux")]
            public float materials = 1f;
            [Tooltip("Vagues (à partir de 1) où une élite apparaît au début de la vague")]
            public List<int> eliteWaves = new();
            [Tooltip("Deux boss à la dernière vague")]
            public bool doubleBoss;
        }

        public List<Level> levels = new();

        const string UnlockKey = "danger_unlocked";
        static DifficultySet instance;
        static readonly Level Default = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static DifficultySet Instance => instance != null ? instance : instance = Resources.Load<DifficultySet>("DifficultySet");

        public static int Count => Instance != null ? Instance.levels.Count : 1;

        /// <summary>Niveau de la partie en cours (Session.Difficulty).</summary>
        public static Level Current => Get(Session.Difficulty);

        public static Level Get(int index)
        {
            var set = Instance;
            if (set == null || set.levels.Count == 0) return Default;
            return set.levels[Mathf.Clamp(index, 0, set.levels.Count - 1)];
        }

        /// <summary>Plus haut niveau débloqué sur ce PC.</summary>
        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockKey, 0), 0, Count - 1);

        public static void Unlock(int index)
        {
            index = Mathf.Clamp(index, 0, Count - 1);
            if (index <= Unlocked) return;
            PlayerPrefs.SetInt(UnlockKey, index);
            PlayerPrefs.Save();
        }
    }
}
