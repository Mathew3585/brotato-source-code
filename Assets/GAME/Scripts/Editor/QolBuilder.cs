using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Confort : infobulles des armes / objets dans la boutique, écran des contrôles (dans les options),
    /// flèches vers le boss et les élites hors écran. Ajoute seulement ce qui manque dans les scènes.
    /// </summary>
    static class QolBuilder
    {
        const string ArrowGuid = "24726326f0d24dc41875ec34f94e95b2";
        const int IndicatorCount = 6;
        static readonly Color Accent = UIFactory.Accent;

        public static bool PatchGameScene()
        {
            bool changed = false;
            foreach (var column in Object.FindObjectsByType<ShopColumnView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= PatchShopColumn(column);

            var hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
            if (hud != null && hud.GetComponent<OffscreenIndicators>() == null)
            {
                BuildIndicators(hud);
                changed = true;
            }

            foreach (var options in Object.FindObjectsByType<OptionsMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= PatchOptions(options) | PatchFpsOption(options);

            changed |= FixTileSeams();
            changed |= UpdateKeyboardControls();
            changed |= UseGameControlsForSceneUI();
            return changed;
        }

        /// <summary>
        /// Lignes noires entre les tuiles du sol : les tuiles se chevauchent très légèrement (invisible)
        /// et chaque tuile est un rectangle complet (pas de découpe au plus près des pixels).
        /// </summary>
        static bool FixTileSeams()
        {
            const string tilesGuid = "9ee30eb7d2f1f404790266ee96a67099";
            var path = AssetDatabase.GUIDToAssetPath(tilesGuid);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteExtrude != 0)
                {
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteExtrude = 0;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
            }

            var grid = Object.FindFirstObjectByType<Grid>(FindObjectsInactive.Include);
            if (grid == null || grid.cellGap != Vector3.zero) return false;
            grid.cellGap = new Vector3(-0.02f, -0.02f, 0f);
            EditorUtility.SetDirty(grid);
            return true;
        }

        public static bool PatchMenuScene()
        {
            bool changed = false;
            foreach (var options in Object.FindObjectsByType<OptionsMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= PatchOptions(options) | PatchFpsOption(options);
            changed |= UpdateKeyboardControls();
            changed |= UseGameControlsForSceneUI();
            return changed;
        }

        /// <summary>
        /// L'EventSystem de la scène utilisait les actions UI par défaut d'Unity (Espace ne valide pas) :
        /// on le branche sur les actions UI de GameControls (Submit = Entrée / Espace / A).
        /// </summary>
        static bool UseGameControlsForSceneUI()
        {
            const string inputPath = "Assets/GAME/Input/GameControls.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(inputPath);
            if (asset == null) return false;
            var references = new System.Collections.Generic.Dictionary<string, UnityEngine.InputSystem.InputActionReference>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(inputPath))
                if (o is UnityEngine.InputSystem.InputActionReference r && r.action != null)
                    references[r.action.actionMap.name + "/" + r.action.name] = r;

            bool changed = false;
            foreach (var module in Object.FindObjectsByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(module);
                if (so.FindProperty("m_ActionsAsset").objectReferenceValue == asset) continue;
                so.FindProperty("m_ActionsAsset").objectReferenceValue = asset;
                void Set(string property, string action)
                {
                    var p = so.FindProperty(property);
                    if (p != null) p.objectReferenceValue = action != null && references.TryGetValue(action, out var r) ? r : null;
                }
                Set("m_PointAction", "UI/Point");
                Set("m_MoveAction", "UI/Navigate");
                Set("m_SubmitAction", "UI/Submit");
                Set("m_CancelAction", "UI/Cancel");
                Set("m_LeftClickAction", "UI/Click");
                Set("m_MiddleClickAction", null);
                Set("m_RightClickAction", null);
                Set("m_ScrollWheelAction", null);
                Set("m_TrackedDevicePositionAction", null);
                Set("m_TrackedDeviceOrientationAction", null);
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }

        // ================================================================ Infobulles de la boutique

        static bool PatchShopColumn(ShopColumnView v)
        {
            bool changed = false;
            float inner = v.Rect.sizeDelta.x - 32f;

            if (v.tooltipRoot == null)
            {
                // Par-dessus les stats, en bas de la colonne
                var root = UIFactory.TopLeft(v.transform, "Tooltip", 12, 898, inner + 8, 138);
                UIFactory.AddImage(root, new Color(0.12f, 0.12f, 0.16f, 0.98f));
                v.tooltipTitle = UIFactory.Text(root, "Title", 12, 8, inner - 16, 32, 24);
                v.tooltipTitle.text = "Item name";
                v.tooltipText = UIFactory.Text(root, "Text", 12, 42, inner - 16, 92, 17, TextAlignmentOptions.TopLeft, wrap: true);
                v.tooltipText.text = "Description";
                v.tooltipRoot = root.gameObject;
                root.gameObject.SetActive(false);
                changed = true;
            }

            foreach (var slot in v.weaponSlots)
            {
                if (slot.GetComponent<TooltipSource>() != null) continue;
                slot.gameObject.AddComponent<TooltipSource>();
                changed = true;
            }

            if (v.itemCounts == null || v.itemCounts.Length != v.itemIcons.Length)
            {
                v.itemCounts = new TextMeshProUGUI[v.itemIcons.Length];
                for (int i = 0; i < v.itemIcons.Length; i++)
                {
                    var rt = UIFactory.Rect(v.itemIcons[i].transform, "Count", new Vector2(1f, 0f), new Vector2(4f, -2f), new Vector2(40f, 18f));
                    rt.pivot = new Vector2(1f, 0f);
                    var count = UIFactory.Text(rt, 16, TextAlignmentOptions.BottomRight);
                    count.color = new Color(1f, 0.85f, 0.35f);
                    count.text = "";
                    v.itemCounts[i] = count;
                }
                changed = true;
            }

            foreach (var icon in v.itemIcons)
            {
                if (icon.GetComponent<TooltipSource>() == null)
                {
                    icon.gameObject.AddComponent<TooltipSource>();
                    changed = true;
                }
                if (icon.GetComponent<Selectable>() != null) continue;
                // Sélectionnable à la manette (aucune action : juste l'infobulle)
                icon.raycastTarget = true;
                var button = icon.gameObject.AddComponent<Button>();
                button.targetGraphic = icon;
                button.colors = new ColorBlock
                {
                    normalColor = Color.white,
                    highlightedColor = new Color(1f, 0.9f, 0.6f),
                    selectedColor = new Color(1f, 0.85f, 0.4f),
                    pressedColor = Color.white,
                    disabledColor = Color.gray,
                    colorMultiplier = 1f,
                    fadeDuration = 0.05f,
                };
                icon.gameObject.AddComponent<ButtonSound>();
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(v);
            return changed;
        }

        // ================================================================ Indicateurs hors écran

        static void BuildIndicators(HUD hud)
        {
            var hudRoot = new SerializedObject(hud).FindProperty("hudRoot").objectReferenceValue as GameObject;
            if (hudRoot == null) return;
            var canvasRect = (RectTransform)hudRoot.transform;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(ArrowGuid));

            var container = UIFactory.Stretch(canvasRect, "OffscreenIndicators");
            var arrows = new Image[IndicatorCount];
            for (int i = 0; i < IndicatorCount; i++)
            {
                var rt = UIFactory.Rect(container, "Arrow " + i, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
                arrows[i] = UIFactory.AddImage(rt, Color.red);
                arrows[i].sprite = sprite;
                arrows[i].preserveAspect = true;
                rt.gameObject.SetActive(false);
            }

            var indicators = hud.gameObject.AddComponent<OffscreenIndicators>();
            var so = new SerializedObject(indicators);
            so.FindProperty("canvasRect").objectReferenceValue = canvasRect;
            GameUIBuilder.SetArray(so, "arrows", arrows);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ Écran des contrôles

        /// <summary>
        /// Ligne « FPS limit » sous la musique : agrandit le panneau et descend ce qui suit (plein écran, secousses, boutons).
        /// </summary>
        static bool PatchFpsOption(OptionsMenu options)
        {
            var so = new SerializedObject(options);
            if (so.FindProperty("fpsSlider").objectReferenceValue != null) return false;
            var panel = (RectTransform)so.FindProperty("panel").objectReferenceValue;
            if (panel == null) return false;

            const float rowY = 290f, shift = 80f;
            foreach (RectTransform child in panel)
                if (-child.anchoredPosition.y >= rowY - 1f)
                    child.anchoredPosition -= new Vector2(0f, shift);
            panel.sizeDelta += new Vector2(0f, shift);

            UIFactory.Text(panel, "FpsLabel", 50, rowY, 300, 50, 32, TextAlignmentOptions.Left).text = "FPS limit";
            var slider = UIFactory.Slider(panel, "FpsSlider", 340, rowY + 10, 280, 34, Accent);
            slider.wholeNumbers = true;
            slider.minValue = 0;
            slider.maxValue = GameSettings.FpsOptions.Length - 1;
            slider.gameObject.AddComponent<ButtonSound>();
            var value = UIFactory.Text(panel, "FpsValue", 630, rowY, 110, 50, 30, TextAlignmentOptions.Right);
            value.text = "Screen";

            so.FindProperty("fpsSlider").objectReferenceValue = slider;
            so.FindProperty("fpsValue").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static bool PatchOptions(OptionsMenu options)
        {
            var so = new SerializedObject(options);
            if (so.FindProperty("controls").objectReferenceValue != null) return false;

            var panel = (RectTransform)so.FindProperty("panel").objectReferenceValue;
            var back = (Button)so.FindProperty("backButton").objectReferenceValue;
            if (panel == null || back == null) return false;

            // [Controls] [Back] sur la dernière ligne du panneau d'options
            float w = panel.sizeDelta.x;
            var backRt = (RectTransform)back.transform;
            backRt.anchoredPosition = new Vector2(w / 2f + 15f, backRt.anchoredPosition.y);
            backRt.sizeDelta = new Vector2(300f, backRt.sizeDelta.y);
            var controlsButton = UIFactory.Button(panel, "Controls", w / 2f - 315f, -backRt.anchoredPosition.y, 300f, backRt.sizeDelta.y, Accent);
            UIFactory.ButtonLabel(controlsButton, 34).text = "Controls";
            controlsButton.gameObject.AddComponent<ButtonSound>();

            var controls = BuildControlsPanel(options.transform);
            so.FindProperty("controlsButton").objectReferenceValue = controlsButton;
            so.FindProperty("controls").objectReferenceValue = controls;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static string ControlLine(string action, string keys) => $"{action}\n<color=#FFD966>{keys}</color>\n";

        /// <summary>Contrôles clavier (sans souris : le curseur est caché, la visée suit le déplacement).</summary>
        static string KeyboardControlsText =>
            "<size=120%>KEYBOARD</size>\n\n"
            + ControlLine("Move", "WASD / Arrow keys")
            + ControlLine("Toggle manual aim", "T")
            + ControlLine("Aim (manual mode)", "Your movement direction")
            + "Emotes  <color=#FFD966>Hold F + direction</color>\n"
            + ControlLine("Pause", "Esc")
            + ControlLine("Menus", "Arrows + Enter  •  Esc to go back");

        /// <summary>Met à jour le texte clavier d'un écran des contrôles déjà construit.</summary>
        static bool UpdateKeyboardControls()
        {
            bool changed = false;
            foreach (var panel in Object.FindObjectsByType<ControlsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.name != "Keyboard" || !text.text.Contains("MOUSE")) continue;
                    text.text = KeyboardControlsText;
                    EditorUtility.SetDirty(text);
                    changed = true;
                }
            }
            return changed;
        }

        static ControlsPanel BuildControlsPanel(Transform optionsRoot)
        {
            var root = UIFactory.Stretch(optionsRoot, "ControlsScreen");
            var view = root.gameObject.AddComponent<ControlsPanel>();

            const float w = 1240f, h = 680f;
            var panel = UIFactory.Rect(root, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            UIFactory.AddImage(panel, new Color(0.07f, 0.07f, 0.09f, 0.98f));
            UIFactory.AddImage(UIFactory.TopLeft(panel, "Bar", 0, 0, w, 8), Accent);
            UIFactory.Text(panel, "Title", 0, 26, w, 70, 60, TextAlignmentOptions.Top).text = "CONTROLS";

            var intro = UIFactory.Text(panel, "Intro", 40, 100, w - 80, 40, 24, TextAlignmentOptions.Top);
            intro.text = "<color=#AAAAAA>Your weapons fire on their own at the nearest enemy. Just move, dodge and pick up materials!</color>";

            string Line(string action, string keys) => ControlLine(action, keys);

            var keyboard = UIFactory.Text(panel, "Keyboard", 60, 160, w / 2f - 80, 420, 24, TextAlignmentOptions.TopLeft, wrap: true);
            keyboard.text = KeyboardControlsText;

            var gamepad = UIFactory.Text(panel, "Gamepad", w / 2f + 20, 160, w / 2f - 80, 420, 24, TextAlignmentOptions.TopLeft, wrap: true);
            gamepad.text = "<size=120%>GAMEPAD</size>\n\n"
                           + Line("Move", "Left stick / D-pad")
                           + Line("Toggle manual aim", "Right stick click")
                           + Line("Aim (manual mode)", "Right stick")
                           + "Emotes  <color=#FFD966>Hold Y + right stick</color>\n"
                           + Line("Pause", "Start")
                           + Line("Menus", "Stick + A  •  B to go back");

            var back = UIFactory.Button(panel, "Back", w / 2f - 150f, h - 90f, 300f, 64f, Accent);
            UIFactory.ButtonLabel(back, 30).text = "Back";
            back.gameObject.AddComponent<ButtonSound>();

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return view;
        }
    }
}
