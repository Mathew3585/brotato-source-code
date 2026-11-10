using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Écran de fin : carte en haut de chaque colonne de joueur (personnage, nom, meilleure arme)
    /// et cadre doré (+ bandeau « TOP DAMAGE ») pour le joueur qui a fait le plus de dégâts.
    /// Modifie le prefab EndPlayerColumn (les 4 colonnes suivent), puis branche les références dans la scène.
    /// N'ajoute que ce qui manque.
    /// </summary>
    static class EndScreenBuilder
    {
        const string ColumnPath = "Assets/GAME/Prefabs/UI/EndPlayerColumn.prefab";
        const string GameScenePath = "Assets/GAME/Scenes/Game.unity";
        const float Height = 680f;
        const float PortraitSize = 120f;
        const float TitleY = 150f;
        const float WeaponY = 196f;
        const float StatsY = 268f;
        static readonly Color Gold = new(1f, 0.8f, 0.25f);

        [MenuItem("BrotatoLike/Écran de fin : cartes des joueurs", priority = 13)]
        static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Run();
        }

        /// <summary>Appelé à la fin de PrototypeBuilder.Build (après la conversion en prefabs).</summary>
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ColumnPath) != null)
            {
                var root = PrefabUtility.LoadPrefabContents(ColumnPath);
                try
                {
                    if (BuildCard(root.transform)) PrefabUtility.SaveAsPrefabAsset(root, ColumnPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var scene = EditorSceneManager.OpenScene(GameScenePath);
            var hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
            var view = hud != null ? new SerializedObject(hud).FindProperty("endScreen").objectReferenceValue as EndScreenView : null;
            if (view == null || view.players == null) return;

            bool changed = false;
            foreach (var col in view.players)
            {
                if (col == null || col.root == null) continue;
                var rt = (RectTransform)col.root.transform;
                // Colonne qui n'est pas une instance du prefab : on la complète directement
                changed |= BuildCard(rt);
                changed |= RevertOverrides(rt, "m_SizeDelta");
                foreach (var name in new[] { "Title", "Stats" })
                    if (rt.Find(name) is RectTransform child) changed |= RevertOverrides(child, "m_AnchoredPosition", "m_SizeDelta");
                changed |= Assign(col, rt);
            }
            if (!changed) return;
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Agrandit la colonne, descend le titre et les stats, ajoute portrait, arme, séparateur et cadre doré.</summary>
        static bool BuildCard(Transform column)
        {
            var rt = (RectTransform)column;
            if (rt.Find("Portrait") != null) return false;
            float width = rt.sizeDelta.x;
            rt.sizeDelta = new Vector2(width, Height);

            var title = rt.Find("Title") as RectTransform;
            var stats = rt.Find("Stats") as RectTransform;
            var reference = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
            if (title != null) title.anchoredPosition = new Vector2(title.anchoredPosition.x, -TitleY);
            if (stats != null)
            {
                stats.anchoredPosition = new Vector2(stats.anchoredPosition.x, -StatsY);
                stats.sizeDelta = new Vector2(stats.sizeDelta.x, Height - StatsY - 20f);
            }

            // Cadre doré : 4 bandes de 6 px juste autour de la colonne (le fond de la colonne reste visible)
            const float border = 6f;
            var frame = UIFactory.Stretch(rt, "GoldFrame");
            frame.offsetMin = new Vector2(-border, -border);
            frame.offsetMax = new Vector2(border, border);
            Strip(frame, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, border));
            Strip(frame, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, border));
            Strip(frame, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(border, 0f));
            Strip(frame, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(border, 0f));
            // Bandeau sur le bord bas du cadre (en haut, il toucherait le sous-titre)
            var badge = UIFactory.Rect(frame, "Badge", new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(230f, 36f));
            UIFactory.AddImage(badge, Gold);
            var badgeText = UIFactory.Text(UIFactory.Stretch(badge, "Label"), 24, TextAlignmentOptions.Center);
            badgeText.text = "TOP DAMAGE";
            badgeText.color = new Color(0.1f, 0.08f, 0.05f);
            CopyFont(reference, badgeText);
            badge.gameObject.SetActive(false);
            frame.gameObject.SetActive(false);

            var portrait = UIFactory.Icon(rt, "Portrait", (width - PortraitSize) / 2f, 22f, PortraitSize);
            portrait.preserveAspect = true;

            const float iconSize = 44f;
            var weaponIcon = UIFactory.Icon(rt, "WeaponIcon", 70f, WeaponY, iconSize);
            var weaponName = UIFactory.Text(rt, "WeaponName", 70f + iconSize + 12f, WeaponY - 4f, width - 160f - iconSize, 56f, 24, TextAlignmentOptions.Left);
            weaponName.text = "<color=#AAAAAA>Best weapon</color>\nSMG";
            weaponName.color = new Color(1f, 0.85f, 0.35f);
            CopyFont(reference, weaponName);

            UIFactory.AddImage(UIFactory.TopLeft(rt, "Separator", 30f, StatsY - 14f, width - 60f, 2f), new Color(1f, 1f, 1f, 0.15f));
            return true;
        }

        static void Strip(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var rt = UIFactory.Rect(parent, name, anchorMin, Vector2.zero, size);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            UIFactory.AddImage(rt, Gold);
        }

        static void CopyFont(TextMeshProUGUI from, TextMeshProUGUI to)
        {
            if (from == null) return;
            to.font = from.font;
            to.fontSharedMaterial = from.fontSharedMaterial;
        }

        static bool Assign(EndScreenView.PlayerColumn col, RectTransform rt)
        {
            if (col.portrait != null) return false;
            col.portrait = rt.Find("Portrait")?.GetComponent<Image>();
            col.weaponIcon = rt.Find("WeaponIcon")?.GetComponent<Image>();
            col.weaponName = rt.Find("WeaponName")?.GetComponent<TextMeshProUGUI>();
            col.goldFrame = rt.Find("GoldFrame")?.gameObject;
            col.badge = rt.Find("GoldFrame/Badge")?.gameObject;
            return col.portrait != null;
        }

        /// <summary>Retire des overrides d'une instance de prefab (pour suivre la nouvelle mise en page du prefab).</summary>
        static bool RevertOverrides(Object target, params string[] properties)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(target)) return false;
            var so = new SerializedObject(target);
            bool changed = false;
            foreach (var name in properties)
            {
                var p = so.FindProperty(name);
                if (p == null || !p.prefabOverride) continue;
                PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                changed = true;
            }
            return changed;
        }
    }
}
