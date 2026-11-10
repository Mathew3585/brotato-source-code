using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Roue des emotes : rond pixel des slots (Art/UI/EmoteSlot.png), asset Data/Resources/EmoteSet
    /// (8 emojis du pack Arlan Trindade) et prefab Prefabs/Resources/EmoteWheel. N'écrase rien d'existant.
    /// </summary>
    static class EmoteBuilder
    {
        const string SlotSpritePath = "Assets/GAME/Art/UI/EmoteSlot.png";
        const string SetPath = "Assets/GAME/Data/Resources/EmoteSet.asset";
        const string PrefabPath = "Assets/GAME/Prefabs/Resources/EmoteWheel.prefab";
        const string EmojiDir = "Assets/_External/Art/Arlan Trindade/Free emojis pixel art/emojis-x1-32x32";
        const int SlotPixels = 16;

        // Dans le sens des aiguilles d'une montre en partant du haut :
        // pouce levé, mort de rire, amoureux, peur, pouce baissé, pleure, colère, attention
        static readonly int[] DefaultEmojis = { 41, 3, 70, 52, 42, 6, 71, 44 };

        [MenuItem("BrotatoLike/Emotes : roue des emotes", priority = 14)]
        public static void Run()
        {
            var slot = BuildSlotSprite();
            var set = PrototypeBuilder.LoadOrCreate<EmoteSet>(SetPath, s =>
            {
                s.emotes = DefaultEmojis
                    .Select(n => AssetDatabase.LoadAssetAtPath<Sprite>($"{EmojiDir}/E{n}.png"))
                    .Where(sprite => sprite != null)
                    .ToArray();
                s.slotSprite = slot;
            });
            if (set.slotSprite == null)
            {
                set.slotSprite = slot;
                EditorUtility.SetDirty(set);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var wheel = EmoteWheel.BuildDefault(slot);
                PrefabUtility.SaveAsPrefabAsset(wheel.gameObject, PrefabPath);
                Object.DestroyImmediate(wheel.gameObject);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BrotatoLike] Roue des emotes prête ({set.emotes.Length} emotes).");
        }

        /// <summary>Rond blanc de 16 px (bord un peu plus sombre), teinté par la roue.</summary>
        static Sprite BuildSlotSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SlotSpritePath);
            if (existing != null) return existing;

            var tex = new Texture2D(SlotPixels, SlotPixels, TextureFormat.RGBA32, false);
            float c = (SlotPixels - 1) / 2f;
            float r = SlotPixels / 2f;
            for (int y = 0; y < SlotPixels; y++)
            for (int x = 0; x < SlotPixels; x++)
            {
                float d = new Vector2(x - c, y - c).magnitude;
                var color = d > r - 0.5f ? Color.clear : d > r - 1.5f ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
                tex.SetPixel(x, y, color);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SlotSpritePath));
            File.WriteAllBytes(SlotSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SlotSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SlotSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SlotPixels;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SlotSpritePath);
        }
    }
}
