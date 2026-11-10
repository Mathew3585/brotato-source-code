using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>Écran de fin de partie (victoire / game over) avec le récap de chaque joueur.</summary>
    public class EndScreenView : MonoBehaviour
    {
        [Serializable]
        public class PlayerColumn
        {
            public GameObject root;
            public Image colorBar;
            public TextMeshProUGUI title;
            public TextMeshProUGUI stats;
            [Header("Carte du joueur (en haut de la colonne)")]
            public Image portrait;
            public Image weaponIcon;
            public TextMeshProUGUI weaponName;
            [Tooltip("Cadre doré : joueur qui a infligé le plus de dégâts")]
            public GameObject goldFrame;
            [Tooltip("Bandeau « TOP DAMAGE » sur le cadre doré (seulement à plusieurs)")]
            public GameObject badge;
        }

        public RectTransform panel;
        public TextMeshProUGUI title;
        public TextMeshProUGUI subtitle;
        public PlayerColumn[] players;
        public TextMeshProUGUI hint;
    }
}
