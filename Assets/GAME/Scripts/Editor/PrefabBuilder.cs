using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Transforme l'UI répétée des scènes en prefabs (Assets/GAME/Prefabs/UI) : colonnes de boutique, panneaux du HUD,
    /// cases du lobby, colonnes de l'écran de fin, options, boutons texte, objet Audio...
    ///
    /// Les objets existants sont CONVERTIS sur place en instances (pas recréés) : les références de la scène
    /// et les retouches faites à la main sont gardées (elles deviennent des overrides).
    /// Le prefab est créé depuis le premier exemplaire s'il n'existe pas encore ; un prefab existant n'est jamais écrasé.
    /// Après « Reconstruire l'UI des scènes », l'UI recréée est reconvertie en instances des prefabs existants.
    /// </summary>
    static class PrefabBuilder
    {
        const string PrefabDir = "Assets/GAME/Prefabs";
        const string UiDir = PrefabDir + "/UI";
        const string ResourcesDir = PrefabDir + "/Resources";
        const string GameScenePath = "Assets/GAME/Scenes/Game.unity";
        const string MenuScenePath = "Assets/GAME/Scenes/MainMenu.unity";

        static string Ui(string name) => $"{UiDir}/{name}.prefab";

        static readonly string AudioPath = PrefabDir + "/Audio.prefab";
        static readonly string ButtonPath = Ui("UIButton");
        static readonly string OptionsPath = Ui("OptionsMenu");
        static readonly string ShopColumnPath = Ui("ShopColumn");
        static readonly string LobbySlotPath = Ui("LobbySlot");

        static readonly ConvertToPrefabInstanceSettings ConvertSettings = new()
        {
            objectMatchMode = ObjectMatchMode.ByHierarchy,
            componentsNotMatchedBecomesOverride = true,
            gameObjectsNotMatchedBecomesOverride = true,
            // Indispensable : la position, la taille, la couleur du joueur... de chaque exemplaire restent les siennes
            recordPropertyOverridesOfMatches = true,
            changeRootNameToAssetName = false,
        };

        static int converted;

        /// <summary>Convertit l'UI des deux scènes, puis imbrique les petits prefabs dans les gros. Ouvre et enregistre les scènes.</summary>
        public static void Run()
        {
            PrototypeBuilder.EnsureFolder(UiDir);
            PrototypeBuilder.EnsureFolder(ResourcesDir);
            converted = 0;
            BuildFxPrefabs();

            // L'ordre compte : les prefabs « Audio » et « OptionsMenu » sont créés depuis la scène de jeu,
            // puis la scène du menu convertit les siens en instances de ces mêmes prefabs.
            PatchScene(GameScenePath, PatchGameScene);
            PatchScene(MenuScenePath, PatchMenuScene);

            // Prefabs imbriqués, une fois les scènes converties (convertir un objet vers un prefab
            // qui contient déjà des prefabs imbriqués n'est pas fiable)
            EditPrefab(ShopColumnPath, NestShopColumn);
            EditPrefab(OptionsPath, root => ConvertTextButtons(new[] { root }));
            EditPrefab(LobbySlotPath, root => ConvertTextButtons(new[] { root }));

            AssetDatabase.SaveAssets();
            if (converted > 0) Debug.Log($"[BrotatoLike] Prefabs : {converted} objet(s) converti(s) en instances de prefabs.");
        }

        // ================================================================ Scènes

        static void PatchScene(string path, Func<bool> patch)
        {
            var scene = EditorSceneManager.OpenScene(path);
            int before = CountReferences(AllBehaviours(scene));
            if (!patch()) return;

            // Filet de sécurité : si une conversion a cassé des références, on n'enregistre pas la scène
            int after = CountReferences(AllBehaviours(scene));
            if (after < before)
                throw new Exception($"{scene.name} : {before - after} référence(s) perdue(s) pendant la conversion en prefabs. Scène NON enregistrée.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool PatchGameScene()
        {
            bool changed = ConvertAudio();
            changed |= ConvertOptions();

            var shop = Object.FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
            if (shop != null)
                changed |= Convert(ShopColumnPath, Refs<ShopColumnView>(shop, "columnViews").Select(v => v.gameObject));

            var hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
            if (hud != null)
            {
                var panels = Refs<PlayerPanelView>(hud, "playerPanels").ToList();
                foreach (var p in panels) changed |= NamePanelBars(p);
                changed |= Convert(Ui("PlayerPanel"), panels.Select(p => p.gameObject));

                var end = new SerializedObject(hud).FindProperty("endScreen").objectReferenceValue as EndScreenView;
                if (end != null && end.players != null)
                    changed |= Convert(Ui("EndPlayerColumn"), end.players.Select(c => c.root));
            }

            // En dernier : boutons texte restants (pause...) ; crée le prefab UIButton
            changed |= ConvertTextButtons(SceneManager.GetActiveScene().GetRootGameObjects());
            return changed;
        }

        static bool PatchMenuScene()
        {
            bool changed = ConvertAudio();
            changed |= ConvertOptions();

            var menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
            if (menu != null)
            {
                changed |= Convert(LobbySlotPath, Refs<LobbySlotView>(menu, "slots").Select(v => v.gameObject));
                changed |= PutInRow(Refs<Image>(menu, "menuFarmers").Select(i => (RectTransform)i.transform).ToList(),
                    "Characters", TextAnchor.MiddleCenter);
            }

            var danger = Object.FindFirstObjectByType<DangerMenu>(FindObjectsInactive.Include);
            if (danger != null)
                changed |= PutInRow(Refs<Button>(danger, "buttons").Select(b => (RectTransform)b.transform).ToList(),
                    "Buttons", TextAnchor.UpperCenter);

            changed |= ConvertTextButtons(SceneManager.GetActiveScene().GetRootGameObjects());
            return changed;
        }

        static bool ConvertAudio() =>
            Convert(AudioPath, Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(a => a.gameObject));

        /// <summary>Options (avec l'écran des contrôles dedans) : un seul prefab pour les deux scènes.</summary>
        static bool ConvertOptions() =>
            Convert(OptionsPath, Object.FindObjectsByType<OptionsMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(o => o.gameObject));

        /// <summary>Les barres PV et XP s'appelaient toutes deux « Bar » : des noms uniques pour que la conversion les reconnaisse.</summary>
        static bool NamePanelBars(PlayerPanelView panel)
        {
            if (PrefabUtility.IsPartOfAnyPrefab(panel.gameObject)) return false;
            bool changed = false;
            void Rename(RectTransform fill, string name)
            {
                if (fill == null || fill.parent == null || fill.parent.name == name) return;
                fill.parent.name = name;
                changed = true;
            }
            Rename(panel.hpFill, "HpBar");
            Rename(panel.xpFill, "XpBar");
            return changed;
        }

        /// <summary>
        /// Range des éléments placés à la main dans une rangée avec HorizontalLayoutGroup
        /// (les éléments ajoutés au lancement, quand les données en demandent plus, s'y alignent tout seuls).
        /// </summary>
        static bool PutInRow(List<RectTransform> items, string rowName, TextAnchor alignment)
        {
            items.RemoveAll(i => i == null);
            if (items.Count == 0) return false;
            var first = items[0];
            var parent = (RectTransform)first.parent;
            if (parent.GetComponent<HorizontalLayoutGroup>() != null) return false;

            float spacing = items.Count > 1
                ? Mathf.Abs(items[1].anchoredPosition.x - first.anchoredPosition.x) - first.sizeDelta.x
                : 30f;
            // Centre vertical de la rangée = centre vertical du premier élément (quels que soient ses ancres)
            var corners = new Vector3[4];
            first.GetWorldCorners(corners);
            var center = parent.InverseTransformPoint((corners[0] + corners[2]) / 2f);

            var row = UIFactory.Rect(parent, rowName, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Mathf.Max(parent.rect.width, 1600f), first.sizeDelta.y));
            row.localPosition = new Vector3(0f, center.y, 0f);
            row.SetSiblingIndex(first.GetSiblingIndex());
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = Mathf.Max(0f, spacing);
            layout.childAlignment = alignment;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            foreach (var item in items) item.SetParent(row, false);
            return true;
        }

        // ================================================================ Prefabs imbriqués

        /// <summary>Dans la colonne de boutique : cartes d'offre, cadenas, améliorations, emplacements d'arme, icônes d'objet, boutons.</summary>
        static bool NestShopColumn(GameObject root)
        {
            var v = root.GetComponent<ShopColumnView>();
            if (v == null) return false;
            bool changed = Convert(Ui("OfferCard"), v.offers.Select(o => o.card != null ? o.card.gameObject : null));
            changed |= Convert(Ui("LockButton"), v.offers.Select(o => o.lockButton != null ? o.lockButton.gameObject : null));
            changed |= Convert(Ui("UpgradeCard"), v.upgrades.Select(u => u.button != null ? u.button.gameObject : null));
            changed |= Convert(Ui("WeaponSlot"), v.weaponSlots.Select(b => b != null ? b.gameObject : null));
            changed |= Convert(Ui("ItemIcon"), v.itemIcons.Select(i => i != null ? i.gameObject : null));
            changed |= ConvertTextButtons(new[] { root });
            return changed;
        }

        static void EditPrefab(string path, Func<GameObject, bool> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                int before = CountReferences(behaviours);
                if (!edit(root)) return;
                int after = CountReferences(root.GetComponentsInChildren<MonoBehaviour>(true));
                if (after < before)
                    throw new Exception($"{path} : {before - after} référence(s) perdue(s) en imbriquant les prefabs. Prefab NON enregistré.");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================ Boutons

        /// <summary>Bouton « texte » construit par UIFactory : fond + Button, un seul enfant « Label » (TextMeshPro).</summary>
        static bool IsTextButton(GameObject go)
        {
            if (go.GetComponent<Button>() == null || go.GetComponent<Image>() == null) return false;
            var t = go.transform;
            if (t.childCount != 1) return false;
            var label = t.GetChild(0);
            return label.name == "Label" && label.childCount == 0 && label.GetComponent<TextMeshProUGUI>() != null;
        }

        static bool ConvertTextButtons(IEnumerable<GameObject> roots) =>
            Convert(ButtonPath, roots
                .SelectMany(r => r.GetComponentsInChildren<Button>(true))
                .Select(b => b.gameObject)
                .Where(IsTextButton)
                .ToList());

        // ================================================================ Visuels créés au lancement (Resources)

        /// <summary>Chiffre de dégâts et tourelle : chargés par DamagePopup / DeployedTurrets (repli par code s'ils manquent).</summary>
        static void BuildFxPrefabs()
        {
            CreatePrefabIfMissing(ResourcesDir + "/DamagePopup.prefab", "DamagePopup", go =>
            {
                // Mêmes réglages que le texte créé par code dans DamagePopup.Create
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.fontSize = 4.5f;
                tmp.fontStyle = FontStyles.Normal;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.sortingOrder = 32000;
                tmp.text = "12";
            });
            CreatePrefabIfMissing(ResourcesDir + "/Turret.prefab", "Turret", go => go.AddComponent<SpriteRenderer>());
        }

        static void CreatePrefabIfMissing(string path, string name, Action<GameObject> build)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var go = new GameObject(name);
            try
            {
                build(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[BrotatoLike] Prefab créé : {path}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================ Conversion

        /// <summary>
        /// Convertit des objets ordinaires en instances du prefab <paramref name="path"/>
        /// (créé depuis le premier s'il n'existe pas). Les objets faisant déjà partie d'un prefab sont ignorés.
        /// </summary>
        static bool Convert(string path, IEnumerable<GameObject> objects)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool changed = false;
            foreach (var go in objects.ToList())
            {
                if (go == null || PrefabUtility.IsPartOfAnyPrefab(go)) continue;
                MakeSiblingNamesUnique(go.transform);

                if (asset == null)
                {
                    asset = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
                    Debug.Log($"[BrotatoLike] Prefab créé : {path} (depuis « {go.name} »)");
                }
                else
                {
                    PrefabUtility.ConvertToPrefabInstance(go, asset, ConvertSettings, InteractionMode.AutomatedAction);
                    // Le même objet doit devenir l'instance : sinon les références vers lui seraient perdues
                    if (go == null || !PrefabUtility.IsPartOfPrefabInstance(go))
                        throw new Exception($"Conversion vers {path} : l'objet n'a pas été converti sur place. Arrêt pour ne pas casser la scène.");
                }
                converted++;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// La conversion associe les objets par leur chemin de noms : deux frères du même nom seraient ambigus.
        /// Les doublons deviennent « Nom 2 », « Nom 3 »... (mêmes noms dans chaque exemplaire, construits pareil).
        /// </summary>
        static void MakeSiblingNamesUnique(Transform t)
        {
            var seen = new Dictionary<string, int>();
            foreach (Transform child in t)
            {
                if (seen.TryGetValue(child.name, out int count))
                {
                    seen[child.name] = count + 1;
                    child.name = $"{child.name} {count + 1}";
                }
                else seen[child.name] = 1;
                MakeSiblingNamesUnique(child);
            }
        }

        // ================================================================ Utilitaires

        static IEnumerable<T> Refs<T>(Object owner, string field) where T : Object
        {
            var prop = new SerializedObject(owner).FindProperty(field);
            if (prop == null || !prop.isArray) yield break;
            for (int i = 0; i < prop.arraySize; i++)
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue is T value) yield return value;
        }

        static IEnumerable<MonoBehaviour> AllBehaviours(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true));

        /// <summary>Nombre de références non vides dans nos composants (sert à vérifier qu'une conversion n'en a perdu aucune).</summary>
        static int CountReferences(IEnumerable<MonoBehaviour> behaviours)
        {
            int count = 0;
            foreach (var mb in behaviours)
            {
                if (mb == null || mb.GetType().Namespace == null || !mb.GetType().Namespace.StartsWith("BrotatoLike")) continue;
                var it = new SerializedObject(mb).GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null)
                        count++;
            }
            return count;
        }
    }
}
