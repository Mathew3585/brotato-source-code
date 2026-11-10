using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Colonne de boutique d'un joueur (caisses, améliorations, offres, inventaire).
    /// Toute la mise en page se fait dans la scène ; ce composant ne fait que référencer les éléments.
    /// </summary>
    public class ShopColumnView : MonoBehaviour
    {
        [Serializable]
        public class OfferView
        {
            public Button card;
            public Image stripe;
            public Image icon;
            public TextMeshProUGUI name;
            public TextMeshProUGUI desc;
            public TextMeshProUGUI price;
            public TextMeshProUGUI sold;
            public Button lockButton;
            public Image lockIcon;
        }

        [Serializable]
        public class UpgradeView
        {
            public Button button;
            public Image stripe;
            public TextMeshProUGUI tier;
            public TextMeshProUGUI text;
        }

        [Header("En-tête")]
        public Image colorBar;
        public TextMeshProUGUI title;
        public TextMeshProUGUI materials;
        public TextMeshProUGUI message;

        [Header("Caisse")]
        public GameObject crateGroup;
        public TextMeshProUGUI crateTitle;
        public Image crateStripe;
        public Image crateIcon;
        public TextMeshProUGUI crateName;
        public TextMeshProUGUI crateDesc;
        public Button takeButton;
        public Button recycleButton;
        public TextMeshProUGUI recycleLabel;

        [Header("Améliorations de niveau")]
        public GameObject levelUpGroup;
        public TextMeshProUGUI levelUpTitle;
        public UpgradeView[] upgrades;

        [Header("Boutique")]
        public GameObject shopGroup;
        public OfferView[] offers;
        public Button rerollButton;
        public TextMeshProUGUI rerollLabel;
        public Button readyButton;

        [Header("Inventaire")]
        public TextMeshProUGUI weaponsLabel;
        public Button[] weaponSlots;
        public Image[] weaponIcons;
        public TextMeshProUGUI[] weaponTiers;
        public Button sellButton;
        public TextMeshProUGUI sellLabel;
        public Button mergeButton;
        [Tooltip("Coop : donner l'arme ou l'objet sélectionné à un coéquipier")]
        public Button giveButton;
        public TextMeshProUGUI giveLabel;
        [Tooltip("Coop (3-4 joueurs) : un bouton par coéquipier possible, à la place de Sell / Merge / Give")]
        public Button[] giveTargets;
        public TextMeshProUGUI[] giveTargetLabels;
        public TextMeshProUGUI itemsLabel;
        [Tooltip("Icônes d'objets (affichées dans l'ordre, les autres sont masquées)")]
        public Image[] itemIcons;
        [Tooltip("Compteur sous chaque icône (\"x6\" quand l'objet est possédé plusieurs fois)")]
        public TextMeshProUGUI[] itemCounts;
        public TextMeshProUGUI statsLeft;
        public TextMeshProUGUI statsRight;

        [Header("Page Stats (Q / E, LB / RB)")]
        [Tooltip("Page qui remplace les offres / améliorations : toutes les stats avec une courte explication")]
        public GameObject statsPage;
        public TextMeshProUGUI statsPageText;
        [Tooltip("Onglets en bas de la colonne : « Q <  SHOP  STATS  > E »")]
        public TextMeshProUGUI tabBar;

        [Header("Infobulle (armes / objets possédés)")]
        public GameObject tooltipRoot;
        public TextMeshProUGUI tooltipTitle;
        public TextMeshProUGUI tooltipText;

        [Header("Prêt")]
        public GameObject readyOverlay;
        public Image readyOverlayImage;
        public TextMeshProUGUI readyText;
        public Button unreadyButton;

        public RectTransform Rect => (RectTransform)transform;
    }
}
