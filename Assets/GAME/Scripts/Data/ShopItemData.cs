using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Tout ce qui peut s'acheter en boutique (armes et objets).</summary>
    public abstract class ShopItemData : ScriptableObject
    {
        public string displayName = "Objet";
        [Range(1, 4)] public int tier = 1;
        public int price = 20;

        public abstract Sprite Icon { get; }

        /// <summary>Description affichée en boutique (texte riche TMP).</summary>
        public abstract string Description { get; }

        public static Color TierColor(int tier) => tier switch
        {
            2 => new Color(0.35f, 0.65f, 1f),
            3 => new Color(0.75f, 0.45f, 1f),
            4 => new Color(1f, 0.35f, 0.35f),
            _ => new Color(0.85f, 0.85f, 0.85f),
        };

        public static string TierRoman(int tier) => tier switch
        {
            2 => "II",
            3 => "III",
            4 => "IV",
            _ => "I",
        };
    }
}
