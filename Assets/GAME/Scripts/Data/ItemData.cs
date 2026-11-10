using System.Text;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Objet passif : modifie les stats du joueur qui l'achète.</summary>
    [CreateAssetMenu(menuName = "BrotatoLike/Objet", fileName = "Item_")]
    public class ItemData : ShopItemData
    {
        public Sprite icon;
        public StatModifier[] modifiers;
        [Tooltip("Nombre maximum par joueur (0 = illimité). 1 = objet unique : il ne sort plus en boutique une fois acheté.")]
        public int maxPerPlayer = 0;
        [Tooltip("Structure : tourelle posée automatiquement au début de chaque vague (une par exemplaire possédé)")]
        public WeaponData deployTurret;

        public override Sprite Icon => icon;

        public override string Description
        {
            get
            {
                var sb = new StringBuilder();
                if (deployTurret != null)
                {
                    var t = deployTurret;
                    sb.Append($"Structure: placed at the start of every wave\nDamage: <color=#FFD966>{t.damage:0.#}</color> <color=#AAAAAA>(+{t.statScaling * 100f:0}% Engineering)</color>   Rate: {1f / t.turretFireInterval:0.##}/s   Range: {t.range:0.#}");
                }
                if (modifiers != null)
                    foreach (var m in modifiers)
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(StatInfo.FormatModifier(m.stat, m.value));
                    }
                if (maxPerPlayer == 1) sb.Append(sb.Length > 0 ? "\n<color=#AAAAAA>Unique</color>" : "<color=#AAAAAA>Unique</color>");
                return sb.ToString();
            }
        }
    }
}
