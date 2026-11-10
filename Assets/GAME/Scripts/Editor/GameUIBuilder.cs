using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Crée dans la scène de jeu toute l'UI (HUD, boutique, pause) et branche les références.
    /// Une fois créée, l'UI se modifie librement dans l'éditeur : ce code n'est plus relancé
    /// (sauf via "BrotatoLike > Reconstruire l'UI des scènes").
    /// </summary>
    static class GameUIBuilder
    {
        const float ColumnWidth = 460f;
        const float ColumnHeight = 1040f;
        const float Inner = ColumnWidth - 32f;
        static readonly Color Placeholder = new(0.6f, 0.6f, 0.6f);

        public static void Build(HUD hud)
        {
            Clear(hud.transform);
            var shop = hud.GetComponent<ShopUI>();
            if (shop == null) shop = hud.gameObject.AddComponent<ShopUI>();

            BuildHud(hud);
            BuildShop(shop);
            BuildPause(hud);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(shop);
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        // ================================================================ HUD

        static void BuildHud(HUD hud)
        {
            var root = UIFactory.CreateCanvas(hud.transform, "HUD Canvas", 10, interactive: false);

            var wave = UIFactory.Text(UIFactory.Rect(root, "Wave", new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(600, 60)), 44, TextAlignmentOptions.Top);
            wave.text = "WAVE 1 / 20";
            var timer = UIFactory.Text(UIFactory.Rect(root, "Timer", new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(300, 90)), 72, TextAlignmentOptions.Top);
            timer.text = "20";
            var center = UIFactory.Text(UIFactory.Rect(root, "Center", new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1400, 400)), 64, TextAlignmentOptions.Center);
            var hint = UIFactory.Text(UIFactory.Rect(root, "Hint", new Vector2(0.5f, 0f), new Vector2(0, 35), new Vector2(1800, 40)), 24, TextAlignmentOptions.Bottom);
            hint.color = new Color(1f, 1f, 1f, 0.6f);
            hint.text = "Move: WASD / arrows / left stick   •   Manual aim: T / right stick click   •   Pause: Esc / Start";

            // Barre du boss
            var bossBar = UIFactory.Rect(root, "BossBar", new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(820f, 30f));
            UIFactory.AddImage(bossBar, new Color(0f, 0f, 0f, 0.7f));
            var bossFill = UIFactory.Stretch(bossBar, "Fill");
            UIFactory.AddImage(bossFill, new Color(0.75f, 0.15f, 0.2f));
            var bossName = UIFactory.Text(UIFactory.Stretch(bossBar, "Name"), 22, TextAlignmentOptions.Center);
            bossName.text = "DRAGON   3000 / 3000";

            var panels = new PlayerPanelView[4];
            for (int i = 0; i < 4; i++) panels[i] = BuildPlayerPanel(root, i);

            var so = new SerializedObject(hud);
            so.FindProperty("hudRoot").objectReferenceValue = root.gameObject;
            so.FindProperty("waveText").objectReferenceValue = wave;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("centerText").objectReferenceValue = center;
            so.FindProperty("bossBar").objectReferenceValue = bossBar;
            so.FindProperty("bossFill").objectReferenceValue = bossFill;
            so.FindProperty("bossName").objectReferenceValue = bossName;
            SetArray(so, "playerPanels", panels);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static PlayerPanelView BuildPlayerPanel(RectTransform root, int i)
        {
            bool right = i % 2 == 1;
            bool bottom = i >= 2;
            var anchor = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
            var pos = new Vector2(right ? -20f : 20f, bottom ? 60f : -20f);

            var panel = UIFactory.Rect(root, "Player " + (i + 1), anchor, pos, new Vector2(380, 130));
            panel.pivot = anchor;
            panel.anchoredPosition = pos;
            UIFactory.AddImage(panel, new Color(0f, 0f, 0f, 0.55f));
            var v = panel.gameObject.AddComponent<PlayerPanelView>();

            var stripe = UIFactory.Rect(panel, "Stripe", new Vector2(right ? 1f : 0f, 0.5f), Vector2.zero, new Vector2(8, 130));
            stripe.pivot = new Vector2(right ? 1f : 0f, 0.5f);
            v.stripe = UIFactory.AddImage(stripe, Session.ColorOf(i));

            v.title = UIFactory.Text(panel, "Name", 22, 8, 240, 34, 28);
            v.title.text = $"P{i + 1} - Character";
            v.title.color = Session.ColorOf(i);
            v.level = UIFactory.Text(panel, "Level", 246, 8, 120, 34, 26, TextAlignmentOptions.TopRight);
            v.level.text = "Lv. 1";
            v.hpFill = Bar(panel, 22, 46, 340, 28, new Color(0.85f, 0.2f, 0.2f), out v.hpText);
            v.hpText.text = "10 / 10";
            v.xpFill = Bar(panel, 22, 80, 340, 12, new Color(0.35f, 0.85f, 0.35f), out _);
            v.materials = UIFactory.Text(panel, "Materials", 22, 96, 200, 30, 24);
            v.materials.text = "Materials: 0";
            v.materials.color = new Color(1f, 0.85f, 0.35f);
            v.aim = UIFactory.Text(panel, "Aim", 166, 100, 200, 30, 20, TextAlignmentOptions.TopRight);
            v.aim.text = "Aim: auto";
            return v;
        }

        static RectTransform Bar(Transform parent, float x, float y, float w, float h, Color fillColor, out TextMeshProUGUI label)
        {
            var back = UIFactory.TopLeft(parent, "Bar", x, y, w, h);
            UIFactory.AddImage(back, new Color(0.15f, 0.15f, 0.15f));
            var fill = UIFactory.Stretch(back, "Fill");
            UIFactory.AddImage(fill, fillColor);
            label = UIFactory.Text(UIFactory.Stretch(back, "Label"), h * 0.75f, TextAlignmentOptions.Center);
            return fill;
        }

        // ================================================================ Boutique

        static void BuildShop(ShopUI shop)
        {
            var root = UIFactory.CreateCanvas(shop.transform, "Shop Canvas", 20, interactive: true);
            UIFactory.AddImage(UIFactory.Stretch(root, "Dim"), new Color(0f, 0f, 0f, 0.7f));

            const float gap = 20f;
            float total = 4 * ColumnWidth + 3 * gap;
            var columns = new ShopColumnView[4];
            for (int i = 0; i < 4; i++)
            {
                columns[i] = BuildColumn(root, i);
                columns[i].Rect.anchoredPosition = new Vector2(-total / 2f + i * (ColumnWidth + gap), -20f);
            }

            var so = new SerializedObject(shop);
            so.FindProperty("shopRoot").objectReferenceValue = root.gameObject;
            SetArray(so, "columnViews", columns);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Masqué dans l'éditeur pour ne pas cacher le jeu : active "Shop Canvas" pour le modifier
            root.gameObject.SetActive(false);
        }

        static ShopColumnView BuildColumn(RectTransform parent, int index)
        {
            var root = UIFactory.Rect(parent, "Column J" + (index + 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(ColumnWidth, ColumnHeight));
            root.pivot = new Vector2(0f, 1f);
            UIFactory.AddImage(root, new Color(0.07f, 0.07f, 0.09f, 0.94f));
            var v = root.gameObject.AddComponent<ShopColumnView>();
            var color = Session.ColorOf(index);

            v.colorBar = UIFactory.AddImage(UIFactory.TopLeft(root, "Bar", 0, 0, ColumnWidth, 6), color);
            v.title = UIFactory.Text(root, "Title", 16, 16, Inner, 40, 30);
            v.title.text = $"P{index + 1} - Character";
            v.title.color = color;
            v.materials = UIFactory.Text(root, "Materials", 16, 16, Inner, 40, 28, TextAlignmentOptions.TopRight);
            v.materials.text = "Materials: 0";
            v.materials.color = new Color(1f, 0.85f, 0.35f);
            v.message = UIFactory.Text(root, "Message", 16, 600, Inner, 24, 20, TextAlignmentOptions.Center);
            v.message.color = new Color(1f, 0.45f, 0.45f);

            BuildCrate(root, v);
            BuildLevelUp(root, v);
            BuildOffers(root, v);
            BuildInventory(root, v);
            BuildReadyOverlay(root, v);
            return v;
        }

        static void BuildCrate(RectTransform root, ShopColumnView v)
        {
            var group = UIFactory.TopLeft(root, "Crate", 0, 64, ColumnWidth, 540);
            v.crateGroup = group.gameObject;
            v.crateTitle = UIFactory.Text(group, "Title", 16, 0, Inner, 44, 34, TextAlignmentOptions.Top);
            v.crateTitle.text = "CRATE!";
            v.crateTitle.color = new Color(0.55f, 0.8f, 1f);

            var card = UIFactory.TopLeft(group, "Card", 16, 60, Inner, 220);
            UIFactory.AddImage(card, UIFactory.ButtonNormal);
            v.crateStripe = UIFactory.AddImage(UIFactory.TopLeft(card, "Tier", 0, 0, 8, 220), Color.white);
            v.crateIcon = UIFactory.Icon(card, "Icon", 24, 24, 110);
            v.crateName = UIFactory.Text(card, "Name", 150, 24, Inner - 170, 40, 30);
            v.crateName.text = "Item name";
            v.crateDesc = UIFactory.Text(card, "Desc", 150, 70, Inner - 170, 140, 20, TextAlignmentOptions.TopLeft, wrap: true);
            v.crateDesc.fontStyle = FontStyles.Normal;

            float half = Inner / 2f - 6f;
            v.takeButton = UIFactory.Button(group, "Take", 16, 300, half, 70, Placeholder);
            UIFactory.ButtonLabel(v.takeButton, 28).text = "Take";
            v.recycleButton = UIFactory.Button(group, "Recycle", 16 + half + 12, 300, half, 70, Placeholder);
            v.recycleLabel = UIFactory.ButtonLabel(v.recycleButton, 24);
            v.recycleLabel.text = "Recycle (+5)";
            group.gameObject.SetActive(false);
        }

        static void BuildLevelUp(RectTransform root, ShopColumnView v)
        {
            var group = UIFactory.TopLeft(root, "LevelUp", 0, 64, ColumnWidth, 540);
            v.levelUpGroup = group.gameObject;
            v.levelUpTitle = UIFactory.Text(group, "Title", 16, 0, Inner, 44, 34, TextAlignmentOptions.Top);
            v.levelUpTitle.text = "LEVEL UP!";
            v.levelUpTitle.color = new Color(0.5f, 1f, 0.5f);

            v.upgrades = new ShopColumnView.UpgradeView[4];
            for (int i = 0; i < 4; i++)
            {
                var u = new ShopColumnView.UpgradeView();
                u.button = UIFactory.Button(group, "Upgrade " + i, 16, 54 + i * 120, Inner, 110, Placeholder);
                u.stripe = UIFactory.AddImage(UIFactory.TopLeft(u.button.transform, "Tier", 0, 0, 8, 110), Color.white);
                u.tier = UIFactory.Text(u.button.transform, "TierText", 24, 12, Inner - 40, 30, 20);
                u.tier.text = "Tier I";
                u.text = UIFactory.Text(u.button.transform, "Text", 24, 44, Inner - 40, 50, 32);
                u.text.text = "+5% Damage";
                v.upgrades[i] = u;
            }
            group.gameObject.SetActive(false);
        }

        static void BuildOffers(RectTransform root, ShopColumnView v)
        {
            var group = UIFactory.TopLeft(root, "Shop", 0, 64, ColumnWidth, 540);
            v.shopGroup = group.gameObject;
            float cardW = Inner - 74f;

            v.offers = new ShopColumnView.OfferView[4];
            for (int i = 0; i < 4; i++)
            {
                float y = i * 116f;
                var o = new ShopColumnView.OfferView();
                o.card = UIFactory.Button(group, "Offer " + i, 16, y, cardW, 108, Placeholder);
                o.stripe = UIFactory.AddImage(UIFactory.TopLeft(o.card.transform, "Tier", 0, 0, 8, 108), Color.white);
                o.icon = UIFactory.Icon(o.card.transform, "Icon", 18, 18, 72);
                o.name = UIFactory.Text(o.card.transform, "Name", 100, 8, cardW - 190, 30, 22);
                o.name.text = "Offer name";
                o.price = UIFactory.Text(o.card.transform, "Price", cardW - 96, 8, 86, 30, 24, TextAlignmentOptions.TopRight);
                o.price.text = "20";
                o.desc = UIFactory.Text(o.card.transform, "Desc", 100, 40, cardW - 110, 64, 16, TextAlignmentOptions.TopLeft, wrap: true);
                o.desc.fontStyle = FontStyles.Normal;
                o.desc.text = "Description";
                o.sold = UIFactory.Text(UIFactory.Stretch(o.card.transform, "Sold"), 40, TextAlignmentOptions.Center);
                o.sold.text = "SOLD";
                o.sold.color = new Color(1f, 1f, 1f, 0.35f);
                o.sold.enabled = false;

                o.lockButton = UIFactory.Button(group, "Lock " + i, 16 + Inner - 64, y + 22, 64, 64, Placeholder);
                o.lockIcon = UIFactory.Icon(o.lockButton.transform, "Icon", 8, 8, 48);
                v.offers[i] = o;
            }

            float half = Inner / 2f - 6f;
            v.rerollButton = UIFactory.Button(group, "Reroll", 16, 470, half, 60, Placeholder);
            v.rerollLabel = UIFactory.ButtonLabel(v.rerollButton, 24);
            v.rerollLabel.text = "Reroll (1)";
            v.readyButton = UIFactory.Button(group, "Ready", 16 + half + 12, 470, half, 60, Placeholder);
            UIFactory.ButtonLabel(v.readyButton, 28).text = "READY";
        }

        static void BuildInventory(RectTransform root, ShopColumnView v)
        {
            v.weaponsLabel = UIFactory.Text(root, "WeaponsLabel", 16, 630, Inner, 28, 20);
            v.weaponsLabel.text = "Weapons (1/6)";

            int n = WeaponHolder.MaxWeapons;
            float slot = Mathf.Min(64f, (Inner - (n - 1) * 8f) / n);
            v.weaponSlots = new Button[n];
            v.weaponIcons = new Image[n];
            v.weaponTiers = new TextMeshProUGUI[n];
            for (int i = 0; i < n; i++)
            {
                var b = UIFactory.Button(root, "Slot " + i, 16 + i * (slot + 8), 660, slot, slot, Placeholder);
                v.weaponIcons[i] = UIFactory.Icon(b.transform, "Icon", 4, 4, slot - 8);
                v.weaponTiers[i] = UIFactory.Text(b.transform, "Tier", 0, slot - 20, slot - 4, 20, 15, TextAlignmentOptions.BottomRight);
                v.weaponSlots[i] = b;
            }

            float half = Inner / 2f - 6f;
            float y = 660 + slot + 8;
            v.sellButton = UIFactory.Button(root, "Sell", 16, y, half, 42, Placeholder);
            v.sellLabel = UIFactory.ButtonLabel(v.sellButton, 20);
            v.sellLabel.text = "Sell (+5)";
            v.mergeButton = UIFactory.Button(root, "Merge", 16 + half + 12, y, half, 42, Placeholder);
            UIFactory.ButtonLabel(v.mergeButton, 20).text = "Merge";

            v.itemsLabel = UIFactory.Text(root, "ItemsLabel", 16, 790, Inner, 28, 20);
            v.itemsLabel.text = "Items (0)";
            var grid = UIFactory.TopLeft(root, "Items", 16, 818, Inner, 84);
            const float size = 36f, step = 40f;
            int perRow = Mathf.FloorToInt(Inner / step);
            var icons = new List<Image>();
            for (int k = 0; k < perRow * 2; k++)
                icons.Add(UIFactory.Icon(grid, "Item " + k, (k % perRow) * step, (k / perRow) * step, size));
            v.itemIcons = icons.ToArray();

            v.statsLeft = UIFactory.Text(root, "StatsL", 16, 908, Inner / 2f, 130, 17, TextAlignmentOptions.TopLeft, wrap: true);
            v.statsRight = UIFactory.Text(root, "StatsR", 16 + Inner / 2f, 908, Inner / 2f, 130, 17, TextAlignmentOptions.TopLeft, wrap: true);
            v.statsLeft.fontStyle = v.statsRight.fontStyle = FontStyles.Normal;
            v.statsLeft.text = "Max HP 10\nHP Regen 0\nArmor 0";
            v.statsRight.text = "Damage 0%\nAttack Speed 0%\nCrit Chance 0%";
        }

        static void BuildReadyOverlay(RectTransform root, ShopColumnView v)
        {
            var overlay = UIFactory.Stretch(root, "ReadyOverlay");
            v.readyOverlay = overlay.gameObject;
            v.readyOverlayImage = UIFactory.AddImage(overlay, new Color(0f, 0f, 0f, 0.65f), raycast: true);
            v.readyText = UIFactory.Text(UIFactory.TopLeft(overlay, "Text", 0, 380, ColumnWidth, 160), 54, TextAlignmentOptions.Center);
            v.readyText.text = "READY!\n<size=45%>Waiting for the other players...</size>";
            v.unreadyButton = UIFactory.Button(overlay, "Cancel", ColumnWidth / 2f - 120f, 560, 240, 56, Placeholder);
            UIFactory.ButtonLabel(v.unreadyButton, 24).text = "Cancel";
            overlay.gameObject.SetActive(false);
        }

        // ================================================================ Pause

        static void BuildPause(HUD hud)
        {
            var root = UIFactory.CreateCanvas(hud.transform, "Pause Canvas", 30, interactive: true);
            var panel = UIFactory.Stretch(root, "Pause");
            UIFactory.AddImage(panel, new Color(0f, 0f, 0f, 0.6f), raycast: true);

            var title = UIFactory.Text(UIFactory.Rect(panel, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(800f, 120f)),
                96, TextAlignmentOptions.Center);
            title.text = "PAUSE";

            var column = UIFactory.Rect(panel, "Buttons", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(440f, 190f));
            var accent = UIFactory.Accent;
            var resume = UIFactory.Button(column, "Resume", 0, 0, 440, 80, accent);
            UIFactory.ButtonLabel(resume, 34).text = "Resume";
            var menu = UIFactory.Button(column, "Menu", 0, 100, 440, 80, accent);
            UIFactory.ButtonLabel(menu, 34).text = "Main menu";

            var so = new SerializedObject(hud);
            so.FindProperty("pauseRoot").objectReferenceValue = root.gameObject;
            so.FindProperty("pausePanel").objectReferenceValue = panel;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("menuButton").objectReferenceValue = menu;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
        }

        // ================================================================ Utilitaires

        public static void SetArray<T>(SerializedObject so, string field, IList<T> values) where T : Object
        {
            var prop = so.FindProperty(field);
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
