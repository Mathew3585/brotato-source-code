using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Passe d'équilibrage : courbe des 20 vagues, armes trop fortes / trop faibles, personnages distincts.
    /// ATTENTION : écrase les valeurs concernées (menu "BrotatoLike > Appliquer l'équilibrage").
    /// </summary>
    public static class BalanceBuilder
    {
        const string DataDir = "Assets/GAME/Data";

        [MenuItem("BrotatoLike/Appliquer l'équilibrage (écrase les réglages)", priority = 30)]
        public static void ApplyFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Équilibrage",
                    "Les vagues, certaines armes et les personnages vont être réécrits avec les valeurs d'équilibrage par défaut.",
                    "Appliquer", "Annuler")) return;
            Apply();
        }

        public static void Apply()
        {
            BalanceWaves();
            BalanceEnemies();
            BalanceWeapons();
            BalanceCharacters();
            AssetDatabase.SaveAssets();
            Debug.Log("[BrotatoLike] Équilibrage appliqué.");
        }

        /// <summary>Uniquement les vagues et les ennemis (armes et personnages inchangés).</summary>
        public static void ApplyWavesAndEnemies()
        {
            BalanceWaves();
            BalanceEnemies();
            AssetDatabase.SaveAssets();
            Debug.Log("[BrotatoLike] Équilibrage des vagues et des ennemis appliqué.");
        }

        // ---------------------------------------------------------------- Vagues

        static void BalanceWaves()
        {
            var waveSet = AssetDatabase.LoadAssetAtPath<WaveSet>(DataDir + "/WaveSet.asset");
            if (waveSet == null) return;
            int count = waveSet.waves.Count;
            for (int i = 0; i < count; i++)
            {
                var w = waveSet.waves[i];
                float t = i / (count - 1f);
                bool boss = w.boss != null;

                // Montée régulière (sans pic vers les vagues 10-11), les dernières vagues restent denses
                w.duration = boss ? 60f : Mathf.Min(20f + 5f * i, 60f);
                w.spawnInterval = boss ? 1.4f : Mathf.Lerp(1.5f, 0.45f, t);
                w.groupSizeMin = 2 + i / 5;
                w.groupSizeMax = 3 + i / 3;
                w.maxAlive = boss ? 50 : Mathf.Min(20 + 12 * i, 260);
                w.enemyHealthMultiplier = 1f + 0.035f * i;
                // Tireurs en même temps (solo) : 2 à l'arrivée des mages (vague 6), 4 à la vague 10, 9 à la vague 19
                w.maxShooters = i < 5 ? 0 : boss ? 3 : 2 + (i - 5) / 2;
            }
            // Coop : +50 % d'ennemis, +30 % de PV et +50 % de tireurs par joueur en plus
            waveSet.extraEnemiesPerPlayer = 0.5f;
            waveSet.extraHealthPerPlayer = 0.3f;
            waveSet.extraShootersPerPlayer = 0.5f;

            // Tireurs plus rares et plus tardifs
            foreach (var entry in waveSet.enemies)
            {
                if (entry.enemy == null) continue;
                switch (entry.enemy.displayName)
                {
                    case "Dark Mage": entry.weight = 0.25f; entry.fromWave = 6; break;
                    case "Cannon": entry.weight = 0.08f; entry.fromWave = 8; break;
                    case "Firehead": entry.weight = 0.2f; entry.fromWave = 10; break;
                }
            }
            EditorUtility.SetDirty(waveSet);
        }

        // ---------------------------------------------------------------- Ennemis

        /// <summary>
        /// Dégâts par vague revus à la baisse (contact et projectiles) et tireurs plus lents :
        /// à la vague 10, une balle fait ~3 dégâts au lieu de 6.
        /// </summary>
        static readonly Dictionary<string, float> ContactDamagePerWave = new()
        {
            ["Zombie"] = 0.45f, ["Runner Zombie"] = 0.35f, ["Skeleton"] = 0.55f, ["Pirate Skeleton"] = 0.5f,
            ["Tombstone"] = 0.7f, ["Crow"] = 0.3f, ["Ghoul"] = 0.55f, ["Dark Mage"] = 0.3f, ["Firehead"] = 0.3f,
            ["Beetle"] = 0.5f, ["Larva"] = 0.3f, ["Giant Beetle"] = 0.55f, ["Cannon"] = 0.3f, ["Troll"] = 0.8f,
        };

        static void BalanceEnemies()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyData", new[] { DataDir + "/Enemies" }))
            {
                var e = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (e == null) continue;
                if (ContactDamagePerWave.TryGetValue(e.displayName, out float dmg)) e.damagePerWave = dmg;
                if (!e.isBoss) e.projectileDamagePerWave = 0.25f;

                switch (e.displayName)
                {
                    case "Dark Mage":
                        e.fireInterval = 3f;
                        e.projectileSpeed = 5f;
                        break;
                    case "Firehead":
                        e.fireInterval = 2.6f;
                        e.projectilesPerShot = 3;
                        e.projectileSpread = 30f;
                        break;
                    case "Cannon":
                        e.fireInterval = 4f;
                        e.projectilesPerShot = 6;
                        e.projectileSpeed = 3.5f;
                        break;
                    case "Dragon":
                        e.fireInterval = 2.8f;
                        break;
                }
                EditorUtility.SetDirty(e);
            }
        }

        // ---------------------------------------------------------------- Armes

        /// <summary>Corrections des armes (tier I) : DPS visé ~6-10 en cible unique au tier I.</summary>
        static readonly Dictionary<string, System.Action<WeaponData>> WeaponTweaks = new()
        {
            ["Sword"] = w => w.damage = 6f,                 // balayage + 10% crit : trop fort à 7
            ["Sniper"] = w => w.cooldown = 2.4f,           // perce 4 + gros crits
            ["SMG"] = w => w.damage = 1.8f,       // 16 DPS -> 12
            ["Shotgun"] = w => w.damage = 3f,        // 5 plombs
            ["Watering Can"] = w => w.damage = 1.2f,           // très rapide + ralentit
            ["Blunderbuss"] = w => w.damage = 2.5f,           // 4 plombs
            ["Axe"] = w => w.cooldown = 1.4f,
        };

        static void BalanceWeapons()
        {
            var all = AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir + "/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null).ToList();

            foreach (var baseWeapon in all.Where(w => w.tier == 1))
            {
                if (WeaponTweaks.TryGetValue(baseWeapon.displayName, out var tweak))
                {
                    tweak(baseWeapon);
                    EditorUtility.SetDirty(baseWeapon);
                }

                // Les tiers II à IV sont recalculés depuis le tier I (sprites et noms conservés)
                var current = baseWeapon.nextTier;
                while (current != null)
                {
                    ScaleTier(baseWeapon, current);
                    EditorUtility.SetDirty(current);
                    current = current.nextTier;
                }
            }
        }

        static void ScaleTier(WeaponData b, WeaponData w)
        {
            float k = w.tier - 1;
            w.price = Mathf.RoundToInt(b.price * (1f + 0.9f * k));
            w.damage = b.damage * (1f + 0.6f * k);
            w.cooldown = b.cooldown * (1f - 0.08f * k);
            w.critChance = b.critChance + 2f * k;
            w.knockback = b.knockback * (1f + 0.1f * k);
            // Le reste suit le tier I
            w.kind = b.kind;
            w.range = b.range;
            w.critMultiplier = b.critMultiplier;
            w.projectileSpeed = b.projectileSpeed;
            w.projectilesPerShot = b.projectilesPerShot;
            w.spreadAngle = b.spreadAngle;
            w.inaccuracy = b.inaccuracy;
            w.pierce = b.pierce;
            w.explosionRadius = b.explosionRadius;
            w.burstCount = b.burstCount;
            w.burstInterval = b.burstInterval;
            w.slowAmount = b.slowAmount;
            w.slowDuration = b.slowDuration;
            w.turretSprite = b.turretSprite;
            w.turretScale = b.turretScale;
            w.turretDuration = b.turretDuration;
            w.turretFireInterval = b.turretFireInterval * (1f - 0.08f * k);
            w.attackDuration = b.attackDuration;
            w.passiveModifiers = b.passiveModifiers?.Select(m => new StatModifier(m.stat, m.value * (1f + 0.5f * k))).ToArray();
        }

        // ---------------------------------------------------------------- Personnages

        static void BalanceCharacters()
        {
            var weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir + "/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null && w.tier == 1).GroupBy(w => w.displayName).ToDictionary(g => g.Key, g => g.First());

            Character("Pierre", "Versatile", "Rifle", weapons,
                M(StatType.MaxHealth, 12), M(StatType.Damage, 5), M(StatType.Armor, 1), M(StatType.PickupRange, 2.5f));
            Character("Lucie", "Sharpshooter", "Musket", weapons,
                M(StatType.MaxHealth, 8), M(StatType.CritChance, 15), M(StatType.Range, 2), M(StatType.AttackSpeed, -10), M(StatType.PickupRange, 2.5f));
            Character("Hugo", "Brute (melee)", "Shovel", weapons,
                M(StatType.MaxHealth, 18), M(StatType.Armor, 3), M(StatType.Damage, 15), M(StatType.MoveSpeed, -10), M(StatType.Range, -1), M(StatType.PickupRange, 2.5f));
            Character("Margot", "Lucky and fast", "Pistol", weapons,
                M(StatType.MaxHealth, 9), M(StatType.MoveSpeed, 15), M(StatType.Luck, 25), M(StatType.PickupRange, 4.5f), M(StatType.Damage, -10));
        }

        static void Character(string name, string tagline, string weaponName, Dictionary<string, WeaponData> weapons, params StatModifier[] stats)
        {
            var c = AssetDatabase.LoadAssetAtPath<CharacterData>($"{DataDir}/Characters/Character_{name}.asset");
            if (c == null) return;
            c.tagline = tagline;
            c.baseStats = stats;
            if (weapons.TryGetValue(weaponName, out var weapon)) c.startingWeapon = weapon;
            EditorUtility.SetDirty(c);
        }

        static StatModifier M(StatType stat, float value) => new(stat, value);
    }
}
