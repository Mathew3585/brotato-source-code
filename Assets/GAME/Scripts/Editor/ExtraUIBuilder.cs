using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Ajoute dans les scènes existantes ce qui manque : panneau Options (menu + pause), boutons Continuer / Options,
    /// écran de fin de partie. Ne touche pas à ce qui existe déjà (sauf replacer les boutons des menus).
    /// </summary>
    static class ExtraUIBuilder
    {
        static readonly Color Accent = UIFactory.Accent;

        // ================================================================ Scène de jeu

        public static bool PatchGameScene()
        {
            var hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
            if (hud == null) return false;
            var so = new SerializedObject(hud);
            bool changed = false;

            if (so.FindProperty("endScreen").objectReferenceValue == null)
            {
                so.FindProperty("endScreen").objectReferenceValue = BuildEndScreen(hud.transform);
                changed = true;
            }

            var pauseRoot = so.FindProperty("pauseRoot").objectReferenceValue as GameObject;
            if (pauseRoot != null && so.FindProperty("options").objectReferenceValue == null)
            {
                so.FindProperty("options").objectReferenceValue = BuildOptions(pauseRoot.transform);
                changed = true;
            }

            var resume = so.FindProperty("resumeButton").objectReferenceValue as Button;
            var menu = so.FindProperty("menuButton").objectReferenceValue as Button;
            if (resume != null && menu != null && so.FindProperty("optionsButton").objectReferenceValue == null)
            {
                // Reprendre / Options / Menu principal
                var column = resume.transform.parent;
                var options = UIFactory.Button(column, "Options", 0, 100, 440, 80, Accent);
                UIFactory.ButtonLabel(options, 34).text = "Options";
                options.gameObject.AddComponent<ButtonSound>();
                ((RectTransform)menu.transform).anchoredPosition = new Vector2(0f, -200f);
                ((RectTransform)column).sizeDelta = new Vector2(440f, 290f);
                so.FindProperty("optionsButton").objectReferenceValue = options;
                changed = true;
            }

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        static EndScreenView BuildEndScreen(Transform parent)
        {
            var canvas = UIFactory.CreateCanvas(parent, "End Canvas", 25, interactive: false);
            var view = canvas.gameObject.AddComponent<EndScreenView>();
            UIFactory.AddImage(UIFactory.Stretch(canvas, "Dim"), new Color(0f, 0f, 0f, 0.75f));

            view.panel = UIFactory.Stretch(canvas, "Panel");
            view.title = UIFactory.Text(UIFactory.Rect(view.panel, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1600f, 130f)),
                110, TextAlignmentOptions.Center);
            view.title.text = "GAME OVER";
            view.subtitle = UIFactory.Text(UIFactory.Rect(view.panel, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1600f, 50f)),
                36, TextAlignmentOptions.Center);
            view.subtitle.text = "Reached wave 5 / 20";

            const float width = 420f, height = 560f, gap = 30f;
            float total = 4 * width + 3 * gap;
            view.players = new EndScreenView.PlayerColumn[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -total / 2f + i * (width + gap) + width / 2f;
                var col = UIFactory.Rect(view.panel, "Player " + (i + 1), new Vector2(0.5f, 0.5f), new Vector2(x, -40f), new Vector2(width, height));
                UIFactory.AddImage(col, new Color(0.07f, 0.07f, 0.09f, 0.95f));
                var c = new EndScreenView.PlayerColumn
                {
                    root = col.gameObject,
                    colorBar = UIFactory.AddImage(UIFactory.TopLeft(col, "Bar", 0, 0, width, 8), Session.ColorOf(i)),
                    title = UIFactory.Text(col, "Title", 0, 20, width, 44, 34, TextAlignmentOptions.Top),
                    stats = UIFactory.Text(col, "Stats", 30, 80, width - 60, height - 100, 26, TextAlignmentOptions.TopLeft, wrap: true),
                };
                c.title.text = $"P{i + 1} - Character";
                c.title.color = Session.ColorOf(i);
                c.stats.text = "Enemies killed: 0\nDamage dealt: 0";
                view.players[i] = c;
            }

            view.hint = UIFactory.Text(UIFactory.Rect(view.panel, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1600f, 50f)),
                30, TextAlignmentOptions.Center);
            view.hint.text = "Enter / A: restart   •   Esc / B: main menu";

            canvas.gameObject.SetActive(false);
            return view;
        }

        // ================================================================ Menu principal

        public static bool PatchMenuScene()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
            if (menu == null) return false;
            var so = new SerializedObject(menu);
            var menuPanel = so.FindProperty("menuPanel").objectReferenceValue as GameObject;
            var newGame = so.FindProperty("newGameButton").objectReferenceValue as Button;
            var quit = so.FindProperty("quitButton").objectReferenceValue as Button;
            if (menuPanel == null || newGame == null || quit == null) return false;
            bool changed = false;

            if (so.FindProperty("continueButton").objectReferenceValue == null)
            {
                // Continuer / Nouvelle partie / Options / Quitter
                var column = (RectTransform)newGame.transform.parent;
                column.sizeDelta = new Vector2(440f, 390f);
                column.anchoredPosition = new Vector2(column.anchoredPosition.x, -250f);

                var cont = UIFactory.Button(column, "Continue", 0, 0, 440, 84, Accent);
                UIFactory.ButtonLabel(cont, 38).text = "Continue";
                cont.gameObject.AddComponent<ButtonSound>();
                ((RectTransform)newGame.transform).anchoredPosition = new Vector2(0f, -100f);
                var options = UIFactory.Button(column, "Options", 0, 200, 440, 84, Accent);
                UIFactory.ButtonLabel(options, 38).text = "Options";
                options.gameObject.AddComponent<ButtonSound>();
                ((RectTransform)quit.transform).anchoredPosition = new Vector2(0f, -300f);

                so.FindProperty("continueButton").objectReferenceValue = cont;
                so.FindProperty("optionsButton").objectReferenceValue = options;
                changed = true;
            }

            if (so.FindProperty("options").objectReferenceValue == null)
            {
                so.FindProperty("options").objectReferenceValue = BuildOptions(menuPanel.transform.parent);
                changed = true;
            }

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // ================================================================ Options

        static OptionsMenu BuildOptions(Transform parent)
        {
            var root = UIFactory.Stretch(parent, "Options");
            UIFactory.AddImage(root, new Color(0f, 0f, 0f, 0.6f), raycast: true);
            var menu = root.gameObject.AddComponent<OptionsMenu>();

            const float w = 760f, h = 600f;
            var panel = UIFactory.Rect(root, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            UIFactory.AddImage(panel, UIFactory.PanelColor);
            UIFactory.AddImage(UIFactory.TopLeft(panel, "Bar", 0, 0, w, 8), Accent);
            UIFactory.Text(panel, "Title", 0, 30, w, 70, 64, TextAlignmentOptions.Top).text = "OPTIONS";

            UIFactory.Text(panel, "SfxLabel", 50, 130, 300, 50, 32, TextAlignmentOptions.Left).text = "Sound effects";
            var sfx = UIFactory.Slider(panel, "SfxSlider", 340, 140, 280, 34, Accent);
            var sfxValue = UIFactory.Text(panel, "SfxValue", 630, 130, 90, 50, 30, TextAlignmentOptions.Right);
            sfxValue.text = "80%";

            UIFactory.Text(panel, "MusicLabel", 50, 210, 300, 50, 32, TextAlignmentOptions.Left).text = "Music";
            var music = UIFactory.Slider(panel, "MusicSlider", 340, 220, 280, 34, Accent);
            var musicValue = UIFactory.Text(panel, "MusicValue", 630, 210, 90, 50, 30, TextAlignmentOptions.Right);
            musicValue.text = "60%";

            var fullscreen = UIFactory.Toggle(panel, "Fullscreen", 40, 300, w - 80, 70, "Fullscreen", Accent);
            var shake = UIFactory.Toggle(panel, "ScreenShake", 40, 385, w - 80, 70, "Screen shake", Accent);

            var back = UIFactory.Button(panel, "Back", w / 2f - 160f, 500, 320, 70, Accent);
            UIFactory.ButtonLabel(back, 34).text = "Back";

            foreach (var s in new Selectable[] { sfx, music, fullscreen, shake, back }) s.gameObject.AddComponent<ButtonSound>();

            var so = new SerializedObject(menu);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("sfxSlider").objectReferenceValue = sfx;
            so.FindProperty("sfxValue").objectReferenceValue = sfxValue;
            so.FindProperty("musicSlider").objectReferenceValue = music;
            so.FindProperty("musicValue").objectReferenceValue = musicValue;
            so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreen;
            so.FindProperty("shakeToggle").objectReferenceValue = shake;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return menu;
        }
    }
}
