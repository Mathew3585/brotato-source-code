using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Améliorations proposées à chaque montée de niveau (choisir 1 parmi 4).</summary>
    public static class UpgradeTable
    {
        public struct Choice
        {
            public StatType stat;
            public float value;
            public int tier;
        }

        // Valeur au tier I et gain par tier en plus (1 = proportionnel : II = x2, III = x3, IV = x4).
        // Régénération : +2 / +3 / +4 / +5 (comme Brotato), sinon un tier IV donnait +8 d'un coup.
        static readonly (StatType stat, float value, float growth)[] Table =
        {
            (StatType.MaxHealth, 3f, 1f),
            (StatType.HealthRegen, 2f, 0.5f),
            (StatType.Armor, 1f, 1f),
            (StatType.Dodge, 3f, 1f),
            (StatType.MoveSpeed, 3f, 1f),
            (StatType.Damage, 5f, 1f),
            (StatType.AttackSpeed, 5f, 1f),
            (StatType.CritChance, 3f, 1f),
            (StatType.Range, 0.5f, 1f),
            (StatType.PickupRange, 0.5f, 1f),
            (StatType.Luck, 5f, 1f),
            (StatType.MeleeDamage, 1f, 1f),
            (StatType.RangedDamage, 1f, 1f),
            (StatType.Engineering, 1f, 1f),
            (StatType.LifeSteal, 1f, 1f),
            (StatType.Harvesting, 5f, 0.4f),
        };

        public static Choice[] Roll(int count, int waveIndex, float luck)
        {
            // Mélange de Fisher-Yates pour tirer des stats toutes différentes
            var indices = new int[Table.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var choices = new Choice[Mathf.Min(count, Table.Length)];
            for (int i = 0; i < choices.Length; i++)
            {
                var (stat, value, growth) = Table[indices[i]];
                int tier = ShopCatalog.RollTier(waveIndex + 2, luck);
                choices[i] = new Choice { stat = stat, value = value * (1f + (tier - 1) * growth), tier = tier };
            }
            return choices;
        }
    }
}
