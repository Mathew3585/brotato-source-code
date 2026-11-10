using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>Case d'un joueur dans l'écran de sélection des personnages.</summary>
    public class LobbySlotView : MonoBehaviour
    {
        public Image colorBar;
        public TextMeshProUGUI header;

        [Header("Libre")]
        public GameObject emptyGroup;
        public TextMeshProUGUI emptyText;

        [Header("Occupée")]
        public GameObject filledGroup;
        public Image portrait;
        public Button leftArrow;
        public Button rightArrow;
        public TextMeshProUGUI characterName;
        public TextMeshProUGUI stats;
        public Image weaponIcon;
        public TextMeshProUGUI weaponName;
        public TextMeshProUGUI status;
        public TextMeshProUGUI hint;

        public RectTransform Rect => (RectTransform)transform;
    }
}
