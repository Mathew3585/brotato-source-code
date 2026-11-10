using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    public enum ChallengeType
    {
        ReachWave,         // atteindre la vague N
        WinRun,            // gagner une partie
        WinOnDanger,       // gagner au Danger N ou plus
        WinWithCharacter,  // gagner avec un personnage précis
        KillEnemies,       // tuer N ennemis en une partie (un joueur)
        CollectMaterials,  // ramasser N matériaux en une partie (un joueur)
        ReachLevel,        // atteindre le niveau N
        OwnTier4Weapon,    // posséder une arme de tier IV
    }

    /// <summary>
    /// Défis : chacun débloque un objet ou une arme pour la boutique. Tant que le défi n'est pas réussi,
    /// sa récompense n'est jamais proposée. L'asset vit dans Data/Resources.
    /// </summary>
    [CreateAssetMenu(menuName = "BrotatoLike/Défis", fileName = "ChallengeSet")]
    public class ChallengeSet : ScriptableObject
    {
        [Serializable]
        public class Challenge
        {
            [Tooltip("Identifiant de sauvegarde : ne pas le changer après coup")]
            public string id;
            public string displayName;
            public ChallengeType type;
            [Tooltip("Valeur à atteindre (vague, niveau, nombre d'ennemis...)")]
            public int target = 1;
            [Tooltip("Pour WinWithCharacter")]
            public CharacterData character;
            [Tooltip("Objet ou arme débloqué (une arme débloque aussi ses tiers supérieurs)")]
            public ShopItemData reward;

            /// <summary>Texte de l'objectif, généré depuis le type et la valeur.</summary>
            public string Goal => type switch
            {
                ChallengeType.ReachWave => $"Reach wave {target}.",
                ChallengeType.WinRun => "Win a run.",
                ChallengeType.WinOnDanger => $"Win a run on Danger {target} or higher.",
                ChallengeType.WinWithCharacter => $"Win a run with {(character != null ? character.displayName : "?")}.",
                ChallengeType.KillEnemies => $"Kill {target} enemies in a single run.",
                ChallengeType.CollectMaterials => $"Collect {target} materials in a single run.",
                ChallengeType.ReachLevel => $"Reach level {target}.",
                ChallengeType.OwnTier4Weapon => "Own a tier IV weapon.",
                _ => "",
            };

            /// <summary>Défi avec une progression chiffrée à afficher (ex. 340 / 1000).</summary>
            public bool HasProgress => type is ChallengeType.ReachWave or ChallengeType.KillEnemies
                or ChallengeType.CollectMaterials or ChallengeType.ReachLevel;
        }

        public List<Challenge> challenges = new();

        static ChallengeSet instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static ChallengeSet Instance => instance != null ? instance : instance = Resources.Load<ChallengeSet>("ChallengeSet");
    }
}
