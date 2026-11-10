using TMPro;
using UnityEditor;
using UnityEngine;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Police pixel Thaleah partout. Elle n'a pas d'accents : on lui ajoute LiberationSans en police de secours
    /// pour les caractères manquants (é, è, à, ç, •, flèches...).
    /// </summary>
    static class FontBuilder
    {
        const string ThaleahGuid = "067e7e252c8558e4fb0a6d2ca18e8a4f";
        const string LiberationGuid = "8f586378b4e144a9851e7b34d9b748ee";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        static TMP_FontAsset Load(string guid) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

        public static TMP_FontAsset Setup()
        {
            var thaleah = Load(ThaleahGuid);
            var liberation = Load(LiberationGuid);
            if (thaleah == null) return null;

            if (liberation != null)
            {
                thaleah.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
                if (!thaleah.fallbackFontAssetTable.Contains(liberation))
                {
                    thaleah.fallbackFontAssetTable.Add(liberation);
                    EditorUtility.SetDirty(thaleah);
                }
            }

            // Police par défaut de TextMeshPro (textes créés par code : chiffres de dégâts...)
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop.objectReferenceValue != thaleah)
                {
                    prop.objectReferenceValue = thaleah;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            return thaleah;
        }

        /// <summary>Remplace LiberationSans par Thaleah dans la scène ouverte (les polices choisies à la main sont gardées).</summary>
        public static bool ApplyToOpenScene(TMP_FontAsset thaleah)
        {
            if (thaleah == null) return false;
            var liberation = Load(LiberationGuid);
            bool changed = false;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font != null && text.font != liberation) continue;
                text.font = thaleah;
                text.fontSharedMaterial = thaleah.material;
                text.fontStyle &= ~FontStyles.Bold; // pas de faux gras sur une police pixel
                EditorUtility.SetDirty(text);
                changed = true;
            }
            return changed;
        }
    }
}
