using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Tout ce que la boutique peut proposer + règles de prix et de rareté.</summary>
    [CreateAssetMenu(menuName = "BrotatoLike/Catalogue de boutique", fileName = "ShopCatalog")]
    public class ShopCatalog : ScriptableObject
    {
        public List<WeaponData> weapons = new();
        public List<ItemData> items = new();

        [Header("Boutique")]
        public Sprite lockIcon;
        public int offerCount = 4;
        [Range(0f, 1f)] public float weaponChance = 0.35f;
        [Tooltip("Hausse des prix par vague (0.1 = +10% par vague)")]
        public float priceIncreasePerWave = 0.1f;
        [Range(0f, 1f)] public float sellRatio = 0.3f;

        [Header("Armes proposées selon le stuff")]
        [Tooltip("Chance minimale d'une arme du style minoritaire (ex. : stuff 100% distance → 15% de corps à corps)")]
        [Range(0f, 0.5f)] public float minorStyleChance = 0.15f;
        [Tooltip("Chance qu'une arme proposée soit une arme déjà possédée (même tier, pour pouvoir la fusionner)")]
        [Range(0f, 1f)] public float ownedWeaponChance = 0.2f;

        public int PriceAtWave(ShopItemData item, int waveIndex) =>
            Mathf.Max(1, Mathf.RoundToInt(item.price * (1f + priceIncreasePerWave * waveIndex)));

        public int SellValue(ShopItemData item, int waveIndex) =>
            Mathf.Max(1, Mathf.RoundToInt(PriceAtWave(item, waveIndex) * sellRatio));

        public int RerollPrice(int waveIndex, int rerollCount) =>
            (waveIndex + 1) + rerollCount * Mathf.Max(1, (waveIndex + 1) / 2);

        /// <summary>Tire un tier selon l'avancée de la partie et la chance (stat Luck).</summary>
        public static int RollTier(int waveIndex, float luck)
        {
            float luckMult = Mathf.Max(0f, 1f + luck / 100f);
            float t4 = Mathf.Clamp((waveIndex - 7) * 0.023f, 0f, 0.08f) * luckMult;
            float t3 = Mathf.Clamp((waveIndex - 3) * 0.02f, 0f, 0.25f) * luckMult;
            float t2 = Mathf.Clamp((waveIndex - 1) * 0.06f, 0f, 0.6f) * luckMult;

            float r = Random.value;
            if (r < t4) return 4;
            if (r < t4 + t3) return 3;
            if (r < t4 + t3 + t2) return 2;
            return 1;
        }

        /// <param name="canOwnMore">Filtre : faux pour un objet que le joueur a déjà au maximum (objets uniques).</param>
        /// <param name="owned">Armes du joueur : oriente les armes proposées vers son style (distance / corps à corps).</param>
        public ShopItemData RollOffer(int waveIndex, float luck, System.Func<ItemData, bool> canOwnMore = null,
            IReadOnlyList<WeaponData> owned = null)
        {
            int tier = RollTier(waveIndex, luck);
            bool weapon = Random.value < weaponChance;

            if (weapon)
            {
                // Parfois une arme déjà possédée, au même tier : de quoi la fusionner
                if (owned != null && owned.Count > 0 && Random.value < ownedWeaponChance)
                {
                    var same = owned[Random.Range(0, owned.Count)];
                    if (same != null && same.nextTier != null && weapons.Contains(same)) return same;
                }

                // Sinon, le style dominant du stuff a plus de chances
                bool wantMelee = Random.value < MeleeChance(owned);
                for (int t = tier; t >= 1; t--)
                {
                    var pick = Pick(weapons, t, w => w.IsMelee == wantMelee) ?? Pick(weapons, t, null);
                    if (pick != null) return pick;
                }
            }

            // Si rien n'existe à ce tier, on descend d'un tier
            for (int t = tier; t >= 1; t--)
            {
                ShopItemData pick = Pick(items, t, canOwnMore);
                if (pick == null) pick = Pick(weapons, t, null);
                if (pick != null) return pick;
            }
            return null;
        }

        /// <summary>
        /// Chance de proposer une arme de corps à corps : suit la part de corps à corps du stuff,
        /// sans descendre sous minorStyleChance ni monter au-dessus de 1 - minorStyleChance.
        /// </summary>
        float MeleeChance(IReadOnlyList<WeaponData> owned)
        {
            int melee = 0, total = 0;
            if (owned != null)
                foreach (var w in owned)
                {
                    if (w == null || w.kind == WeaponKind.Turret) continue;
                    total++;
                    if (w.IsMelee) melee++;
                }
            if (total == 0) return 0.5f;
            return Mathf.Clamp((float)melee / total, minorStyleChance, 1f - minorStyleChance);
        }

        /// <summary>Contenu d'une caisse : toujours un objet, avec un bonus de rareté.</summary>
        public ItemData RollCrateItem(int waveIndex, float luck, System.Func<ItemData, bool> canOwnMore = null)
        {
            int tier = RollTier(waveIndex + 2, luck);
            for (int t = tier; t >= 1; t--)
            {
                var pick = Pick(items, t, canOwnMore);
                if (pick != null) return pick;
            }
            return null;
        }

        static readonly List<ShopItemData> buffer = new();

        static T Pick<T>(List<T> list, int tier, System.Func<T, bool> filter) where T : ShopItemData
        {
            buffer.Clear();
            foreach (var item in list)
                if (item != null && item.tier == tier && (filter == null || filter(item)) && !Challenges.IsLocked(item)) buffer.Add(item);
            return buffer.Count == 0 ? null : (T)buffer[Random.Range(0, buffer.Count)];
        }
    }
}
