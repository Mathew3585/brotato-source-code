using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Crée dans la scène MainMenu l'UI du menu principal et de l'écran de sélection des personnages.
    /// Une fois créée, elle se modifie librement dans l'éditeur.
    /// </summary>
    static class MenuUIBuilder
    {
        public static void Build(MainMenu menu, string gameTitle)
        {
            GameUIBuilder.Clear(menu.transform);
            var canvas = UIFactory.CreateCanvas(menu.transform, "Menu Canvas", 0, interactive: true);
            UIFactory.AddImage(UIFactory.Stretch(canvas, "Background"), new Color(0.07f, 0.11f, 0.09f));

            var so = new SerializedObject(menu);
            BuildMainPanel(canvas, menu, so, gameTitle);
            BuildLobby(canvas, so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);
        }

        static void BuildMainPanel(RectTransform canvas, MainMenu menu, SerializedObject so, string gameTitle)
        {
            var panel = UIFactory.Stretch(canvas, "Menu");

            var title = UIFactory.Text(UIFactory.Rect(panel, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1600f, 140f)),
                104, TextAlignmentOptions.Center);
            title.text = gameTitle;
            title.color = new Color(1f, 0.85f, 0.35f);

            var subtitle = UIFactory.Text(UIFactory.Rect(panel, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1400f, 50f)),
                30, TextAlignmentOptions.Center);
            subtitle.text = "Survive the undead waves — 1 to 4 players";
            subtitle.color = new Color(1f, 1f, 1f, 0.7f);

            // Les personnages qui courent sous le titre
            var characters = menu.characters ?? new CharacterData[0];
            var farmers = new Image[characters.Length];
            for (int i = 0; i < characters.Length; i++)
            {
                float x = (i - (characters.Length - 1) / 2f) * 190f;
                farmers[i] = UIFactory.AddImage(UIFactory.Rect(panel, "Character " + (i + 1), new Vector2(0.5f, 0.5f), new Vector2(x, 90f), new Vector2(150f, 150f)), Color.white);
                farmers[i].preserveAspect = true;
                if (characters[i] != null && characters[i].idleFrames.Length > 0) farmers[i].sprite = characters[i].idleFrames[0];
            }

            var column = UIFactory.Rect(panel, "Buttons", new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(440f, 190f));
            var accent = UIFactory.Accent;
            var newGame = UIFactory.Button(column, "NewGame", 0, 0, 440, 84, accent);
            UIFactory.ButtonLabel(newGame, 38).text = "New game";
            var quit = UIFactory.Button(column, "Quit", 0, 104, 440, 84, accent);
            UIFactory.ButtonLabel(quit, 38).text = "Quit";
            so.FindProperty("menuPanel").objectReferenceValue = panel.gameObject;
            so.FindProperty("title").objectReferenceValue = title.transform;
            so.FindProperty("newGameButton").objectReferenceValue = newGame;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            GameUIBuilder.SetArray(so, "menuFarmers", farmers);
        }

        static void BuildLobby(RectTransform canvas, SerializedObject so)
        {
            var panel = UIFactory.Stretch(canvas, "Lobby");

            var title = UIFactory.Text(UIFactory.Rect(panel, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1600f, 80f)),
                60, TextAlignmentOptions.Center);
            title.text = "CHOOSE YOUR CHARACTERS";
            title.color = new Color(1f, 0.85f, 0.35f);

            var subtitle = UIFactory.Text(UIFactory.Rect(panel, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1600f, 40f)),
                26, TextAlignmentOptions.Center);
            subtitle.text = "Keyboard: Enter to join   •   Gamepad: A to join   •   4 players max";
            subtitle.color = new Color(1f, 1f, 1f, 0.7f);

            const float width = 420f, height = 700f, gap = 30f;
            float total = 4 * width + 3 * gap;
            var slots = new LobbySlotView[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -total / 2f + i * (width + gap) + width / 2f;
                slots[i] = BuildSlot(panel, i, x, width, height);
            }

            var footer = UIFactory.Text(UIFactory.Rect(panel, "Footer", new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(1600f, 50f)),
                30, TextAlignmentOptions.Center);
            footer.text = "Esc / B: back to menu";

            so.FindProperty("lobbyPanel").objectReferenceValue = panel.gameObject;
            so.FindProperty("footer").objectReferenceValue = footer;
            GameUIBuilder.SetArray(so, "slots", slots);

            panel.gameObject.SetActive(false);
        }

        static LobbySlotView BuildSlot(RectTransform parent, int index, float x, float width, float height)
        {
            var color = Session.ColorOf(index);
            var root = UIFactory.Rect(parent, "Slot J" + (index + 1), new Vector2(0.5f, 0.5f), new Vector2(x, -20f), new Vector2(width, height));
            UIFactory.AddImage(root, new Color(0.05f, 0.05f, 0.07f, 0.9f));
            var v = root.gameObject.AddComponent<LobbySlotView>();

            v.colorBar = UIFactory.AddImage(UIFactory.TopLeft(root, "Bar", 0, 0, width, 8), color);
            v.header = UIFactory.Text(root, "Header", 0, 18, width, 40, 30, TextAlignmentOptions.Top);
            v.header.text = "P" + (index + 1);
            v.header.color = color;

            // Emplacement libre
            var empty = UIFactory.Stretch(root, "Empty");
            v.emptyGroup = empty.gameObject;
            v.emptyText = UIFactory.Text(UIFactory.Stretch(empty, "Text"), 34, TextAlignmentOptions.Center);
            v.emptyText.text = "Press\n<color=#FFD966>Enter</color> or <color=#FFD966>A</color>\nto join";
            v.emptyText.color = new Color(1f, 1f, 1f, 0.5f);

            // Emplacement occupé
            var filled = UIFactory.Stretch(root, "Filled");
            v.filledGroup = filled.gameObject;
            v.portrait = UIFactory.Icon(filled, "Portrait", width / 2f - 100f, 70, 200);
            v.leftArrow = UIFactory.Button(filled, "Left", 24, 140, 60, 70, color);
            UIFactory.ButtonLabel(v.leftArrow, 48).text = "<";
            v.rightArrow = UIFactory.Button(filled, "Right", width - 84, 140, 60, 70, color);
            UIFactory.ButtonLabel(v.rightArrow, 48).text = ">";

            v.characterName = UIFactory.Text(filled, "Name", 0, 282, width, 50, 42, TextAlignmentOptions.Top);
            v.characterName.text = "Character";
            v.stats = UIFactory.Text(filled, "Stats", 40, 345, width - 80, 190, 23, TextAlignmentOptions.TopLeft, wrap: true);
            v.stats.fontStyle = FontStyles.Normal;
            v.stats.text = "+10 Max HP";
            v.weaponIcon = UIFactory.Icon(filled, "WeaponIcon", 40, 540, 56);
            v.weaponName = UIFactory.Text(filled, "Weapon", 110, 552, width - 140, 40, 24);
            v.weaponName.text = "Weapon: Rifle";
            v.status = UIFactory.Text(filled, "Status", 0, 610, width, 50, 34, TextAlignmentOptions.Top);
            v.status.text = "Enter: ready";
            filled.gameObject.SetActive(false);
            return v;
        }
    }
}
