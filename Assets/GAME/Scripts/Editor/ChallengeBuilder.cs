using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Défis : asset ChallengeSet (s'il n'existe pas), bouton + écran « Challenges » dans MainMenu,
    /// notification « Challenge complete! » dans Game. N'ajoute que ce qui manque.
    /// </summary>
    static class ChallengeBuilder
    {
        const string AssetPath = "Assets/GAME/Data/Resources/ChallengeSet.asset";
        const string CardPrefabPath = "Assets/GAME/Prefabs/UI/ChallengeCard.prefab";
        const float CardSize = 150f, CardGap = 14f;
        const int CardsPerRow = 5;
        // Cadenas karsiori « Hefty Padlock » rouge (différent de celui de la boutique)
        const string LockGuid = "81e8f506db7e43648a44126cea84dc11";
        static readonly Color Accent = UIFactory.Accent;

        // ================================================================ Données

        public static ChallengeSet BuildAsset() => PrototypeBuilder.LoadOrCreate<ChallengeSet>(AssetPath, set =>
        {
            var items = Find<ItemData>(_ => true);
            var weapons = Find<WeaponData>(w => w.tier == 1);
            var characters = Find<CharacterData>(_ => true);
            ShopItemData Item(string name) => items.FirstOrDefault(i => i.displayName == name);
            ShopItemData Weapon(string name) => weapons.FirstOrDefault(w => w.displayName == name);
            CharacterData Character(string name) => characters.FirstOrDefault(c => c.displayName == name);

            ChallengeSet.Challenge C(string id, string name, ChallengeType type, int target, ShopItemData reward, CharacterData character = null) =>
                new() { id = id, displayName = name, type = type, target = target, reward = reward, character = character };

            set.challenges = new List<ChallengeSet.Challenge>
            {
                C("first_harvest", "First Harvest", ChallengeType.ReachWave, 5, Item("Lucky Coin")),
                C("night_shift", "Night Shift", ChallengeType.ReachWave, 10, Item("Magic Essence")),
                C("deep_night", "Deep Night", ChallengeType.ReachWave, 15, Item("Turret")),
                C("pest_control", "Pest Control", ChallengeType.KillEnemies, 1000, Item("Flame")),
                C("exterminator", "Exterminator", ChallengeType.KillEnemies, 2500, Weapon("Rocket Launcher")),
                C("hoarder", "Hoarder", ChallengeType.CollectMaterials, 750, Item("Diamond")),
                C("grown_up", "Grown Up", ChallengeType.ReachLevel, 12, Item("Scroll")),
                C("blacksmith", "Blacksmith", ChallengeType.OwnTier4Weapon, 1, Item("World Ender")),
                C("dragon_slayer", "Dragon Slayer", ChallengeType.WinRun, 1, Item("Crown")),
                C("cursed_ground", "Cursed Ground", ChallengeType.WinOnDanger, 2, Item("The Ring")),
                C("nightmare", "Nightmare", ChallengeType.WinOnDanger, 5, Item("Magic Egg")),
                C("win_pete", "Pete's Harvest", ChallengeType.WinWithCharacter, 1, Weapon("Sniper"), Character("Pete")),
                C("win_lucy", "Lucy's Harvest", ChallengeType.WinWithCharacter, 1, Weapon("Harpoon"), Character("Lucy")),
                C("win_hugo", "Hugo's Harvest", ChallengeType.WinWithCharacter, 1, Weapon("Blunderbuss"), Character("Hugo")),
                C("win_maggie", "Maggie's Harvest", ChallengeType.WinWithCharacter, 1, Weapon("Magic Staff"), Character("Maggie")),
            };
            foreach (var c in set.challenges)
                if (c.reward == null) Debug.LogWarning($"[BrotatoLike] Défi {c.id} : récompense introuvable");
        });

        static List<T> Find<T>(System.Func<T, bool> filter) where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/GAME/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null && filter(a)).ToList();

        static Sprite LockSprite()
        {
            var path = AssetDatabase.GUIDToAssetPath(LockGuid);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ================================================================ Menu principal

        public static bool PatchMenuScene()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
            if (menu == null) return false;
            var so = new SerializedObject(menu);
            if (so.FindProperty("challenges").objectReferenceValue is ChallengesMenu existing) return UseCardPrefab(existing);

            var menuPanel = so.FindProperty("menuPanel").objectReferenceValue as GameObject;
            var newGame = so.FindProperty("newGameButton").objectReferenceValue as Button;
            if (menuPanel == null || newGame == null) return false;

            // Bouton « Challenges » sous « Play online » : 6 boutons un peu plus serrés
            var column = (RectTransform)newGame.transform.parent;
            var button = UIFactory.Button(column, "Challenges", 0, 0, 440, 76, Accent);
            UIFactory.ButtonLabel(button, 36).text = "Challenges";
            button.gameObject.AddComponent<ButtonSound>();
            var order = new[] { "continueButton", "newGameButton", "onlineButton", null, "optionsButton", "quitButton" };
            float y = 0f;
            foreach (var field in order)
            {
                var b = field == null ? button : so.FindProperty(field).objectReferenceValue as Button;
                if (b == null) continue;
                var rt = (RectTransform)b.transform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 76f);
                y += 86f;
            }
            column.sizeDelta = new Vector2(column.sizeDelta.x, y);

            so.FindProperty("challengesButton").objectReferenceValue = button;
            so.FindProperty("challenges").objectReferenceValue = BuildScreen(menuPanel.transform.parent);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ================================================================ Carte (prefab)

        /// <summary>Prefab d'une carte : bouton + icône de la récompense + gros cadenas au milieu.</summary>
        static ChallengeCardView BuildCardPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ChallengeCardView>(CardPrefabPath);
            if (existing != null) return existing;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CardPrefabPath));

            var temp = new GameObject("Temp", typeof(RectTransform));
            var button = UIFactory.Button(temp.transform, "ChallengeCard", 0, 0, CardSize, CardSize, Accent);
            button.gameObject.AddComponent<ButtonSound>();
            var icon = UIFactory.AddImage(UIFactory.Rect(button.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f)), Color.white);
            icon.preserveAspect = true;
            var lockIcon = UIFactory.AddImage(UIFactory.Rect(button.transform, "Lock", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f)), Color.white);
            lockIcon.sprite = LockSprite();
            lockIcon.preserveAspect = true;

            var view = button.gameObject.AddComponent<ChallengeCardView>();
            view.button = button;
            view.icon = icon;
            view.lockIcon = lockIcon;
            button.transform.SetParent(null, false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(button.gameObject, CardPrefabPath).GetComponent<ChallengeCardView>();
            Object.DestroyImmediate(button.gameObject);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>Grille vide qui recevra les cartes au lancement.</summary>
        static RectTransform BuildCardGrid(RectTransform panel)
        {
            var grid = UIFactory.TopLeft(panel, "Cards", 40f, 120f, CardsPerRow * CardSize + (CardsPerRow - 1) * CardGap, 640f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(CardSize, CardSize);
            layout.spacing = new Vector2(CardGap, CardGap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = CardsPerRow;
            layout.childAlignment = TextAnchor.UpperLeft;
            return grid;
        }

        /// <summary>Écran déjà construit avec des cartes fixes : on les remplace par la grille + le prefab.</summary>
        static bool UseCardPrefab(ChallengesMenu menu)
        {
            var so = new SerializedObject(menu);
            if (so.FindProperty("cardGrid").objectReferenceValue != null) return false;
            var panel = so.FindProperty("panel").objectReferenceValue as RectTransform;
            if (panel == null) return false;

            for (int i = panel.childCount - 1; i >= 0; i--)
                if (panel.GetChild(i).name.StartsWith("Card ")) Object.DestroyImmediate(panel.GetChild(i).gameObject);

            so.FindProperty("cardPrefab").objectReferenceValue = BuildCardPrefab();
            so.FindProperty("cardGrid").objectReferenceValue = BuildCardGrid(panel);
            so.FindProperty("cardsPerRow").intValue = CardsPerRow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static ChallengesMenu BuildScreen(Transform parent)
        {
            var root = UIFactory.Stretch(parent, "Challenges");
            UIFactory.AddImage(root, new Color(0f, 0f, 0f, 0.75f), raycast: true);
            var menu = root.gameObject.AddComponent<ChallengesMenu>();

            const float w = 1560f, h = 820f;
            var panel = UIFactory.Rect(root, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            UIFactory.AddImage(panel, UIFactory.PanelColor);
            UIFactory.AddImage(UIFactory.TopLeft(panel, "Bar", 0, 0, w, 8), Accent);
            UIFactory.Text(panel, "Title", 40, 26, 700, 70, 60, TextAlignmentOptions.Left).text = "CHALLENGES";
            var counter = UIFactory.Text(panel, "Counter", w - 340, 34, 300, 60, 40, TextAlignmentOptions.Right);
            counter.color = Accent;
            counter.text = "0 / 0";

            // ---- Cartes (gauche) : créées au lancement depuis le prefab
            var grid = BuildCardGrid(panel);

            // ---- Détail (droite)
            const float dx = 880f, dw = 640f;
            UIFactory.AddImage(UIFactory.TopLeft(panel, "DetailBackground", dx - 20f, 120f, dw + 20f, h - 200f), new Color(0.12f, 0.12f, 0.16f, 1f));
            var title = UIFactory.Text(panel, "ChallengeName", dx, 140, dw - 20, 60, 46, TextAlignmentOptions.Left);
            title.text = "Challenge";
            var goal = UIFactory.Text(panel, "Goal", dx, 210, dw - 20, 90, 30, TextAlignmentOptions.TopLeft, wrap: true);
            goal.text = "Goal";
            var progress = UIFactory.Text(panel, "Progress", dx, 305, dw - 20, 44, 28, TextAlignmentOptions.Left);
            progress.text = "Best: 0 / 0";
            var rewardLabel = UIFactory.Text(panel, "RewardLabel", dx, 375, dw - 20, 36, 24, TextAlignmentOptions.Left);
            rewardLabel.text = "REWARD";
            rewardLabel.color = new Color(1f, 1f, 1f, 0.5f);
            var rewardIcon = UIFactory.Icon(panel, "RewardIcon", dx, 420, 110);
            rewardIcon.preserveAspect = true;
            var rewardName = UIFactory.Text(panel, "RewardName", dx + 130, 440, dw - 150, 60, 36, TextAlignmentOptions.Left);
            rewardName.text = "Reward";
            var rewardText = UIFactory.Text(panel, "RewardText", dx, 545, dw - 20, 140, 22, TextAlignmentOptions.TopLeft, wrap: true);
            rewardText.text = "Description";

            var footer = UIFactory.Text(panel, "Footer", 0, h - 60, w, 40, 22, TextAlignmentOptions.Center);
            footer.color = new Color(1f, 1f, 1f, 0.6f);
            footer.text = "Esc / B: back";

            var so = new SerializedObject(menu);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("counter").objectReferenceValue = counter;
            so.FindProperty("cardPrefab").objectReferenceValue = BuildCardPrefab();
            so.FindProperty("cardGrid").objectReferenceValue = grid;
            so.FindProperty("cardsPerRow").intValue = CardsPerRow;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("goal").objectReferenceValue = goal;
            so.FindProperty("progress").objectReferenceValue = progress;
            so.FindProperty("rewardIcon").objectReferenceValue = rewardIcon;
            so.FindProperty("rewardName").objectReferenceValue = rewardName;
            so.FindProperty("rewardText").objectReferenceValue = rewardText;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return menu;
        }

        // ================================================================ Jeu : notification

        public static bool PatchGameScene()
        {
            if (Object.FindFirstObjectByType<ChallengeToast>(FindObjectsInactive.Include) != null) return false;
            var hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
            if (hud == null) return false;

            // Canvas à part, au-dessus de la boutique et de la pause : toujours actif
            var canvas = UIFactory.CreateCanvas(hud.transform, "Toast Canvas", 40, interactive: false);
            var component = canvas.gameObject.AddComponent<ChallengeToast>();
            var toast = UIFactory.Rect(canvas, "Toast", new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(760f, 110f));
            UIFactory.AddImage(toast, new Color(0.07f, 0.07f, 0.09f, 0.95f));
            UIFactory.AddImage(UIFactory.TopLeft(toast, "Bar", 0, 0, 760, 6), new Color(0.45f, 1f, 0.5f));
            var icon = UIFactory.Icon(toast, "Icon", 20, 18, 76);
            icon.preserveAspect = true;
            var text = UIFactory.Text(toast, "Text", 115, 12, 625, 90, 28, TextAlignmentOptions.Left, wrap: true);
            text.text = "Challenge complete!";

            var so = new SerializedObject(component);
            so.FindProperty("toast").objectReferenceValue = toast;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
