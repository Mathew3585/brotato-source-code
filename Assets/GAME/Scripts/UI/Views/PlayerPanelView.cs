using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>Panneau d'un joueur dans le HUD (PV, XP, matériaux). Références à assigner dans la scène.</summary>
    public class PlayerPanelView : MonoBehaviour
    {
        public Image stripe;
        public TextMeshProUGUI title;
        public TextMeshProUGUI level;
        public RectTransform hpFill;
        public TextMeshProUGUI hpText;
        public RectTransform xpFill;
        public TextMeshProUGUI materials;
        public TextMeshProUGUI aim;
        [Tooltip("Caisses ramassées pendant la vague : icône à côté du panneau (côté intérieur de l'écran), cachée à 0")]
        public RectTransform crateRoot;
        public Image crateIcon;
        public TextMeshProUGUI crateCount;
    }
}
