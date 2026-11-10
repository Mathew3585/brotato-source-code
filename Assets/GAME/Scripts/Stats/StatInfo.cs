namespace BrotatoLike
{
    /// <summary>Noms affichés et formatage des stats.</summary>
    public static class StatInfo
    {
        public static string Name(StatType stat) => stat switch
        {
            StatType.MaxHealth => "Max HP",
            StatType.HealthRegen => "HP Regen",
            StatType.Armor => "Armor",
            StatType.Dodge => "Dodge",
            StatType.MoveSpeed => "Speed",
            StatType.Damage => "Damage",
            StatType.AttackSpeed => "Attack Speed",
            StatType.CritChance => "Crit Chance",
            StatType.Range => "Range",
            StatType.PickupRange => "Pickup Range",
            StatType.Luck => "Luck",
            StatType.MeleeDamage => "Melee Dmg",
            StatType.RangedDamage => "Ranged Dmg",
            StatType.Engineering => "Engineering",
            StatType.LifeSteal => "Life Steal",
            StatType.Harvesting => "Harvesting",
            _ => stat.ToString(),
        };

        public static bool IsPercent(StatType stat) =>
            stat is StatType.Dodge or StatType.MoveSpeed or StatType.Damage or StatType.AttackSpeed or StatType.CritChance or StatType.LifeSteal;

        /// <summary>Courte explication (page « Stats » de la boutique).</summary>
        public static string Description(StatType stat) => stat switch
        {
            StatType.MaxHealth => "your health",
            StatType.HealthRegen => "HP recovered over time",
            StatType.LifeSteal => "chance to heal 1 per hit",
            StatType.Armor => "reduces damage taken",
            StatType.Dodge => "chance to avoid a hit",
            StatType.MoveSpeed => "movement speed",
            StatType.Luck => "better shop and upgrades",
            StatType.Harvesting => "materials each wave",
            StatType.Damage => "multiplies all damage",
            StatType.MeleeDamage => "added to melee hits",
            StatType.RangedDamage => "added to ranged hits",
            StatType.Engineering => "added to turret hits",
            StatType.AttackSpeed => "weapons attack faster",
            StatType.CritChance => "chance of a critical hit",
            StatType.Range => "attack reach",
            StatType.PickupRange => "materials pulled in",
            _ => "",
        };

        public static string FormatValue(StatType stat, float value) =>
            value.ToString("0.#") + (IsPercent(stat) ? "%" : "");

        /// <summary>"+5% Dégâts" en vert, "-2 Armure" en rouge.</summary>
        public static string FormatModifier(StatType stat, float value)
        {
            string color = value >= 0 ? "#7CFF7C" : "#FF6B6B";
            string sign = value >= 0 ? "+" : "";
            return $"<color={color}>{sign}{FormatValue(stat, value)}</color> {Name(stat)}";
        }
    }
}
