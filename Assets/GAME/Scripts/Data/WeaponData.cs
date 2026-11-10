using UnityEngine;

namespace BrotatoLike
{
    public enum WeaponKind
    {
        Ranged, // tire des projectiles
        Thrust, // corps à corps : coup d'estoc en ligne droite
        Sweep,  // corps à corps : balayage en arc de cercle
        Turret, // pose une tourelle qui tire toute seule
    }

    [CreateAssetMenu(menuName = "BrotatoLike/Arme", fileName = "Weapon_")]
    public class WeaponData : ShopItemData
    {
        [Tooltip("Version de tier supérieur obtenue en fusionnant 2 armes identiques")]
        public WeaponData nextTier;
        public WeaponKind kind = WeaponKind.Ranged;
        [Tooltip("Part des dégâts plats du joueur ajoutée à chaque coup (Melee Dmg pour la mêlée, Ranged Dmg à distance, Engineering pour les tourelles). 1 = 100 %")]
        public float statScaling = 1f;

        [Header("Visuel")]
        public Sprite sprite;
        public float spriteScale = 1f;
        [Tooltip("Rotation à appliquer pour que le sprite pointe vers la droite (ex : -45 pour un sprite en diagonale)")]
        public float spriteAngleOffset = 0f;
        public Sprite projectileSprite;
        public float projectileScale = 1f;
        [Tooltip("Rotation du sprite de projectile (-90 = sprite qui pointe vers le haut, 0 = vers la droite)")]
        public float projectileAngleOffset = -90f;

        [Header("Attaque")]
        public float damage = 5f;
        [Tooltip("Secondes entre deux attaques")]
        public float cooldown = 1f;
        [Tooltip("Portée de tir, ou allonge pour le corps à corps")]
        public float range = 7f;
        public float knockback = 0.3f;
        [Tooltip("Chance de critique de base de l'arme (en %)")]
        public float critChance = 3f;
        public float critMultiplier = 2f;

        [Header("Distance")]
        public float projectileSpeed = 15f;
        public int projectilesPerShot = 1;
        [Tooltip("Angle total de dispersion en degrés quand il y a plusieurs projectiles")]
        public float spreadAngle = 0f;
        [Tooltip("Imprécision aléatoire en degrés")]
        public float inaccuracy = 0f;
        [Tooltip("Nombre d'ennemis supplémentaires traversés")]
        public int pierce = 0;
        [Tooltip("Rayon d'explosion à l'impact (0 = pas d'explosion)")]
        public float explosionRadius = 0f;
        [Tooltip("Nombre de tirs par rafale (1 = tir simple)")]
        public int burstCount = 1;
        public float burstInterval = 0.08f;

        [Header("Effets")]
        [Tooltip("Ralentissement appliqué aux ennemis touchés (0.4 = -40% de vitesse)")]
        [Range(0f, 1f)] public float slowAmount = 0f;
        public float slowDuration = 1.5f;

        [Header("Tourelle")]
        public Sprite turretSprite;
        public float turretScale = 1f;
        [Tooltip("Durée de vie de la tourelle posée")]
        public float turretDuration = 7f;
        [Tooltip("Secondes entre deux tirs de la tourelle")]
        public float turretFireInterval = 0.6f;

        [Header("Bonus passifs (tant que l'arme est équipée)")]
        public StatModifier[] passiveModifiers;

        [Header("Corps à corps")]
        [Tooltip("Durée d'un coup (aller-retour)")]
        public float attackDuration = 0.25f;

        public bool IsMelee => kind is WeaponKind.Thrust or WeaponKind.Sweep;
        public override Sprite Icon => sprite;

        public override string Description
        {
            get
            {
                string kindLabel = kind switch
                {
                    WeaponKind.Thrust => "Melee (thrust)",
                    WeaponKind.Sweep => "Melee (sweep)",
                    WeaponKind.Turret => $"Turret ({turretDuration:0}s)",
                    _ => explosionRadius > 0f ? "Explosive" : "Ranged",
                };
                int shotsPerAttack = Mathf.Max(1, projectilesPerShot) * Mathf.Max(1, burstCount);
                string shots = shotsPerAttack > 1 ? $"{damage:0.#} x{shotsPerAttack}" : $"{damage:0.#}";
                float rate = kind == WeaponKind.Turret ? 1f / turretFireInterval : 1f / cooldown;
                string text = $"{kindLabel}   Damage: <color=#FFD966>{shots}</color>   Rate: {rate:0.##}/s\n" +
                              $"Range: {range:0.#}   Crit: {critChance:0}% (x{critMultiplier:0.#})";
                if (pierce >= 99) text += "   Pierces all";
                else if (pierce > 0) text += $"   Pierces {pierce}";
                if (explosionRadius > 0f) text += $"   Area: {explosionRadius:0.#}";
                if (knockback < 0f) text += "   Pulls";
                if (slowAmount > 0f) text += $"   Slows {slowAmount * 100f:0}%";
                if (statScaling > 0f) text += $"   <color=#AAAAAA>+{statScaling * 100f:0}% {StatInfo.Name(PlayerStats.FlatDamageStat(kind))}</color>";
                if (passiveModifiers != null)
                    foreach (var m in passiveModifiers) text += "   " + StatInfo.FormatModifier(m.stat, m.value);
                return text;
            }
        }
    }
}
