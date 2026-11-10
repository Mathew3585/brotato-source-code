using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Coop :
    /// - bouton « Give » de la boutique (donner une arme ou un objet à un coéquipier) : la rangée [Sell] [Merge]
    ///   devient [Sell] [Merge] [Give], plus 3 boutons cachés « P2..P4 » (choix du coéquipier à 3-4 joueurs) ;
    /// - icône de caisse « x2 » à côté du panneau de chaque joueur dans le HUD (qui a ramassé les caisses).
    /// Modifie les prefabs ShopColumn et PlayerPanel (les 4 exemplaires suivent) ; n'ajoute que ce qui manque.
    /// </summary>
    static class CoopBuilder
    {
        const string ShopColumnPath = "Assets/GAME/Prefabs/UI/ShopColumn.prefab";
        const string PlayerPanelPath = "Assets/GAME/Prefabs/UI/PlayerPanel.prefab";
        const float CrateSize = 64f;
        const string ButtonPath = "Assets/GAME/Prefabs/UI/UIButton.prefab";
        const string GameScenePath = "Assets/GAME/Scenes/Game.unity";
        const float Gap = 8f;
        const int TargetCount = 3;

        [MenuItem("BrotatoLike/Coop : bouton Give et caisses du HUD", priority = 12)]
        static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Run();
            EditorSceneManager.OpenScene(GameScenePath);
        }

        /// <summary>Appelé à la fin de PrototypeBuilder.Build (après la conversion en prefabs).</summary>
        public static void Run()
        {
            // La scène de jeu d'abord : on y lit le sprite de la caisse (GameManager)
            var scene = EditorSceneManager.OpenScene(GameScenePath);
            var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            var crateSprite = gm != null ? gm.crateSprite : null;

            EditPrefab(ShopColumnPath, Patch);
            EditPrefab(PlayerPanelPath, root => PatchPanel(root, crateSprite));

            // Colonnes de la scène : la rangée retaillée dans le prefab ne doit pas être masquée par d'anciens overrides ;
            // un objet qui n'est pas (ou plus) une instance du prefab est complété directement.
            bool changed = false;
            foreach (var v in Object.FindObjectsByType<ShopColumnView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                changed |= RevertLayout(v.sellButton) | RevertLayout(v.mergeButton);
                changed |= Patch(v.gameObject);
            }
            foreach (var p in Object.FindObjectsByType<PlayerPanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= PatchPanel(p.gameObject, crateSprite);
            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void EditPrefab(string path, System.Func<GameObject, bool> patch)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (patch(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Case « caisses » collée au panneau du joueur (à droite ; le HUD la passe à gauche pour les panneaux de droite) :
        /// fond sombre comme le panneau, icône de caisse, « x2 » en bas à droite. Cachée tant qu'il n'y a pas de caisse.
        /// </summary>
        static bool PatchPanel(GameObject root, Sprite crateSprite)
        {
            var v = root.GetComponent<PlayerPanelView>();
            if (v == null || v.crateRoot != null) return false;

            var box = UIFactory.Rect(root.transform, "Crates", new Vector2(1f, 1f), Vector2.zero, new Vector2(CrateSize, CrateSize));
            box.pivot = new Vector2(0f, 1f);
            box.anchoredPosition = new Vector2(8f, 0f);
            UIFactory.AddImage(box, new Color(0f, 0f, 0f, 0.55f));

            v.crateIcon = UIFactory.Icon(box, "Icon", 6f, 6f, CrateSize - 12f);
            v.crateIcon.sprite = crateSprite;
            v.crateIcon.preserveAspect = true;

            var countRt = UIFactory.Rect(box, "Count", new Vector2(1f, 0f), new Vector2(-3f, 1f), new Vector2(CrateSize, 24f));
            countRt.pivot = new Vector2(1f, 0f);
            v.crateCount = UIFactory.Text(countRt, 22, TextAlignmentOptions.BottomRight);
            v.crateCount.color = new Color(1f, 0.85f, 0.35f);
            v.crateCount.text = "x2";
            if (v.materials != null)
            {
                v.crateCount.font = v.materials.font;
                v.crateCount.fontSharedMaterial = v.materials.fontSharedMaterial;
            }

            v.crateRoot = box;
            box.gameObject.SetActive(false);
            EditorUtility.SetDirty(v);
            return true;
        }

        static bool Patch(GameObject root)
        {
            var v = root.GetComponent<ShopColumnView>();
            if (v == null || v.giveButton != null || v.sellButton == null || v.mergeButton == null) return false;

            var sell = (RectTransform)v.sellButton.transform;
            var merge = (RectTransform)v.mergeButton.transform;
            float x0 = sell.anchoredPosition.x;
            float y = -sell.anchoredPosition.y;
            float h = sell.sizeDelta.y;
            float w = (merge.anchoredPosition.x + merge.sizeDelta.x - x0 - 2f * Gap) / 3f;
            float X(int i) => x0 + i * (w + Gap);

            Place(sell, X(0), y, w, h);
            Place(merge, X(1), y, w, h);

            v.giveButton = NewButton(v, sell.parent, "Give", X(2), y, w, h, out v.giveLabel);
            v.giveLabel.text = "Give";

            v.giveTargets = new Button[TargetCount];
            v.giveTargetLabels = new TextMeshProUGUI[TargetCount];
            for (int i = 0; i < TargetCount; i++)
            {
                v.giveTargets[i] = NewButton(v, sell.parent, "GiveTo " + i, X(i), y, w, h, out v.giveTargetLabels[i]);
                v.giveTargetLabels[i].text = "P" + (i + 2);
                v.giveTargets[i].gameObject.SetActive(false);
            }
            v.giveButton.gameObject.SetActive(false);

            EditorUtility.SetDirty(v);
            return true;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Bouton texte : instance du prefab UIButton (repli : bouton UIFactory), même police que « Sell ».</summary>
        static Button NewButton(ShopColumnView v, Transform parent, string name, float x, float y, float w, float h, out TextMeshProUGUI label)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            Button button;
            if (asset != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                go.name = name;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                Place(rt, x, y, w, h);
                button = go.GetComponent<Button>();
                label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            else
            {
                button = UIFactory.Button(parent, name, x, y, w, h, UIFactory.Accent);
                button.gameObject.AddComponent<ButtonSound>();
                label = UIFactory.ButtonLabel(button, 20);
            }

            var reference = v.sellLabel;
            if (reference != null)
            {
                label.font = reference.font;
                label.fontSharedMaterial = reference.fontSharedMaterial;
                label.fontSize = reference.fontSize;
                label.fontStyle = reference.fontStyle;
                label.color = reference.color;
            }
            button.transform.SetSiblingIndex(v.mergeButton.transform.GetSiblingIndex() + 1);
            return button;
        }

        /// <summary>Retire les overrides de position / taille d'un bouton d'une instance de prefab (pour suivre le prefab).</summary>
        static bool RevertLayout(Selectable button)
        {
            if (button == null || !PrefabUtility.IsPartOfPrefabInstance(button.transform)) return false;
            var so = new SerializedObject(button.transform);
            bool changed = false;
            foreach (var name in new[] { "m_AnchoredPosition", "m_SizeDelta" })
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
