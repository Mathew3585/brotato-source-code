using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>Petits utilitaires pour construire l'UI par code (uGUI + TextMeshPro).</summary>
    public static class UIFactory
    {
        // Couleurs du thème, partagées par tous les outils de construction de l'UI
        public static readonly Color ButtonNormal = new(0.2f, 0.2f, 0.25f, 0.95f);
        /// <summary>Orange des boutons sélectionnés et des barres de titre.</summary>
        public static readonly Color Accent = new(1f, 0.75f, 0.25f);
        /// <summary>Fond des fenêtres (options, danger, défis, en ligne...).</summary>
        public static readonly Color PanelColor = new(0.07f, 0.07f, 0.09f, 0.97f);

        public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder, bool interactive)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (interactive) go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        /// <summary>Rect ancré en haut à gauche de son parent ; (x, y) = décalage vers la droite / vers le bas.</summary>
        public static RectTransform TopLeft(Transform parent, string name, float x, float y, float width, float height)
        {
            var rt = Rect(parent, name, new Vector2(0f, 1f), Vector2.zero, new Vector2(width, height));
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image AddImage(RectTransform rt, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Icon(Transform parent, string name, float x, float y, float size)
        {
            var img = AddImage(TopLeft(parent, name, x, y, size, size), Color.white);
            img.preserveAspect = true;
            return img;
        }

        public static TextMeshProUGUI Text(RectTransform rt, float fontSize, TextAlignmentOptions alignment, bool wrap = false)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.fontStyle = FontStyles.Normal; // police pixel : pas de faux gras
            return t;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float x, float y, float width, float height,
            float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool wrap = false) =>
            Text(TopLeft(parent, name, x, y, width, height), fontSize, alignment, wrap);

        /// <summary>Bouton dont la couleur de fond indique l'état (sélectionné = couleur du joueur).</summary>
        public static Button Button(Transform parent, string name, float x, float y, float width, float height, Color selectedColor)
        {
            var rt = TopLeft(parent, name, x, y, width, height);
            var img = AddImage(rt, Color.white, raycast: true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            SetAccent(button, selectedColor);
            return button;
        }

        /// <summary>Couleurs du bouton : la couleur d'accent sert pour le survol / la sélection.</summary>
        public static void SetAccent(Selectable button, Color selectedColor)
        {
            button.colors = new ColorBlock
            {
                normalColor = ButtonNormal,
                highlightedColor = Color.Lerp(ButtonNormal, selectedColor, 0.45f),
                selectedColor = Color.Lerp(ButtonNormal, selectedColor, 0.75f),
                pressedColor = selectedColor,
                disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.8f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        public static TextMeshProUGUI ButtonLabel(Button button, float fontSize)
        {
            var rt = Stretch(button.transform, "Label");
            return Text(rt, fontSize, TextAlignmentOptions.Center);
        }

        /// <summary>Curseur 0..1 (volumes...) navigable à la manette : gauche / droite changent la valeur.</summary>
        public static Slider Slider(Transform parent, string name, float x, float y, float width, float height, Color accent)
        {
            var rt = TopLeft(parent, name, x, y, width, height);
            AddImage(Stretch(rt, "Background"), new Color(0.12f, 0.12f, 0.15f), raycast: true);

            var fillArea = Stretch(rt, "Fill Area");
            var fill = Stretch(fillArea, "Fill");
            AddImage(fill, accent * new Color(0.8f, 0.8f, 0.8f, 1f));

            var handleArea = Stretch(rt, "Handle Slide Area");
            var handle = Rect(handleArea, "Handle", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(height * 0.7f, 0f));
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(height * 0.7f, 10f);
            var handleImage = AddImage(handle, Color.white, raycast: true);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            SetAccent(slider, accent);
            return slider;
        }

        /// <summary>Ligne cliquable avec un libellé et une case à cocher à droite.</summary>
        public static Toggle Toggle(Transform parent, string name, float x, float y, float width, float height, string label, Color accent)
        {
            var rt = TopLeft(parent, name, x, y, width, height);
            var background = AddImage(rt, Color.white, raycast: true);
            Text(rt, "Label", 20, 0, width - 100, height, height * 0.5f, TextAlignmentOptions.Left).text = label;

            float box = height * 0.6f;
            var boxRt = TopLeft(rt, "Box", width - box - 20, (height - box) / 2f, box, box);
            AddImage(boxRt, new Color(0.08f, 0.08f, 0.1f));
            var check = TopLeft(boxRt, "Check", box * 0.2f, box * 0.2f, box * 0.6f, box * 0.6f);
            var checkImage = AddImage(check, accent);

            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkImage;
            SetAccent(toggle, accent);
            return toggle;
        }

        /// <summary>
        /// Navigation manette/clavier explicite : gauche/droite dans une ligne, haut/bas vers l'élément
        /// le plus proche horizontalement dans la ligne voisine. Indispensable en coop :
        /// la navigation automatique d'Unity pourrait sauter dans le panneau d'un autre joueur.
        /// </summary>
        public static void LinkNavigation(List<List<Selectable>> rows)
        {
            rows.RemoveAll(r => r.Count == 0);
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int i = 0; i < row.Count; i++)
                {
                    var nav = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = i > 0 ? row[i - 1] : null,
                        selectOnRight = i < row.Count - 1 ? row[i + 1] : null,
                        selectOnUp = r > 0 ? Closest(rows[r - 1], row[i]) : null,
                        selectOnDown = r < rows.Count - 1 ? Closest(rows[r + 1], row[i]) : null,
                    };
                    row[i].navigation = nav;
                }
            }
        }

        static Selectable Closest(List<Selectable> row, Selectable from)
        {
            Selectable best = null;
            float bestDist = float.MaxValue;
            float x = from.transform.position.x;
            foreach (var s in row)
            {
                float d = Mathf.Abs(s.transform.position.x - x);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = s;
                }
            }
            return best;
        }
    }
}
