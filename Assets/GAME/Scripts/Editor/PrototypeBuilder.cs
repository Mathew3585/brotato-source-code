using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Menu "BrotatoLike > Construire le prototype" : génère données, prefabs, tuiles et scène de jeu
    /// à partir du pack Undead Survivor. Les assets déjà existants ne sont PAS écrasés
    /// (supprime un asset pour le régénérer).
    /// </summary>
    public static class PrototypeBuilder
    {
        const string Root = "Assets/GAME";
        const string DataDir = Root + "/Data";
        const string PrefabDir = Root + "/Prefabs";
        const string TileDir = Root + "/Art/Tiles";
        const string ScenePath = Root + "/Scenes/Game.unity";
        const string MenuScenePath = Root + "/Scenes/MainMenu.unity";
        const string GameTitle = "CURSED FARM";
        const string InputPath = Root + "/Input/GameControls.inputactions";

        static readonly Vector2Int ArenaSize = new(40, 26);

        // GUID des textures du pack Undead Survivor (insensible aux déplacements de dossier)
        const string PropsGuid = "0d5e657ee5a074e4baa53d5b248a01d2";
        const string TilesGuid = "9ee30eb7d2f1f404790266ee96a67099";
        static readonly string[] FarmerGuids =
        {
            "99a24620d31e5ce4e823c32d11e17c2c", "0c187a32c5bde4747be8d00770b07403",
            "8f7a60065e3984c489b9c4d41bc3dc4e", "57a1443e67528144aa07582927559650",
        };
        static readonly string[] EnemyGuids =
        {
            "1bc6097228993f045852159e71f81d5b", "3bcb7877229a5334da528f2f0872b5c3", "4104ee12b4d54f54b91b3ba875787837",
            "d33598d781682e049a53a0a13104ef29", "c4d2ccab0c2ad7e47853b6e31a7ac7c2",
        };

        // Cadenas karsiori (fermé) pour verrouiller une offre en boutique
        const string LockIconGuid = "23fdb30825d27fe4e8d24b725859531f";

        // Objets passifs : icônes pixel art 16x16 du pack Brackeys
        static readonly (string name, string iconGuid, int tier, int price, StatModifier[] mods)[] ItemDefs =
        {
            ("Cœur robuste", "222d62d87db5931498bb85845842b0d3", 1, 15, new[] { M(StatType.MaxHealth, 5) }),
            ("Gigot", "eab87f10d2afa594c92ed3d29451b618", 1, 18, new[] { M(StatType.MaxHealth, 4), M(StatType.HealthRegen, 2), M(StatType.MoveSpeed, -2) }),
            ("Potion verte", "db005d11213e5da47b9769c2e6c56eee", 1, 15, new[] { M(StatType.HealthRegen, 3) }),
            ("Poisson frais", "78040fcc3a5ca0646b20e131d68b3660", 1, 16, new[] { M(StatType.HealthRegen, 2), M(StatType.MoveSpeed, 3), M(StatType.Armor, -1) }),
            ("Plaque de fer", "9e3850ff9c22c0949954069e855a2a44", 1, 16, new[] { M(StatType.Armor, 2), M(StatType.MoveSpeed, -2) }),
            ("Chapeau de paille", "5087bdcc637973d41abbd18802fb23d3", 1, 15, new[] { M(StatType.Dodge, 6), M(StatType.Armor, -1) }),
            ("Pelote de laine", "a88d5300df9790349866dcad93d3a1ea", 1, 15, new[] { M(StatType.Armor, 1), M(StatType.MaxHealth, 2) }),
            ("Charbon", "9c5f75faa4c63d4499e6694da108de76", 1, 16, new[] { M(StatType.RangedDamage, 2), M(StatType.Damage, 5), M(StatType.MaxHealth, -2) }),
            ("Parchemin", "60dbf6a31ffde4645b0ff2a518c88795", 1, 18, new[] { M(StatType.Range, 1), M(StatType.CritChance, 3), M(StatType.MeleeDamage, -1) }),
            ("Essence magique", "8a14f58b4b600a24ab3fcf9cb3bc82fb", 1, 20, new[] { M(StatType.AttackSpeed, 10), M(StatType.Damage, -3) }),
            ("Pièce porte-bonheur", "d2c0dc76eca05bb4eb4f3fc4a37f258d", 1, 15, new[] { M(StatType.Luck, 8), M(StatType.PickupRange, 1), M(StatType.Harvesting, 2) }),
            ("Flamme", "80e159dab3e6f1142bc5062eb184ccaf", 2, 35, new[] { M(StatType.Damage, 6), M(StatType.AttackSpeed, 6), M(StatType.HealthRegen, -1) }),
            ("Diamant", "6b3b8ef492edc304f9230dc368e8af5c", 2, 35, new[] { M(StatType.CritChance, 6), M(StatType.Damage, 3) }),
            ("L'Anneau", "ec3f5c9d06947e7458e9626551938feb", 2, 30, new[] { M(StatType.Luck, 10), M(StatType.Dodge, 3) }),
            ("Couronne", "f30c689e1cd8caa45a28c4947f382374", 3, 70, new[] { M(StatType.Damage, 10), M(StatType.AttackSpeed, 6), M(StatType.MaxHealth, 4) }),
            ("Œuf magique", "e0c7d5a920e98fd47a926292adc0bec7", 3, 65, new[] { M(StatType.MaxHealth, 8), M(StatType.HealthRegen, 3), M(StatType.Armor, 2) }),
            ("Fin du monde", "5c08bbcce060bf24c8074d9f4d593e63", 4, 110, new[] { M(StatType.Damage, 25), M(StatType.MeleeDamage, 5), M(StatType.RangedDamage, 5), M(StatType.Armor, -3), M(StatType.MoveSpeed, -5) }),
        };

        static StatModifier M(StatType stat, float value) => new(stat, value);

        [MenuItem("BrotatoLike/Construire le prototype", priority = 0)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                foreach (var dir in new[] { DataDir + "/Characters", DataDir + "/Weapons", DataDir + "/Enemies", DataDir + "/Items", PrefabDir, TileDir, Root + "/Scenes" })
                    EnsureFolder(dir);

                ConfigurePixelArtImport(FarmerGuids.Concat(EnemyGuids).Append(PropsGuid).Append(TilesGuid)
                    .Append(LockIconGuid).Concat(ItemDefs.Select(d => d.iconGuid)));
                ContentBuilder.ConfigureImports();
                AudioBuilder.ConfigureImports();

                var props = LoadSprites(PropsGuid);
                var weapons = BuildWeapons(props);
                ContentBuilder.AddWeapons(weapons, props);
                var characters = BuildCharacters(weapons);
                var enemies = BuildEnemies();
                var waveSet = BuildWaveSet(enemies);
                var newEnemies = ContentBuilder.AddEnemies(props, out var boss);
                ContentBuilder.SyncWaveSet(waveSet, newEnemies, boss);
                var prefabs = BuildPrefabs(props);
                var catalog = BuildCatalog(BuildWeaponTiers(weapons), BuildItems());
                var netPrefabs = NetworkBuilder.BuildPrefabs(props);
                ContentBuilder.ApplyTierSprites();
                EnglishMigration.MigrateData();
                var audioConfig = AudioBuilder.BuildConfig();
                DangerBuilder.BuildAsset();
                ChallengeBuilder.BuildAsset();
                LoadOrCreate<JuiceSettings>(DataDir + "/Resources/JuiceSettings.asset", _ => { });
                AudioBuilder.ApplyDebrisColors(AssetDatabase.FindAssets("t:EnemyData", new[] { DataDir })
                    .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g))));

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    BuildScene(props, characters, waveSet, catalog, prefabs);
                else
                    PatchScene(catalog, props);

                AssetDatabase.SaveAssets();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null)
                    BuildMenuScene(characters);
                else
                    PatchMenuScene();

                // Police, options, écran de fin, son et ressenti dans les deux scènes
                var font = FontBuilder.Setup();
                var transition = TransitionBuilder.BuildPrefab();
                foreach (var path in new[] { ScenePath, MenuScenePath })
                {
                    var opened = EditorSceneManager.OpenScene(path);
                    bool changed = path == ScenePath ? ExtraUIBuilder.PatchGameScene() : ExtraUIBuilder.PatchMenuScene();
                    if (path == MenuScenePath) changed |= NetworkBuilder.PatchMenuScene(netPrefabs);
                    if (path == MenuScenePath) changed |= DangerBuilder.PatchMenuScene();
                    changed |= path == MenuScenePath ? ChallengeBuilder.PatchMenuScene() : ChallengeBuilder.PatchGameScene();
                    changed |= path == ScenePath ? QolBuilder.PatchGameScene() : QolBuilder.PatchMenuScene();
                    changed |= AudioBuilder.PatchOpenScene(audioConfig);
                    changed |= TransitionBuilder.PatchOpenScene(transition);
                    changed |= FontBuilder.ApplyToOpenScene(font);
                    changed |= EnglishMigration.MigrateOpenScene();
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(opened);
                        EditorSceneManager.SaveScene(opened);
                    }
                }

                // UI répétée → prefabs (conversion sur place, garde les références et les retouches)
                PrefabBuilder.Run();
                // Coop : bouton « Give » dans le prefab ShopColumn
                CoopBuilder.Run();
                // Écran de fin : carte de chaque joueur et cadre doré du meilleur
                EndScreenBuilder.Run();
                // Stats avancées : page Stats de la boutique, nouveaux objets
                StatsBuilder.Run();
                // Roue des emotes (asset, prefab Resources/EmoteWheel)
                EmoteBuilder.Run();

                AssetDatabase.SaveAssets();
                AddSceneToBuildSettings();
                EditorSceneManager.OpenScene(MenuScenePath);
                Debug.Log("[BrotatoLike] Prototype construit ! Lance Play depuis la scène MainMenu.");
            }
            catch (Exception e)
            {
                Debug.LogError("[BrotatoLike] Échec de la construction du prototype : " + e);
            }
        }

        [MenuItem("BrotatoLike/Ouvrir la scène de jeu", priority = 1)]
        public static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        // ---------------------------------------------------------------- Données

        static Dictionary<string, WeaponData> BuildWeapons(Dictionary<string, Sprite> props)
        {
            var fireball = props["Bullet 5"];
            var bolt = props["Bullet 3"];

            return new Dictionary<string, WeaponData>
            {
                ["Fusil"] = LoadOrCreate<WeaponData>(DataDir + "/Weapons/Weapon_Fusil.asset", w =>
                {
                    w.displayName = "Fusil";
                    w.price = 20;
                    w.sprite = props["Weapon 3"];
                    w.spriteScale = 0.8f;
                    w.projectileSprite = fireball;
                    w.damage = 4f;
                    w.cooldown = 0.6f;
                    w.range = 7f;
                    w.projectileSpeed = 16f;
                    w.knockback = 0.25f;
                }),
                ["Tromblon"] = LoadOrCreate<WeaponData>(DataDir + "/Weapons/Weapon_Tromblon.asset", w =>
                {
                    w.displayName = "Tromblon";
                    w.price = 30;
                    w.sprite = props["Weapon 4"];
                    w.spriteScale = 0.8f;
                    w.projectileSprite = fireball;
                    w.projectileScale = 0.8f;
                    w.damage = 3f;
                    w.cooldown = 1.1f;
                    w.range = 5f;
                    w.projectileSpeed = 14f;
                    w.projectilesPerShot = 4;
                    w.spreadAngle = 35f;
                    w.knockback = 0.5f;
                }),
                ["Mousquet"] = LoadOrCreate<WeaponData>(DataDir + "/Weapons/Weapon_Mousquet.asset", w =>
                {
                    w.displayName = "Mousquet";
                    w.price = 35;
                    w.sprite = props["Weapon 5"];
                    w.spriteScale = 0.8f;
                    w.projectileSprite = bolt;
                    w.damage = 10f;
                    w.cooldown = 1.6f;
                    w.range = 10f;
                    w.projectileSpeed = 22f;
                    w.pierce = 2;
                    w.knockback = 0.8f;
                    w.critChance = 10f;
                }),
            };
        }

        static CharacterData[] BuildCharacters(Dictionary<string, WeaponData> weapons)
        {
            var defs = new (string name, Color color, string weapon, StatModifier[] stats)[]
            {
                ("Pierre", new Color(0.35f, 0.65f, 1f), "Fusil", new[]
                {
                    new StatModifier(StatType.MaxHealth, 10), new StatModifier(StatType.PickupRange, 2.5f),
                }),
                ("Lucie", new Color(1f, 0.45f, 0.7f), "Mousquet", new[]
                {
                    new StatModifier(StatType.MaxHealth, 8), new StatModifier(StatType.PickupRange, 2.5f),
                    new StatModifier(StatType.CritChance, 5), new StatModifier(StatType.MoveSpeed, 5),
                }),
                ("Hugo", new Color(0.45f, 0.9f, 0.45f), "Tromblon", new[]
                {
                    new StatModifier(StatType.MaxHealth, 14), new StatModifier(StatType.PickupRange, 2.5f),
                    new StatModifier(StatType.Armor, 1), new StatModifier(StatType.MoveSpeed, -5),
                }),
                ("Margot", new Color(1f, 0.8f, 0.3f), "Fusil", new[]
                {
                    new StatModifier(StatType.MaxHealth, 10), new StatModifier(StatType.PickupRange, 3.5f),
                    new StatModifier(StatType.AttackSpeed, 10), new StatModifier(StatType.Luck, 10),
                }),
            };

            var result = new CharacterData[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                var sprites = LoadSprites(FarmerGuids[i]);
                result[i] = LoadOrCreate<CharacterData>($"{DataDir}/Characters/Character_{def.name}.asset", c =>
                {
                    c.displayName = def.name;
                    c.uiColor = def.color;
                    c.runFrames = Frames(sprites, "Run");
                    c.idleFrames = Frames(sprites, "Stand");
                    c.deadSprite = sprites["Dead 0"];
                    c.baseStats = def.stats;
                    c.startingWeapon = weapons[def.weapon];
                });
            }
            return result;
        }

        static EnemyData[] BuildEnemies()
        {
            var defs = new (string name, float hp, float hpWave, float speed, float dmg, float dmgWave, int drop, float kbResist, float scale, float radius, float fps)[]
            {
                ("Zombie",           4f,  2f,   1.7f, 1f,   0.6f, 1,  0f,   1f,   0.35f, 8f),
                ("Zombie coureur",   3f,  1.5f, 2.8f, 1f,   0.5f, 1,  0f,   1f,   0.35f, 12f),
                ("Squelette",        9f,  3f,   1.5f, 2f,   0.8f, 2,  0.3f, 1f,   0.35f, 8f),
                ("Squelette pirate", 7f,  2.5f, 2.3f, 1.5f, 0.7f, 2,  0.1f, 1f,   0.35f, 10f),
                ("Pierre tombale",   50f, 15f,  0.9f, 3f,   1f,   10, 0.9f, 1.6f, 0.4f,  4f),
            };

            var result = new EnemyData[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                var sprites = LoadSprites(EnemyGuids[i]);
                result[i] = LoadOrCreate<EnemyData>($"{DataDir}/Enemies/Enemy_{def.name.Replace(' ', '_')}.asset", e =>
                {
                    e.displayName = def.name;
                    e.runFrames = Frames(sprites, "Run");
                    e.hitSprite = sprites["Hit"];
                    e.deadSprite = sprites["Dead"];
                    e.animFps = def.fps;
                    e.scale = def.scale;
                    e.maxHealth = def.hp;
                    e.healthPerWave = def.hpWave;
                    e.moveSpeed = def.speed;
                    e.contactDamage = def.dmg;
                    e.damagePerWave = def.dmgWave;
                    e.materialDrop = def.drop;
                    e.knockbackResistance = def.kbResist;
                    e.radius = def.radius;
                });
            }
            return result;
        }

        static WaveSet BuildWaveSet(EnemyData[] enemies)
        {
            return LoadOrCreate<WaveSet>(DataDir + "/WaveSet.asset", ws =>
            {
                const int count = 20;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (count - 1f);
                    ws.waves.Add(new WaveSet.Wave
                    {
                        duration = Mathf.Min(20f + 5f * i, 60f),
                        spawnInterval = Mathf.Lerp(1.6f, 0.45f, t),
                        groupSizeMin = 1 + i / 5,
                        groupSizeMax = 3 + i / 3,
                        maxAlive = 30 + 12 * i,
                    });
                }

                ws.enemies.Add(new WaveSet.SpawnEntry { enemy = enemies[0], weight = 1f, fromWave = 1 });
                ws.enemies.Add(new WaveSet.SpawnEntry { enemy = enemies[1], weight = 0.6f, fromWave = 3 });
                ws.enemies.Add(new WaveSet.SpawnEntry { enemy = enemies[2], weight = 0.6f, fromWave = 5 });
                ws.enemies.Add(new WaveSet.SpawnEntry { enemy = enemies[3], weight = 0.5f, fromWave = 8 });
                ws.enemies.Add(new WaveSet.SpawnEntry { enemy = enemies[4], weight = 0.04f, fromWave = 6 });
            });
        }

        /// <summary>Crée les tiers II à IV de chaque arme et les chaîne via nextTier (fusion).</summary>
        static List<WeaponData> BuildWeaponTiers(Dictionary<string, WeaponData> baseWeapons)
        {
            var all = new List<WeaponData>();
            foreach (var (key, baseWeapon) in baseWeapons)
            {
                var previous = baseWeapon;
                all.Add(baseWeapon);
                for (int t = 2; t <= 4; t++)
                {
                    int tier = t;
                    var w = LoadOrCreate<WeaponData>($"{DataDir}/Weapons/Weapon_{key}_T{t}.asset", x =>
                    {
                        EditorUtility.CopySerialized(baseWeapon, x);
                        float k = tier - 1;
                        x.tier = tier;
                        x.displayName = $"{baseWeapon.displayName} {ShopItemData.TierRoman(tier)}";
                        x.price = Mathf.RoundToInt(baseWeapon.price * (1f + 0.9f * k));
                        x.damage = baseWeapon.damage * (1f + 0.6f * k);
                        x.cooldown = baseWeapon.cooldown * (1f - 0.08f * k);
                        x.critChance = baseWeapon.critChance + 2f * k;
                        x.knockback = baseWeapon.knockback * (1f + 0.1f * k);
                        x.nextTier = null;
                    });
                    if (previous.nextTier == null)
                    {
                        previous.nextTier = w;
                        EditorUtility.SetDirty(previous);
                    }
                    previous = w;
                    all.Add(w);
                }
            }
            return all;
        }

        static List<ItemData> BuildItems()
        {
            var result = new List<ItemData>();
            foreach (var def in ItemDefs)
            {
                string file = new string(def.name.Where(char.IsLetterOrDigit).ToArray());
                result.Add(LoadOrCreate<ItemData>($"{DataDir}/Items/Item_{file}.asset", item =>
                {
                    item.displayName = def.name;
                    item.icon = LoadSingleSprite(def.iconGuid);
                    item.tier = def.tier;
                    item.price = def.price;
                    item.modifiers = def.mods;
                }));
            }
            return result;
        }

        static ShopCatalog BuildCatalog(List<WeaponData> weapons, List<ItemData> items)
        {
            var catalog = LoadOrCreate<ShopCatalog>(DataDir + "/ShopCatalog.asset", _ => { });
            // Synchronise : ajoute ce qui manque sans retirer ce que tu as ajouté à la main
            foreach (var w in weapons)
                if (w.kind != WeaponKind.Turret && !catalog.weapons.Contains(w)) catalog.weapons.Add(w);
            catalog.weapons.RemoveAll(w => w != null && w.kind == WeaponKind.Turret);
            var turretItem = ContentBuilder.EnsureTurretItem();
            if (turretItem != null && !items.Contains(turretItem)) items.Add(turretItem);
            foreach (var i in items)
                if (!catalog.items.Contains(i)) catalog.items.Add(i);
            if (catalog.lockIcon == null) catalog.lockIcon = LoadSingleSprite(LockIconGuid);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>Met à jour une scène existante avec les nouvelles références (sans la reconstruire).</summary>
        static void PatchScene(ShopCatalog catalog, Dictionary<string, Sprite> props)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null) return;

            bool changed = ContentBuilder.PatchGameManager(gm, props);
            if (gm.shopCatalog == null)
            {
                gm.shopCatalog = catalog;
                changed = true;
            }

            // UI absente (anciennes versions la créaient au lancement) : on la crée dans la scène
            var hud = Object.FindFirstObjectByType<HUD>();
            if (hud != null && new SerializedObject(hud).FindProperty("hudRoot").objectReferenceValue == null)
            {
                GameUIBuilder.Build(hud);
                changed = true;
            }
            if (!changed) return;
            EditorUtility.SetDirty(gm);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- Prefabs

        class Prefabs
        {
            public GameObject player;
            public Enemy enemy;
            public Projectile projectile;
            public Pickup pickup;
            public SpriteRenderer spawnMarker;
        }

        static Prefabs BuildPrefabs(Dictionary<string, Sprite> props)
        {
            var shadowSprite = props["Shadow"];
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (inputAsset == null) throw new Exception("Fichier de contrôles introuvable : " + InputPath);

            var weaponPrefab = LoadOrCreatePrefab(PrefabDir + "/Weapon.prefab", () =>
            {
                var go = new GameObject("Weapon");
                var sr = go.AddComponent<SpriteRenderer>();
                var weapon = go.AddComponent<Weapon>();
                SetRef(weapon, "spriteRenderer", sr);
                return go;
            }).GetComponent<Weapon>();

            var p = new Prefabs();

            p.player = LoadOrCreatePrefab(PrefabDir + "/Player.prefab", () =>
            {
                var go = new GameObject("Player");
                var shadow = CreateSprite("Shadow", go.transform, shadowSprite, -900, new Vector2(0f, -0.5f));
                var body = CreateSprite("Body", go.transform, null, 0, Vector2.zero);
                var weaponsGo = new GameObject("Weapons");
                weaponsGo.transform.SetParent(go.transform, false);
                var holder = weaponsGo.AddComponent<WeaponHolder>();
                SetRef(holder, "weaponPrefab", weaponPrefab);

                var input = go.AddComponent<PlayerInput>();
                input.actions = inputAsset;
                input.defaultActionMap = "Player";
                input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

                var player = go.AddComponent<Player>();
                SetRef(player, "body", body);
                SetRef(player, "shadow", shadow);
                SetRef(player, "weapons", holder);
                return go;
            });

            p.enemy = LoadOrCreatePrefab(PrefabDir + "/Enemy.prefab", () =>
            {
                var go = new GameObject("Enemy");
                var shadow = CreateSprite("Shadow", go.transform, shadowSprite, -900, new Vector2(0f, -0.42f));
                var body = CreateSprite("Body", go.transform, null, 0, Vector2.zero);
                var enemy = go.AddComponent<Enemy>();
                SetRef(enemy, "body", body);
                SetRef(enemy, "shadow", shadow);
                return go;
            }).GetComponent<Enemy>();

            p.projectile = LoadOrCreatePrefab(PrefabDir + "/Projectile.prefab", () =>
            {
                var go = new GameObject("Projectile");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 31000;
                var proj = go.AddComponent<Projectile>();
                SetRef(proj, "spriteRenderer", sr);
                return go;
            }).GetComponent<Projectile>();

            p.pickup = LoadOrCreatePrefab(PrefabDir + "/Pickup.prefab", () =>
            {
                var go = new GameObject("Pickup");
                var sr = go.AddComponent<SpriteRenderer>();
                go.transform.localScale = Vector3.one * 0.7f;
                var pickup = go.AddComponent<Pickup>();
                SetRef(pickup, "spriteRenderer", sr);
                return go;
            }).GetComponent<Pickup>();

            p.spawnMarker = LoadOrCreatePrefab(PrefabDir + "/SpawnMarker.prefab", () =>
            {
                var go = new GameObject("SpawnMarker");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = shadowSprite;
                sr.color = new Color(1f, 0.15f, 0.15f, 0.8f);
                go.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
                return go;
            }).GetComponent<SpriteRenderer>();

            return p;
        }

        // ---------------------------------------------------------------- Scène

        static void BuildScene(Dictionary<string, Sprite> props, CharacterData[] characters, WaveSet waveSet, ShopCatalog catalog, Prefabs prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Caméra
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.11f, 0.1f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<CameraController>();

            // Lumière globale 2D
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // Arène + sol
            var arena = new GameObject("Arena").AddComponent<Arena>();
            arena.size = ArenaSize;
            BuildGround();

            // Gestion du jeu
            var gmGo = new GameObject("GameManager");
            var spawner = gmGo.AddComponent<EnemySpawner>();
            SetRef(spawner, "markerPrefab", prefabs.spawnMarker);
            var gm = gmGo.AddComponent<GameManager>();
            gm.characters = characters;
            gm.waveSet = waveSet;
            gm.shopCatalog = catalog;
            ContentBuilder.PatchGameManager(gm, props);
            gm.enemyPrefab = prefabs.enemy;
            gm.projectilePrefab = prefabs.projectile;
            gm.pickupPrefab = prefabs.pickup;
            gm.materialSprites = new[] { props["Exp 0"], props["Exp 1"], props["Exp 2"] };
            gm.spawner = spawner;

            // Coop locale : un joueur rejoint en appuyant sur un bouton de son périphérique
            var pim = new GameObject("PlayerInputManager").AddComponent<PlayerInputManager>();
            pim.playerPrefab = prefabs.player;
            pim.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
            pim.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            var so = new SerializedObject(pim);
            so.FindProperty("m_MaxPlayerCount").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameUIBuilder.Build(new GameObject("HUD").AddComponent<HUD>());

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void BuildGround()
        {
            var tileSprites = LoadSprites(TilesGuid);
            var tiles = new Tile[6];
            for (int i = 0; i < tiles.Length; i++)
            {
                var sprite = tileSprites["Tile " + i];
                tiles[i] = LoadOrCreate<Tile>($"{TileDir}/Ground_{i}.asset", t => t.sprite = sprite);
            }

            var grid = new GameObject("Grid").AddComponent<Grid>();
            var ground = CreateTilemap(grid.transform, "Ground", -1000, Color.white);
            var outside = CreateTilemap(grid.transform, "Outside", -1001, new Color(0.45f, 0.5f, 0.45f));

            var rng = new System.Random(42);
            int halfW = ArenaSize.x / 2, halfH = ArenaSize.y / 2;
            const int margin = 14;
            for (int x = -halfW - margin; x < halfW + margin; x++)
            for (int y = -halfH - margin; y < halfH + margin; y++)
            {
                bool inside = x >= -halfW && x < halfW && y >= -halfH && y < halfH;
                // Surtout de l'herbe unie, quelques décorations
                int index = rng.NextDouble() < 0.75 ? 0 : 1 + rng.Next(5);
                (inside ? ground : outside).SetTile(new Vector3Int(x, y, 0), tiles[index]);
            }
        }

        static Tilemap CreateTilemap(Transform parent, string name, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tilemap = go.AddComponent<Tilemap>();
            tilemap.color = color;
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return tilemap;
        }

        static void BuildMenuScene(CharacterData[] characters)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.11f, 0.09f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();

            // EventSystem partagé : dans le menu, n'importe quel périphérique peut naviguer
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            menu.characters = characters;
            MenuUIBuilder.Build(menu, GameTitle);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        static void PatchMenuScene()
        {
            var scene = EditorSceneManager.OpenScene(MenuScenePath);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            if (menu == null || new SerializedObject(menu).FindProperty("menuPanel").objectReferenceValue != null) return;
            MenuUIBuilder.Build(menu, GameTitle);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("BrotatoLike/Reconstruire l'UI des scènes", priority = 20)]
        public static void RebuildSceneUI()
        {
            if (!EditorUtility.DisplayDialog("Reconstruire l'UI",
                    "L'UI des scènes MainMenu et Game va être recréée. Tes modifications de mise en page seront PERDUES.",
                    "Reconstruire", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var game = EditorSceneManager.OpenScene(ScenePath);
            var hud = Object.FindFirstObjectByType<HUD>();
            if (hud != null) GameUIBuilder.Build(hud);
            EditorSceneManager.SaveScene(game);

            var menuScene = EditorSceneManager.OpenScene(MenuScenePath);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            if (menu != null) MenuUIBuilder.Build(menu, GameTitle);
            EditorSceneManager.SaveScene(menuScene);
        }

        /// <summary>Menu en premier (scène de démarrage), puis le jeu.</summary>
        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath || s.path == MenuScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- Utilitaires

        internal static void ConfigurePixelArtImport(IEnumerable<string> guids)
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.filterMode == FilterMode.Point && importer.textureCompression == TextureImporterCompression.Uncompressed) continue;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static Dictionary<string, Sprite> LoadSprites(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                throw new Exception($"Texture introuvable (GUID {guid}). Le pack Undead Survivor est-il bien importé ?");
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        }

        internal static Sprite LoadSingleSprite(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) throw new Exception($"Sprite introuvable (GUID {guid})");
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }

        /// <summary>Récupère "Run 0", "Run 1"... triés par numéro.</summary>
        static Sprite[] Frames(Dictionary<string, Sprite> sprites, string prefix) =>
            sprites.Where(kv => kv.Key.StartsWith(prefix + " "))
                .OrderBy(kv => int.Parse(kv.Key.Substring(prefix.Length + 1)))
                .Select(kv => kv.Value)
                .ToArray();

        internal static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static GameObject LoadOrCreatePrefab(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int order, Vector2 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new Exception($"Champ '{field}' introuvable sur {target.GetType().Name}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
