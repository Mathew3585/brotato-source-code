using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Difficulté (« Danger 0 à 5 ») : crée l'asset DifficultySet (s'il n'existe pas) et l'écran de choix
    /// dans la scène MainMenu (s'il manque), branché sur MainMenu et OnlineMenu.
    /// </summary>
    static class DangerBuilder
    {
        const string AssetPath = "Assets/GAME/Data/Resources/DifficultySet.asset";

        public static DifficultySet BuildAsset() => PrototypeBuilder.LoadOrCreate<DifficultySet>(AssetPath, set =>
        {
            set.levels = new List<DifficultySet.Level>
            {
                Level("Danger 0", "A quiet night on the farm.", new Color(0.5f, 0.9f, 0.5f),
                    0.8f, 0.75f, 0.9f, 1f),
                Level("Danger 1", "The farm as it was meant to be.", new Color(0.6f, 0.85f, 1f),
                    1f, 1f, 1f, 1f),
                Level("Danger 2", "The dead are getting restless.", new Color(1f, 0.85f, 0.35f),
                    1.12f, 1.15f, 1.05f, 1f, 12, 17),
                Level("Danger 3", "Something is stirring in the fields.", new Color(1f, 0.6f, 0.25f),
                    1.25f, 1.3f, 1.1f, 1f, 8, 12, 15, 18),
                Level("Danger 4", "The curse grows stronger.", new Color(1f, 0.4f, 0.3f),
                    1.4f, 1.5f, 1.2f, 0.9f, 6, 9, 12, 14, 16, 18),
                Level("Danger 5", "Only the bravest farmers survive.", new Color(0.85f, 0.35f, 0.95f),
                    1.55f, 1.7f, 1.25f, 0.85f, 6, 9, 12, 14, 16, 18),
            };
            set.levels[5].doubleBoss = true;
        });

        static DifficultySet.Level Level(string name, string description, Color color,
            float health, float damage, float count, float materials, params int[] eliteWaves) => new()
        {
            displayName = name,
            description = description,
            color = color,
            enemyHealth = health,
            enemyDamage = damage,
            enemyCount = count,
            materials = materials,
            eliteWaves = new List<int>(eliteWaves),
        };

        /// <summary>Scène MainMenu ouverte : ajoute l'écran de choix s'il manque.</summary>
        public static bool PatchMenuScene()
        {
            var mainMenu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
            if (mainMenu == null) return false;
            var menuSo = new SerializedObject(mainMenu);
            if (menuSo.FindProperty("dangerMenu").objectReferenceValue != null) return false;
            var lobby = menuSo.FindProperty("lobbyPanel").objectReferenceValue as GameObject;
            if (lobby == null) return false;

            var danger = BuildScreen(lobby.transform.parent);
            menuSo.FindProperty("dangerMenu").objectReferenceValue = danger;
            menuSo.ApplyModifiedPropertiesWithoutUndo();

            var online = Object.FindFirstObjectByType<OnlineMenu>(FindObjectsInactive.Include);
            if (online != null)
            {
                var so = new SerializedObject(online);
                so.FindProperty("dangerMenu").objectReferenceValue = danger;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return true;
        }

        static DangerMenu BuildScreen(Transform parent)
        {
            var accent = UIFactory.Accent;
            var root = UIFactory.Stretch(parent, "DangerSelect");
            root.SetAsLastSibling();
            UIFactory.AddImage(root, new Color(0f, 0f, 0f, 0.75f), raycast: true);
            var menu = root.gameObject.AddComponent<DangerMenu>();

            const float w = 1200f, h = 620f;
            var panel = UIFactory.Rect(root, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            UIFactory.AddImage(panel, UIFactory.PanelColor);
            UIFactory.AddImage(UIFactory.TopLeft(panel, "Bar", 0, 0, w, 8), accent);
            UIFactory.Text(panel, "Title", 0, 30, w, 70, 60, TextAlignmentOptions.Top).text = "CHOOSE DANGER";

            const int count = 6;
            const float size = 150f, gap = 30f;
            float x = (w - (count * size + (count - 1) * gap)) / 2f;
            var buttons = new Button[count];
            var labels = new TextMeshProUGUI[count];
            for (int i = 0; i < count; i++)
            {
                buttons[i] = UIFactory.Button(panel, "Danger " + i, x + i * (size + gap), 130, size, size, accent);
                labels[i] = UIFactory.ButtonLabel(buttons[i], 72);
                labels[i].text = i.ToString();
                buttons[i].gameObject.AddComponent<ButtonSound>();
            }

            var levelName = UIFactory.Text(panel, "LevelName", 0, 310, w, 60, 44, TextAlignmentOptions.Center);
            levelName.text = "Danger 0";
            var description = UIFactory.Text(panel, "Description", 60, 375, w - 120, 160, 26, TextAlignmentOptions.Top, wrap: true);
            description.text = "Description";
            var footer = UIFactory.Text(panel, "Footer", 0, 560, w, 40, 22, TextAlignmentOptions.Center);
            footer.color = new Color(1f, 1f, 1f, 0.6f);
            footer.text = "Enter / A: start   -   Esc / B: back";

            var so = new SerializedObject(menu);
            so.FindProperty("panel").objectReferenceValue = panel;
            GameUIBuilder.SetArray(so, "buttons", buttons);
            GameUIBuilder.SetArray(so, "labels", labels);
            so.FindProperty("levelName").objectReferenceValue = levelName;
            so.FindProperty("description").objectReferenceValue = description;
            so.FindProperty("footer").objectReferenceValue = footer;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return menu;
        }
    }
}
