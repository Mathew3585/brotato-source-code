using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Prefab « SceneTransition » (cercle qui se ferme au centre entre deux scènes) et son instance dans chaque scène.
    /// Le prefab n'est jamais recréé : modifie-le librement (couleurs, texte, durées). Le matériau
    /// (Shaders/IrisTransition.mat) règle le cercle : taille des pixels, couleur et épaisseur du liseré.
    /// </summary>
    static class TransitionBuilder
    {
        const string PrefabPath = "Assets/GAME/Prefabs/UI/SceneTransition.prefab";
        const string ShaderPath = "Assets/GAME/Shaders/IrisTransition.shader";
        const string MaterialPath = "Assets/GAME/Shaders/IrisTransition.mat";

        public static GameObject BuildPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab();
            UseIris(prefab);
            return prefab;
        }

        static GameObject CreatePrefab()
        {
            // Toujours au-dessus de tout le reste (pause, console...)
            var temp = new GameObject("Temp");
            var root = UIFactory.CreateCanvas(temp.transform, "SceneTransition", 1000, interactive: true);
            root.SetParent(null, false);
            Object.DestroyImmediate(temp);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var curtain = UIFactory.Stretch(root, "Curtain");
            UIFactory.AddImage(curtain, new Color(0.05f, 0.06f, 0.07f), raycast: true);

            var labelRt = UIFactory.Rect(curtain, "Label", new Vector2(1f, 0f), new Vector2(-70f, 60f), new Vector2(600f, 70f));
            labelRt.pivot = new Vector2(1f, 0f);
            labelRt.anchoredPosition = new Vector2(-70f, 60f);
            var label = UIFactory.Text(labelRt, 48, TextAlignmentOptions.BottomRight);
            label.text = "LOADING...";
            label.color = UIFactory.Accent;
            label.enabled = false;

            var view = root.gameObject.AddComponent<SceneTransition>();
            var so = new SerializedObject(view);
            so.FindProperty("curtain").objectReferenceValue = curtain;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);
            Debug.Log($"[BrotatoLike] Prefab créé : {PrefabPath}");
            return prefab;
        }

        /// <summary>Matériau du cercle (créé une fois, jamais écrasé).</summary>
        static Material IrisMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[BrotatoLike] Shader introuvable : {ShaderPath} (transition en simple fondu).");
                return null;
            }
            var material = new Material(shader) { name = "IrisTransition" };
            material.SetColor("_EdgeColor", UIFactory.Accent);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>Ancien rideau qui glissait → image fixe avec le matériau du cercle (sans les liserés de côté).</summary>
        static void UseIris(GameObject prefab)
        {
            var material = IrisMaterial();
            if (material == null) return;
            var view = prefab.GetComponent<SceneTransition>();
            var curtainRt = view != null ? new SerializedObject(view).FindProperty("curtain").objectReferenceValue as RectTransform : null;
            var image = curtainRt != null ? curtainRt.GetComponent<Image>() : null;
            if (image == null || image.material == material) return;

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var curtain = (RectTransform)new SerializedObject(root.GetComponent<SceneTransition>()).FindProperty("curtain").objectReferenceValue;
                curtain.GetComponent<Image>().material = material;
                curtain.anchoredPosition = Vector2.zero;
                foreach (var edge in new[] { "EdgeLeft", "EdgeRight" })
                {
                    var t = curtain.Find(edge);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[BrotatoLike] Transition : passage au cercle (iris).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Pose une instance du prefab dans la scène ouverte si elle n'en a pas.</summary>
        public static bool PatchOpenScene(GameObject prefab)
        {
            if (prefab == null) return false;
            if (Object.FindAnyObjectByType<SceneTransition>(FindObjectsInactive.Include) != null) return false;
            PrefabUtility.InstantiatePrefab(prefab);
            return true;
        }
    }
}
