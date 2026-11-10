using System;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Stats façon Brotato : des valeurs additives. Les stats en % sont des points de pourcentage.</summary>
    public enum StatType
    {
        MaxHealth,      // PV max (plat)
        HealthRegen,    // régénération (plat)
        Armor,          // réduction de dégâts
        Dodge,          // % d'esquive (max 60)
        MoveSpeed,      // % de vitesse
        Damage,         // % de dégâts
        AttackSpeed,    // % de vitesse d'attaque
        CritChance,     // % de critique
        Range,          // portée (plat, en unités)
        PickupRange,    // rayon de ramassage (plat, en unités)
        Luck,           // chance
        // Ajoutées à la fin pour garder les numéros des stats déjà enregistrées dans les assets et les sauvegardes
        MeleeDamage,    // dégâts plats ajoutés aux armes de mêlée (x la part de l'arme)
        RangedDamage,   // dégâts plats ajoutés aux armes à distance
        Engineering,    // dégâts plats ajoutés aux structures (tourelles des objets)
        LifeSteal,      // % de chance de regagner 1 PV à chaque coup (10 fois par seconde au plus)
        Harvesting,     // matériaux (et XP) gagnés à la fin de chaque vague
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public float value;

        public StatModifier(StatType stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }

    public class PlayerStats
    {
        public static readonly int Count = Enum.GetValues(typeof(StatType)).Length;

        readonly float[] values = new float[Count];

        public event Action Changed;

        public float this[StatType stat] => values[(int)stat];

        public float[] GetAll() => (float[])values.Clone();

        public void SetAll(float[] saved)
        {
            if (saved == null) return;
            for (int i = 0; i < values.Length && i < saved.Length; i++) values[i] = saved[i];
            Changed?.Invoke();
        }

        public void Add(StatType stat, float value)
        {
            values[(int)stat] += value;
            Changed?.Invoke();
        }

        public void Add(StatModifier[] modifiers)
        {
            if (modifiers == null) return;
            foreach (var m in modifiers) values[(int)m.stat] += m.value;
            Changed?.Invoke();
        }

        // --- Valeurs dérivées ---
        public float MoveSpeedMultiplier => Mathf.Max(0.2f, 1f + this[StatType.MoveSpeed] / 100f);
        public float DamageMultiplier => Mathf.Max(0.1f, 1f + this[StatType.Damage] / 100f);
        public float AttackSpeedMultiplier => Mathf.Max(0.2f, 1f + this[StatType.AttackSpeed] / 100f);
        public float CritChance01 => Mathf.Clamp01(this[StatType.CritChance] / 100f);
        public float Dodge01 => Mathf.Clamp(this[StatType.Dodge], 0f, 60f) / 100f;
        public float LifeSteal01 => Mathf.Clamp01(this[StatType.LifeSteal] / 100f);

        /// <summary>Stat de dégâts plats d'un type d'arme : mêlée, distance, ou ingénierie pour les structures.</summary>
        public static StatType FlatDamageStat(WeaponKind kind) => kind switch
        {
            WeaponKind.Thrust or WeaponKind.Sweep => StatType.MeleeDamage,
            WeaponKind.Turret => StatType.Engineering,
            _ => StatType.RangedDamage,
        };

        public float FlatDamage(WeaponKind kind) => this[FlatDamageStat(kind)];

        /// <summary>Multiplicateur de dégâts subis. Armure positive = moins de dégâts.</summary>
        public float ArmorMultiplier
        {
            get
            {
                float armor = this[StatType.Armor];
                return armor >= 0 ? 1f / (1f + armor / 15f) : 1f - armor / 15f;
            }
        }
    }
}
