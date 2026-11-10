using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Contenu "variété de combat" : armes de mêlée, nouvelles armes à distance, nouveaux ennemis et boss.
    /// Appelé par PrototypeBuilder. Comme le reste, n'écrase jamais un asset existant.
    /// </summary>
    static class ContentBuilder
    {
        const string DataDir = "Assets/GAME/Data";

        // --- Brackeys 2D Mega Pack
        const string AxeGuid = "8708d80070522a043a1ab6a2b2775382";
        const string ClubGuid = "06140cc248b05d2488fbb6493ce5fd11";
        const string SpearGuid = "bf28d1ab5e5c42e4aa2c9b729c09fce3";
        const string SwordGuid = "b2f3327f47464544293b6b9018e1296e";
        const string StaffGuid = "3aa1cb26afcdc244bb3a95c193575ecb";
        const string ExplosionGuid = "947c0cbf176869a45b7fd547776a2b0c";
        const string FireballGuid = "5ff9fd1dfa3921644a587a08e82b9e1c";
        const string BeetleGuid = "a50747b7090647445a1aa71f3aefd771";
        const string GiantBeetleGuid = "dfe2da12be5f5994286f281b2cde6656";
        const string FireheadGuid = "d0f0bb7b0aae47846a42f0be578f602f";
        const string GothicGuid = "6978f24c2379e0d48ad8c2501e94d313";
        const string CrowGuid = "7ed863ccbdc96f0438cc78a3c684e3e0";
        const string CanonGuid = "8bfadec067237c04ab16144f6847e733";
        const string DarkWizardGuid = "31b17986fa5b13f4f9b7403589287a19";
        const string TrollGuid = "07f23995d5720bd4d9cd266799cef6fb";
        const string DragonGuid = "4574f971d70a83c41ade6083ada1035a";

        // --- Pixel Guns 2D (dessinés en x5 : on règle les PPU pour une taille cohérente)
        const string HandgunGuid = "24db8b1e40da92c4199a81a20bf8ecfe";
        const string SniperGuid = "6d0bcb00a713ad6418700cb4cd697ba0";
        const string HeavyGunGuid = "7b2dcaffa07fceb4d81774f15bc6ebe3";
        const string BigGunGuid = "e69cacc20f61d5a43b0e615a75e49756";
        const string Bullet6Guid = "963c023c5edf48848b7840d2f00a8687";
        const string Bullet8Guid = "8d3f2d4e6f2505a4cb546267d4fb5f52";
        const string Bullet10Guid = "6c521da4bed23b64e8ff4ebef23f6fd0";

        // Variantes Pixel Guns (sprites par tier)
        const string Handgun2Guid = "1616e4908b7c0284997f328329c92b1b";
        const string Handgun3Guid = "f478eaaeec67b324abfcb43ecf15f8b2";
        const string Handgun4Guid = "a8eff05b4f942064cb43f8371d6e1677";
        const string Handgun5Guid = "ace37081810b42140afa3160c0974eb2";
        const string Handgun6Guid = "920f1d9cf86c0744cb30411c9542cf42";
        const string Handgun7Guid = "df92bddbca9c21846bf482cf05a16c8c";
        const string Handgun8Guid = "67fc38989fbb0f544a3f80c33c188e72";
        const string Handgun9Guid = "8647396bfa183d044a370094732b67c4";
        const string Gun1Guid = "d64e92c5a43e36a4b80566aa5665abe4";
        const string Gun2Guid = "a5291ea130e20174691c3ab7775b30d2";
        const string Gun4Guid = "6daa3f6a352f79145aefc2d43a4c5f9b";
        const string Gun5Guid = "a32a5df5b09d8e24ca17add7a066b323";
        const string Shotgun1Guid = "91c2cdc50a9254441a87d44de8453103";
        const string Shotgun3Guid = "3f83467495e7ee742acbd52a1a15b77e";
        const string Shotgun5Guid = "9da0f9b6ace913a49a8e1e9da7998539";
        const string ShotgunProGuid = "ac6b3eac93bfa194eb9702c3085d5751";
        const string Sniper3Guid = "07b3fa3e64b5b164a8049701b4aaa45d";
        const string Sniper5Guid = "3968ab2d4d4ff71488b1101719cfc2ae";
        const string Sniper6Guid = "d10069ca0c864384195c8bf71b9c1e7d";
        const string SilverHeavy1Guid = "cf42ca6a09b52bf4a8cfe63bf170871f";
        const string GoldHeavy1Guid = "972e541adc259a640a0be9b08b193922";
        const string GoldHeavy5Guid = "e8503f4422301c44b9ef661964cee2ba";

        // Brackeys : armes spéciales
        const string HarpoonGuid = "3f40ae8170066bb4d8280a58328eaa8f";
        const string TurretGuid = "f4c66fedc21c063429688fdaa8a62d6d";
        const string WateringCanGuid = "8dfb7eb4494cbe14eabf0fda5056d27d";
        const string FishingRodGuid = "1959adfdc5c338840858eec9307f0184";
        const string PickaxeGuid = "45cfe68fb47a8514f96477d0fb432286";
        const string GrungyBulletGuid = "61f1ea0354dcb6b478b4e36c6e19e6e4";
        const string WaterDropGuid = "2092e1ae9910a3743a653ebf2e627c0c";

        /// <summary>Sprites par tier (I à IV) : l'arme change d'apparence quand on la fusionne.</summary>
        static readonly (string key, string[] guids)[] TierSprites =
        {
            ("Pistolet", new[] { HandgunGuid, Handgun7Guid, Handgun8Guid, Handgun6Guid }),
            ("Revolver", new[] { Handgun3Guid, Handgun4Guid, Handgun9Guid, Handgun5Guid }),
            ("Mitrailleuse", new[] { HeavyGunGuid, SilverHeavy1Guid, GoldHeavy1Guid, GoldHeavy5Guid }),
            ("Sniper", new[] { SniperGuid, Sniper3Guid, Sniper5Guid, Sniper6Guid }),
            ("FusilAssaut", new[] { Gun4Guid, Gun5Guid, Gun2Guid, Gun1Guid }),
            ("Pompe", new[] { Shotgun1Guid, Shotgun3Guid, Shotgun5Guid, ShotgunProGuid }),
        };

        static readonly Vector2 HandlePivot = new(0.15f, 0.15f); // outils dessinés en diagonale, manche en bas à gauche

        /// <summary>
        /// Objet "Turret" : structure posée automatiquement au début de chaque vague
        /// (utilise les stats de l'arme tourelle, qui n'est plus vendue comme arme).
        /// </summary>
        public static ItemData EnsureTurretItem()
        {
            var turretWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataDir}/Weapons/Weapon_Tourelle.asset");
            if (turretWeapon == null) return null;
            return PrototypeBuilder.LoadOrCreate<ItemData>($"{DataDir}/Items/Item_Turret.asset", item =>
            {
                item.displayName = "Turret";
                item.icon = Sprites(TurretGuid)[0];
                item.tier = 2;
                item.price = 35;
                item.modifiers = new StatModifier[0];
                item.deployTurret = turretWeapon;
            });
        }

        /// <summary>Objets puissants achetables une seule fois (n'apparaissent plus en boutique une fois possédés).</summary>
        public static void ApplyUniqueItems()
        {
            foreach (var name in new[] { "The Ring", "Crown", "Magic Egg", "World Ender" })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:ItemData", new[] { DataDir + "/Items" }))
                {
                    var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (item == null || item.displayName != name) continue;
                    item.maxPerPlayer = 1;
                    EditorUtility.SetDirty(item);
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Réglages d'import : pixels nets + taille cohérente avec les fermiers (~18 px = 1 unité).</summary>
        public static void ConfigureImports()
        {
            foreach (var g in new[] { AxeGuid, ClubGuid, SpearGuid, StaffGuid }) Configure(g, 18, HandlePivot);
            Configure(SwordGuid, 18, new Vector2(0.85f, 0.5f));
            Configure(ExplosionGuid, 16);
            Configure(FireballGuid, 28);
            Configure(BeetleGuid, 50);
            Configure(GiantBeetleGuid, 60);
            Configure(FireheadGuid, 14);
            Configure(GothicGuid, 14);
            foreach (var g in new[] { CrowGuid, DarkWizardGuid, TrollGuid, DragonGuid }) Configure(g, 16);
            Configure(CanonGuid, 18);
            Configure(HandgunGuid, 170);
            Configure(SniperGuid, 230);
            Configure(HeavyGunGuid, 200);
            Configure(BigGunGuid, 300);
            foreach (var g in new[] { Bullet6Guid, Bullet8Guid, Bullet10Guid }) Configure(g, 100);

            // Variantes Pixel Guns : PPU choisi pour une longueur cohérente à l'écran
            foreach (var g in new[] { Handgun2Guid, Handgun3Guid, Handgun4Guid, Handgun5Guid, Handgun6Guid, Handgun7Guid, Handgun8Guid }) Configure(g, 170);
            Configure(Handgun9Guid, 300);
            foreach (var g in new[] { Gun1Guid, Gun2Guid, Gun4Guid, Gun5Guid }) Configure(g, 265);
            foreach (var g in new[] { Shotgun1Guid, Shotgun3Guid, Shotgun5Guid }) Configure(g, 245);
            Configure(ShotgunProGuid, 185);
            foreach (var g in new[] { Sniper3Guid, Sniper5Guid, Sniper6Guid }) Configure(g, 230);
            foreach (var g in new[] { SilverHeavy1Guid, GoldHeavy1Guid, GoldHeavy5Guid }) Configure(g, 200);

            // Brackeys : armes spéciales
            Configure(HarpoonGuid, 10);
            Configure(TurretGuid, 90, new Vector2(0.5f, 0.25f));
            Configure(WateringCanGuid, 18);
            Configure(FishingRodGuid, 18, HandlePivot);
            Configure(PickaxeGuid, 18, HandlePivot);
            Configure(GrungyBulletGuid, 12);
            Configure(WaterDropGuid, 40);
        }

        /// <summary>
        /// Donne à chaque tier son propre sprite. On ne remplace que les sprites encore identiques
        /// au tier I (si tu as changé un sprite à la main, il est conservé).
        /// </summary>
        public static void ApplyTierSprites()
        {
            foreach (var (key, guids) in TierSprites)
            {
                var baseWeapon = LoadWeaponAsset(key, 1);
                if (baseWeapon == null) continue;
                for (int t = 2; t <= 4; t++)
                {
                    var w = LoadWeaponAsset(key, t);
                    if (w == null || w.sprite != baseWeapon.sprite) continue;
                    w.sprite = Sprites(guids[t - 1])[0];
                    EditorUtility.SetDirty(w);
                }
            }
        }

        static WeaponData LoadWeaponAsset(string key, int tier)
        {
            // Le tier I a le nom affiché comme nom de fichier, les tiers suivants la clé + _T2.._T4
            if (tier > 1) return AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataDir}/Weapons/Weapon_{key}_T{tier}.asset");
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir + "/Weapons" }))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null && w.tier == 1 && w.nextTier != null && w.nextTier.name == $"Weapon_{key}_T2") return w;
            }
            return null;
        }

        static void Configure(string guid, float ppu, Vector2? pivot = null)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            bool changed = false;
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, ppu)) { importer.spritePixelsPerUnit = ppu; changed = true; }

            if (pivot.HasValue && importer.spriteImportMode == SpriteImportMode.Single)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteAlignment != (int)SpriteAlignment.Custom || settings.spritePivot != pivot.Value)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = pivot.Value;
                    importer.SetTextureSettings(settings);
                    changed = true;
                }
            }

            if (changed) importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- Armes

        public static void AddWeapons(Dictionary<string, WeaponData> weapons, Dictionary<string, Sprite> props)
        {
            var fireball = Sprites(FireballGuid)[0];

            weapons["Pelle"] = Weapon("Pelle", w =>
            {
                Melee(w, WeaponKind.Thrust, props["Weapon 0"], 0f, dmg: 8, cd: 0.9f, range: 1.6f, kb: 1f, price: 20);
                w.spriteScale = 0.9f;
            });
            weapons["Fourche"] = Weapon("Fourche", w =>
            {
                Melee(w, WeaponKind.Thrust, props["Weapon 1"], 0f, dmg: 6, cd: 0.7f, range: 2f, kb: 0.5f, price: 22);
                w.spriteScale = 0.9f;
            });
            weapons["Gourdin"] = Weapon("Gourdin", w =>
                Melee(w, WeaponKind.Thrust, Sprites(ClubGuid)[0], -45f, dmg: 9, cd: 1f, range: 1.5f, kb: 1.6f, price: 18));
            weapons["Lance"] = Weapon("Lance", w =>
                Melee(w, WeaponKind.Thrust, Sprites(SpearGuid)[0], -45f, dmg: 10, cd: 1.1f, range: 2.6f, kb: 0.6f, price: 28));
            weapons["Hache"] = Weapon("Hache", w =>
            {
                Melee(w, WeaponKind.Sweep, Sprites(AxeGuid)[0], -45f, dmg: 12, cd: 1.3f, range: 1.6f, kb: 0.8f, price: 30);
                w.attackDuration = 0.35f;
            });
            weapons["Epee"] = Weapon("Épée", w =>
            {
                Melee(w, WeaponKind.Sweep, Sprites(SwordGuid)[0], 180f, dmg: 7, cd: 0.65f, range: 1.4f, kb: 0.4f, price: 26);
                w.critChance = 10f;
                w.attackDuration = 0.3f;
            });

            weapons["Pistolet"] = Weapon("Pistolet", w =>
            {
                Ranged(w, Sprites(HandgunGuid)[0], Sprites(Bullet8Guid)[0], dmg: 3, cd: 0.4f, range: 6f, speed: 18f, price: 15);
            });
            weapons["Sniper"] = Weapon("Sniper", w =>
            {
                Ranged(w, Sprites(SniperGuid)[0], Sprites(Bullet6Guid)[0], dmg: 22, cd: 2.2f, range: 13f, speed: 30f, price: 40);
                w.pierce = 4;
                w.critChance = 15f;
                w.critMultiplier = 2.5f;
                w.knockback = 0.8f;
            });
            weapons["Mitrailleuse"] = Weapon("Mitrailleuse", w =>
            {
                Ranged(w, Sprites(HeavyGunGuid)[0], Sprites(Bullet8Guid)[0], dmg: 2.5f, cd: 0.15f, range: 6.5f, speed: 18f, price: 45);
                w.inaccuracy = 8f;
                w.knockback = 0.1f;
            });
            weapons["LanceRoquettes"] = Weapon("Lance-roquettes", w =>
            {
                Ranged(w, Sprites(BigGunGuid)[0], Sprites(Bullet10Guid)[0], dmg: 15, cd: 2f, range: 9f, speed: 10f, price: 45);
                w.explosionRadius = 1.6f;
                w.knockback = 1f;
            });
            weapons["FusilAssaut"] = Weapon("Fusil d'assaut", w =>
            {
                Ranged(w, Sprites(Gun4Guid)[0], Sprites(Bullet8Guid)[0], dmg: 3, cd: 0.9f, range: 7f, speed: 20f, price: 30);
                w.burstCount = 3;
                w.burstInterval = 0.08f;
                w.inaccuracy = 3f;
                w.knockback = 0.15f;
            });
            weapons["Pompe"] = Weapon("Fusil à pompe", w =>
            {
                Ranged(w, Sprites(Shotgun1Guid)[0], Sprites(Bullet8Guid)[0], dmg: 3.5f, cd: 1.2f, range: 5f, speed: 16f, price: 32);
                w.projectilesPerShot = 5;
                w.spreadAngle = 40f;
                w.inaccuracy = 4f;
                w.knockback = 0.9f;
            });
            weapons["Revolver"] = Weapon("Revolver", w =>
            {
                Ranged(w, Sprites(Handgun3Guid)[0], Sprites(Bullet6Guid)[0], dmg: 12, cd: 1.1f, range: 8f, speed: 24f, price: 28);
                w.critChance = 15f;
                w.critMultiplier = 2.5f;
                w.knockback = 0.6f;
            });
            weapons["Harpon"] = Weapon("Harpon", w =>
            {
                var harpoon = Sprites(HarpoonGuid)[0];
                Ranged(w, harpoon, harpoon, dmg: 9, cd: 1.6f, range: 9f, speed: 14f, price: 34);
                w.spriteAngleOffset = 90f; // dessiné pointe vers le bas
                w.projectileAngleOffset = 90f;
                w.pierce = 99;
                w.knockback = -0.5f; // attire les ennemis vers le joueur
            });
            weapons["Tourelle"] = Weapon("Tourelle", w =>
            {
                var turret = Sprites(TurretGuid)[0];
                Ranged(w, turret, Sprites(GrungyBulletGuid)[0], dmg: 4, cd: 9f, range: 7f, speed: 16f, price: 35);
                w.kind = WeaponKind.Turret;
                w.spriteScale = 0.5f;
                w.projectileAngleOffset = -90f;
                w.projectileScale = 1.5f;
                w.turretSprite = turret;
                w.turretDuration = 7f;
                w.turretFireInterval = 0.6f;
                w.knockback = 0.2f;
            });
            weapons["Arrosoir"] = Weapon("Arrosoir", w =>
            {
                Ranged(w, Sprites(WateringCanGuid)[0], Sprites(WaterDropGuid)[0], dmg: 1.5f, cd: 0.12f, range: 3.2f, speed: 9f, price: 25);
                w.spriteAngleOffset = 180f; // bec verseur à gauche
                w.projectileAngleOffset = -90f;
                w.projectileScale = 0.6f;
                w.inaccuracy = 15f;
                w.knockback = 0.05f;
                w.slowAmount = 0.4f;
                w.slowDuration = 1.5f;
            });
            weapons["CannePeche"] = Weapon("Canne à pêche", w =>
            {
                Melee(w, WeaponKind.Thrust, Sprites(FishingRodGuid)[0], -45f, dmg: 5, cd: 1f, range: 3.2f, kb: -0.4f, price: 22);
                w.passiveModifiers = new[] { new StatModifier(StatType.PickupRange, 1.5f) };
            });
            weapons["Pioche"] = Weapon("Pioche", w =>
                Melee(w, WeaponKind.Sweep, Sprites(PickaxeGuid)[0], -45f, dmg: 9, cd: 1f, range: 1.4f, kb: 0.6f, price: 22));

            weapons["Baton"] = Weapon("Bâton magique", w =>
            {
                Ranged(w, Sprites(StaffGuid)[0], fireball, dmg: 6, cd: 1f, range: 7f, speed: 9f, price: 32);
                w.spriteAngleOffset = -45f;
                w.projectileAngleOffset = -90f;
                w.explosionRadius = 1f;
                w.knockback = 0.4f;
            });
        }

        static WeaponData Weapon(string displayName, Action<WeaponData> init)
        {
            string file = new string(displayName.Where(char.IsLetterOrDigit).ToArray());
            return PrototypeBuilder.LoadOrCreate<WeaponData>($"{DataDir}/Weapons/Weapon_{file}.asset", w =>
            {
                w.displayName = displayName;
                init(w);
            });
        }

        static void Melee(WeaponData w, WeaponKind kind, Sprite sprite, float angleOffset, float dmg, float cd, float range, float kb, int price)
        {
            w.kind = kind;
            w.sprite = sprite;
            w.spriteAngleOffset = angleOffset;
            w.damage = dmg;
            w.cooldown = cd;
            w.range = range;
            w.knockback = kb;
            w.price = price;
            w.attackDuration = 0.28f;
        }

        static void Ranged(WeaponData w, Sprite sprite, Sprite projectile, float dmg, float cd, float range, float speed, int price)
        {
            w.kind = WeaponKind.Ranged;
            w.sprite = sprite;
            w.projectileSprite = projectile;
            w.projectileAngleOffset = 0f; // balles Pixel Guns dessinées vers la droite
            w.damage = dmg;
            w.cooldown = cd;
            w.range = range;
            w.projectileSpeed = speed;
            w.price = price;
        }

        // ---------------------------------------------------------------- Ennemis

        public static List<(EnemyData data, float weight, int fromWave)> AddEnemies(Dictionary<string, Sprite> props, out EnemyData boss)
        {
            var fireball = Sprites(FireballGuid)[0];
            var orb = props["Bullet 5"];
            var beetleFrames = Sprites(BeetleGuid);

            var crow = Enemy("Corbeau", e =>
            {
                Stats(e, hp: 2, hpWave: 1, speed: 3.4f, dmg: 1, dmgWave: 0.4f, drop: 1);
                e.runFrames = Sprites(CrowGuid);
                e.facesLeft = true;
                e.bobAmount = 0.12f;
            });
            var ghoul = Enemy("Goule", e =>
            {
                Stats(e, hp: 12, hpWave: 4, speed: 1.8f, dmg: 2, dmgWave: 0.8f, drop: 2);
                e.runFrames = Sprites(GothicGuid);
                e.knockbackResistance = 0.3f;
            });
            var wizard = Enemy("Mage noir", e =>
            {
                Stats(e, hp: 6, hpWave: 2, speed: 1.6f, dmg: 1, dmgWave: 0.4f, drop: 2);
                e.behavior = EnemyBehavior.Ranged;
                e.runFrames = Sprites(DarkWizardGuid);
                Shooter(e, fireball, new Color(0.75f, 0.4f, 1f), interval: 2.2f, speed: 5.5f, count: 1, spread: 0f);
                e.preferredDistance = 5.5f;
            });
            var firehead = Enemy("Tête de feu", e =>
            {
                Stats(e, hp: 5, hpWave: 2, speed: 2f, dmg: 1, dmgWave: 0.4f, drop: 2);
                e.behavior = EnemyBehavior.Ranged;
                e.runFrames = Sprites(FireheadGuid);
                Shooter(e, fireball, new Color(1f, 0.6f, 0.2f), interval: 1.8f, speed: 6f, count: 3, spread: 30f);
                e.preferredDistance = 4.5f;
            });
            var beetle = Enemy("Scarabée", e =>
            {
                Stats(e, hp: 7, hpWave: 2.5f, speed: 1.8f, dmg: 2, dmgWave: 0.7f, drop: 2);
                e.behavior = EnemyBehavior.Charger;
                e.runFrames = beetleFrames;
                e.rotateToDirection = true;
                e.chargeSpeed = 9f;
                e.chargeTriggerDistance = 5f;
            });
            var larva = Enemy("Larve", e =>
            {
                Stats(e, hp: 2, hpWave: 1, speed: 2.8f, dmg: 1, dmgWave: 0.4f, drop: 1);
                e.runFrames = beetleFrames;
                e.rotateToDirection = true;
                e.scale = 0.55f;
                e.tint = new Color(0.7f, 1f, 0.7f);
                e.dropChance = 0.5f;
            });
            var giantBeetle = Enemy("Scarabée géant", e =>
            {
                Stats(e, hp: 18, hpWave: 6, speed: 1.2f, dmg: 2, dmgWave: 0.8f, drop: 3);
                e.runFrames = Sprites(GiantBeetleGuid);
                e.rotateToDirection = true;
                e.radius = 0.5f;
                e.knockbackResistance = 0.5f;
                e.splitInto = larva;
                e.splitCount = 3;
            });
            var canon = Enemy("Canon", e =>
            {
                Stats(e, hp: 15, hpWave: 5, speed: 0f, dmg: 1, dmgWave: 0.4f, drop: 4);
                e.behavior = EnemyBehavior.Turret;
                e.runFrames = Sprites(CanonGuid);
                e.bobAmount = 0f;
                e.knockbackResistance = 1f;
                Shooter(e, orb, new Color(1f, 0.4f, 0.4f), interval: 3f, speed: 4f, count: 8, spread: 360f);
            });
            var troll = Enemy("Troll", e =>
            {
                Stats(e, hp: 120, hpWave: 25, speed: 1.3f, dmg: 4, dmgWave: 1f, drop: 12);
                e.behavior = EnemyBehavior.Charger;
                e.runFrames = Sprites(TrollGuid);
                e.scale = 2.2f;
                e.radius = 0.4f;
                e.knockbackResistance = 0.9f;
                e.chargeSpeed = 8f;
                e.chargeTriggerDistance = 6f;
                e.chargeWindup = 0.8f;
                e.isElite = true;
                e.crateChance = 0.2f;
            });
            boss = Enemy("Dragon", e =>
            {
                Stats(e, hp: 3000, hpWave: 0, speed: 1f, dmg: 6, dmgWave: 0f, drop: 50);
                e.behavior = EnemyBehavior.Boss;
                e.isBoss = true;
                e.runFrames = Sprites(DragonGuid);
                e.scale = 3.5f;
                e.radius = 0.4f;
                e.knockbackResistance = 1f;
                Shooter(e, fireball, new Color(1f, 0.55f, 0.2f), interval: 2.5f, speed: 6f, count: 1, spread: 0f);
                e.projectileScale = 1.3f;
                e.projectileDamage = 3f;
                e.projectileDamagePerWave = 0f;
                e.chargeSpeed = 10f;
                e.chargeWindup = 0.8f;
                e.chargeDuration = 0.6f;
                e.chargeTriggerDistance = 99f;
                e.splitInto = crow;
                e.splitCount = 4;
                e.crateChance = 1f;
            });

            return new List<(EnemyData, float, int)>
            {
                (crow, 0.5f, 4),
                (beetle, 0.4f, 5),
                (wizard, 0.35f, 6),
                (canon, 0.15f, 7),
                (giantBeetle, 0.25f, 8),
                (firehead, 0.3f, 9),
                (troll, 0.03f, 10),
                (ghoul, 0.5f, 11),
            };
        }

        static EnemyData Enemy(string displayName, Action<EnemyData> init)
        {
            string file = new string(displayName.Where(char.IsLetterOrDigit).ToArray());
            return PrototypeBuilder.LoadOrCreate<EnemyData>($"{DataDir}/Enemies/Enemy_{file}.asset", e =>
            {
                e.displayName = displayName;
                init(e);
            });
        }

        static void Stats(EnemyData e, float hp, float hpWave, float speed, float dmg, float dmgWave, int drop)
        {
            e.maxHealth = hp;
            e.healthPerWave = hpWave;
            e.moveSpeed = speed;
            e.contactDamage = dmg;
            e.damagePerWave = dmgWave;
            e.materialDrop = drop;
        }

        static void Shooter(EnemyData e, Sprite sprite, Color color, float interval, float speed, int count, float spread)
        {
            e.projectileSprite = sprite;
            e.projectileColor = color;
            e.fireInterval = interval;
            e.projectileSpeed = speed;
            e.projectilesPerShot = count;
            e.projectileSpread = spread;
        }

        /// <summary>Ajoute les nouveaux ennemis aux vagues et place le boss à la dernière vague.</summary>
        public static void SyncWaveSet(WaveSet waveSet, List<(EnemyData data, float weight, int fromWave)> entries, EnemyData boss)
        {
            foreach (var (data, weight, fromWave) in entries)
            {
                if (waveSet.enemies.Any(e => e.enemy == data)) continue;
                waveSet.enemies.Add(new WaveSet.SpawnEntry { enemy = data, weight = weight, fromWave = fromWave });
            }
            var last = waveSet.waves.LastOrDefault();
            if (last != null && last.boss == null) last.boss = boss;

            // La pierre tombale est une élite (caisse de temps en temps seulement)
            foreach (var e in waveSet.enemies)
            {
                if (e.enemy == null || (e.enemy.displayName != "Pierre tombale" && e.enemy.displayName != "Tombstone") || e.enemy.isElite) continue;
                e.enemy.isElite = true;
                e.enemy.crateChance = 0.2f;
                EditorUtility.SetDirty(e.enemy);
            }
            EditorUtility.SetDirty(waveSet);
        }

        // ---------------------------------------------------------------- Scène

        public static bool PatchGameManager(GameManager gm, Dictionary<string, Sprite> props)
        {
            bool changed = false;
            if (gm.explosionFrames == null || gm.explosionFrames.Length == 0)
            {
                gm.explosionFrames = Sprites(ExplosionGuid);
                changed = true;
            }
            if (gm.crateSprite == null)
            {
                gm.crateSprite = props["Box"];
                changed = true;
            }
            return changed;
        }

        /// <summary>Sprites d'une texture triés par nom (Explosion_0, Explosion_1...).</summary>
        static Sprite[] Sprites(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) throw new Exception($"Texture introuvable (GUID {guid})");
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(s => s.name.Length).ThenBy(s => s.name, StringComparer.Ordinal).ToArray();
        }
    }
}
