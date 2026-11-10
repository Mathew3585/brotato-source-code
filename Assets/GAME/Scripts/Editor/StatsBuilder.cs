using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Stats avancées :
    /// - page « Stats » de la boutique (Q / E, LB / RB) et onglets en bas de chaque colonne, dans le prefab ShopColumn
    ///   (les petites stats en bas de colonne sont masquées) ;
    /// - nouveaux objets avec contreparties (Melee / Ranged Dmg, Engineering, Life Steal, Harvesting), ajoutés au catalogue.
    /// N'ajoute que ce qui manque, n'écrase aucun asset existant.
    /// </summary>
    static class StatsBuilder
    {
        const string ShopColumnPath = "Assets/GAME/Prefabs/UI/ShopColumn.prefab";
        const string GameScenePath = "Assets/GAME/Scenes/Game.unity";
        const string ItemsDir = "Assets/GAME/Data/Items";
        const string CatalogPath = "Assets/GAME/Data/ShopCatalog.asset";
        const string IconDir = "Assets/_External/Art/Brackeys/2D Mega Pack/Items & Icons/Pixel Art/";
        // Zone des offres (sous l'en-tête) et onglets sous la grille d'objets
        const float PageY = 64f, PageHeight = 540f, TabsY = 914f;

        static StatModifier M(StatType stat, float value) => new(stat, value);

        static readonly (string name, string icon, int tier, int price, int max, StatModifier[] mods)[] NewItems =
        {
            ("Whetstone", "Rock", 1, 18, 0, new[] { M(StatType.MeleeDamage, 3), M(StatType.RangedDamage, -1) }),
            ("Gunpowder", "Sand", 1, 18, 0, new[] { M(StatType.RangedDamage, 3), M(StatType.MeleeDamage, -1) }),
            ("Lumber", "Wood", 1, 16, 0, new[] { M(StatType.Engineering, 3), M(StatType.AttackSpeed, -3) }),
            ("Lost Sheep", "Sheep", 1, 20, 0, new[] { M(StatType.Harvesting, 5), M(StatType.Damage, -3) }),
            ("Blood Drop", "WaterDrop1", 2, 32, 0, new[] { M(StatType.LifeSteal, 3), M(StatType.MaxHealth, -2) }),
            ("Holy Water", "WaterDrop2", 2, 34, 0, new[] { M(StatType.LifeSteal, 2), M(StatType.HealthRegen, 2), M(StatType.Damage, -4) }),
            ("Hearth", "Fireplace", 2, 34, 0, new[] { M(StatType.HealthRegen, 3), M(StatType.Engineering, 3), M(StatType.MoveSpeed, -3) }),
            ("War Banner", "Enemy Army", 2, 36, 0, new[] { M(StatType.Damage, 8), M(StatType.MeleeDamage, 2), M(StatType.Luck, -8) }),
            ("Glass Cannon", "Glass", 3, 60, 0, new[] { M(StatType.Damage, 15), M(StatType.RangedDamage, 3), M(StatType.MaxHealth, -5), M(StatType.Armor, -2) }),
            ("Fortress", "Castle", 3, 65, 0, new[] { M(StatType.Armor, 4), M(StatType.Engineering, 5), M(StatType.Dodge, -5), M(StatType.MoveSpeed, -5) }),
            ("Militia", "Friendly Army", 3, 62, 0, new[] { M(StatType.MeleeDamage, 4), M(StatType.RangedDamage, 4), M(StatType.AttackSpeed, -6) }),
            ("Mother Earth", "Earth", 4, 100, 1, new[] { M(StatType.Harvesting, 15), M(StatType.MaxHealth, 8), M(StatType.MoveSpeed, -5) }),
        };

        [MenuItem("BrotatoLike/Stats : page Stats et nouveaux objets", priority = 14)]
        static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Run();
        }

        /// <summary>Appelé à la fin de PrototypeBuilder.Build (après la conversion en prefabs).</summary>
        public static void Run()
        {
            BuildItems();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopColumnPath) != null)
            {
                var root = PrefabUtility.LoadPrefabContents(ShopColumnPath);
                try
                {
                    if (PatchColumn(root)) PrefabUtility.SaveAsPrefabAsset(root, ShopColumnPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // Colonne de la scène qui n'est pas une instance du prefab : complétée directement
            var scene = EditorSceneManager.OpenScene(GameScenePath);
            bool changed = false;
            foreach (var v in Object.FindObjectsByType<ShopColumnView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= PatchColumn(v.gameObject);
            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ================================================================ Boutique

        static bool PatchColumn(GameObject root)
        {
            var v = root.GetComponent<ShopColumnView>();
            if (v == null || v.statsPage != null) return false;
            float width = v.Rect.sizeDelta.x;
            float inner = width - 32f;

            var page = UIFactory.TopLeft(v.transform, "StatsPage", 0f, PageY, width, PageHeight);
            v.statsPageText = UIFactory.Text(page, "Text", 16f, 4f, inner, PageHeight - 8f, 22, TextAlignmentOptions.TopLeft);
            v.statsPageText.text = "<size=80%><color=#FFD966>SURVIVAL</color></size>\nMax HP<pos=44%>10<pos=58%><size=65%><color=#8A8A8A>your health</color></size>";
            CopyFont(v.title, v.statsPageText);
            v.statsPage = page.gameObject;
            page.gameObject.SetActive(false);
            // Juste au-dessus du voile « prêt » et de l'infobulle dans l'ordre d'affichage : à côté des autres groupes
            if (v.shopGroup != null) page.SetSiblingIndex(v.shopGroup.transform.GetSiblingIndex() + 1);

            v.tabBar = UIFactory.Text(v.transform, "Tabs", 16f, TabsY, inner, 34f, 22, TextAlignmentOptions.Center);
            v.tabBar.text = "Q <    SHOP     STATS    > E";
            CopyFont(v.title, v.tabBar);
            if (v.tooltipRoot != null) v.tabBar.transform.SetSiblingIndex(v.tooltipRoot.transform.GetSiblingIndex());

            // Les petites stats du bas sont remplacées par la page Stats
            if (v.statsLeft != null) v.statsLeft.gameObject.SetActive(false);
            if (v.statsRight != null) v.statsRight.gameObject.SetActive(false);

            EditorUtility.SetDirty(v);
            return true;
        }

        static void CopyFont(TextMeshProUGUI from, TextMeshProUGUI to)
        {
            if (from == null) return;
            to.font = from.font;
            to.fontSharedMaterial = from.fontSharedMaterial;
        }

        // ================================================================ Objets

        static void BuildItems()
        {
            var guids = NewItems
                .Select(d => AssetDatabase.AssetPathToGUID(IconDir + d.icon + ".png"))
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList();
            PrototypeBuilder.ConfigurePixelArtImport(guids);

            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            foreach (var def in NewItems)
            {
                string guid = AssetDatabase.AssetPathToGUID(IconDir + def.icon + ".png");
                string file = new string(def.name.Where(char.IsLetterOrDigit).ToArray());
                var item = PrototypeBuilder.LoadOrCreate<ItemData>($"{ItemsDir}/Item_{file}.asset", i =>
                {
                    i.displayName = def.name;
                    i.icon = string.IsNullOrEmpty(guid) ? null : PrototypeBuilder.LoadSingleSprite(guid);
                    i.tier = def.tier;
                    i.price = def.price;
                    i.maxPerPlayer = def.max;
                    i.modifiers = def.mods;
                });
                if (catalog != null && !catalog.items.Contains(item))
                {
                    catalog.items.Add(item);
                    EditorUtility.SetDirty(catalog);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
