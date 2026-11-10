using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Son et ressenti : crée la bibliothèque de sons (Brackeys + Kenney Interface Sounds),
    /// place un AudioManager dans chaque scène, ajoute les sons de boutons et le flash de dégâts.
    /// N'écrase rien : les sons déjà assignés dans la bibliothèque sont conservés.
    /// </summary>
    static class AudioBuilder
    {
        const string ResourcesDir = "Assets/GAME/Data/Resources";
        const string AudioDir = "Assets/GAME/Data/Audio";
        const string MuzzleFlash2Guid = "8dc86788c2b5b6f4c8c37280a947161a";
        const string MuzzleFlash3Guid = "729dd7a288cc6af4cac0edf25f4af631";

        // id, fichiers (sans extension), volume, hauteur min/max, délai anti-spam
        static readonly (SoundId id, string[] clips, float volume, float pitchMin, float pitchMax, float interval)[] Defaults =
        {
            // Combat
            (SoundId.Shoot, new[] { "Shot" }, 0.18f, 0.9f, 1.15f, 0.05f),
            (SoundId.MeleeSwing, new[] { "Whoosh" }, 0.3f, 0.9f, 1.2f, 0.05f),
            (SoundId.EnemyHit, new[] { "Hit" }, 0.3f, 0.85f, 1.2f, 0.03f),
            (SoundId.EnemyDeath, new[] { "GruntVoice01", "GruntVoice02" }, 0.18f, 0.8f, 1.3f, 0.07f),
            (SoundId.EnemyShoot, new[] { "Shot" }, 0.12f, 0.55f, 0.7f, 0.08f),
            (SoundId.Explosion, new[] { "Explosion" }, 0.45f, 0.9f, 1.1f, 0.06f),
            (SoundId.TurretDeploy, new[] { "drop_003" }, 0.6f, 0.9f, 1.1f, 0.1f),
            (SoundId.PlayerHurt, new[] { "GruntVoice02" }, 0.6f, 1f, 1.15f, 0.1f),
            (SoundId.PlayerDeath, new[] { "DeathVoice" }, 0.7f, 1f, 1f, 0.2f),
            // Ramassage / progression
            (SoundId.PickupMaterial, new[] { "glass_001", "glass_002", "glass_003" }, 0.18f, 1f, 1.35f, 0.025f),
            (SoundId.PickupCrate, new[] { "Bonus" }, 0.6f, 1f, 1f, 0.1f),
            (SoundId.LevelUp, new[] { "maximize_006" }, 0.6f, 1f, 1f, 0.1f),
            // Partie
            (SoundId.CountdownTick, new[] { "tick_001" }, 0.6f, 1f, 1f, 0.2f),
            (SoundId.WaveStart, new[] { "Spawn" }, 0.5f, 1f, 1f, 0.5f),
            (SoundId.WaveEnd, new[] { "maximize_004" }, 0.6f, 1f, 1f, 0.5f),
            (SoundId.BossSpawn, new[] { "Explosion" }, 0.8f, 0.45f, 0.5f, 0.5f),
            (SoundId.BossDeath, new[] { "Explosion" }, 1f, 0.6f, 0.65f, 0.5f),
            (SoundId.Victory, new[] { "confirmation_004" }, 0.8f, 1f, 1f, 0.5f),
            (SoundId.GameOver, new[] { "GameOver" }, 0.7f, 1f, 1f, 0.5f),
            // Boutique
            (SoundId.Buy, new[] { "confirmation_001" }, 0.55f, 1f, 1.05f, 0.05f),
            (SoundId.BuyFail, new[] { "error_004" }, 0.5f, 1f, 1f, 0.1f),
            (SoundId.Reroll, new[] { "switch_002" }, 0.55f, 0.95f, 1.05f, 0.05f),
            (SoundId.Lock, new[] { "toggle_001" }, 0.5f, 1f, 1f, 0.05f),
            (SoundId.Merge, new[] { "maximize_002" }, 0.6f, 1f, 1f, 0.1f),
            (SoundId.Sell, new[] { "drop_002" }, 0.55f, 1f, 1f, 0.05f),
            (SoundId.Upgrade, new[] { "confirmation_002" }, 0.55f, 1f, 1.05f, 0.05f),
            // Interface
            (SoundId.UiSelect, new[] { "select_001" }, 0.3f, 0.97f, 1.03f, 0.03f),
            (SoundId.UiConfirm, new[] { "click_002" }, 0.5f, 1f, 1f, 0.05f),
            (SoundId.UiBack, new[] { "back_001" }, 0.5f, 1f, 1f, 0.05f),
            (SoundId.UiJoin, new[] { "maximize_001" }, 0.6f, 1f, 1f, 0.05f),
            (SoundId.UiReady, new[] { "confirmation_003" }, 0.55f, 1f, 1f, 0.05f),
            (SoundId.UiPause, new[] { "open_002" }, 0.5f, 1f, 1f, 0.1f),
            (SoundId.CharacterChange, new[] { "scroll_001", "scroll_002" }, 0.45f, 1f, 1.05f, 0.03f),
        };

        // Couleur des débris par ennemi (seulement si encore à la valeur par défaut)
        static readonly Dictionary<string, Color> DebrisColors = new()
        {
            ["Skeleton"] = new Color(0.9f, 0.9f, 0.85f),
            ["Pirate Skeleton"] = new Color(0.85f, 0.8f, 0.85f),
            ["Tombstone"] = new Color(0.5f, 0.45f, 0.6f),
            ["Dark Mage"] = new Color(0.55f, 0.35f, 0.85f),
            ["Firehead"] = new Color(1f, 0.55f, 0.2f),
            ["Beetle"] = new Color(0.55f, 0.3f, 0.7f),
            ["Giant Beetle"] = new Color(0.35f, 0.75f, 0.7f),
            ["Larva"] = new Color(0.5f, 0.9f, 0.6f),
            ["Crow"] = new Color(0.3f, 0.3f, 0.35f),
            ["Cannon"] = new Color(0.85f, 0.2f, 0.2f),
            ["Ghoul"] = new Color(0.35f, 0.4f, 0.45f),
            ["Dragon"] = new Color(1f, 0.45f, 0.15f),
        };

        /// <summary>
        /// Crée la configuration audio générale (dans Resources) et les 5 catégories de sons (dans Data/Audio).
        /// Seuls les sons encore vides sont remplis : tes choix de clips sont conservés.
        /// </summary>
        public static AudioConfig BuildConfig()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesDir)) AssetDatabase.CreateFolder("Assets/GAME/Data", "Resources");
            if (!AssetDatabase.IsValidFolder(AudioDir)) AssetDatabase.CreateFolder("Assets/GAME/Data", "Audio");

            var config = PrototypeBuilder.LoadOrCreate<AudioConfig>(ResourcesDir + "/AudioConfig.asset", _ => { });
            if (config.combat == null) config.combat = PrototypeBuilder.LoadOrCreate<CombatSounds>(AudioDir + "/CombatSounds.asset", _ => { });
            if (config.interfaceUtilisateur == null) config.interfaceUtilisateur = PrototypeBuilder.LoadOrCreate<UISounds>(AudioDir + "/UISounds.asset", _ => { });
            if (config.boutique == null) config.boutique = PrototypeBuilder.LoadOrCreate<ShopSounds>(AudioDir + "/ShopSounds.asset", _ => { });
            if (config.partie == null) config.partie = PrototypeBuilder.LoadOrCreate<GameSounds>(AudioDir + "/GameSounds.asset", _ => { });
            if (config.ramassage == null) config.ramassage = PrototypeBuilder.LoadOrCreate<PickupSounds>(AudioDir + "/PickupSounds.asset", _ => { });

            foreach (var (id, clips, volume, pitchMin, pitchMax, interval) in Defaults)
            {
                var ev = config.Get(id);
                if (ev == null || ev.HasClips) continue;
                ev.clips = clips.Select(FindClip).Where(c => c != null).ToArray();
                ev.volume = volume;
                ev.pitchMin = pitchMin;
                ev.pitchMax = pitchMax;
                ev.minInterval = interval;
            }

            foreach (var asset in new Object[] { config, config.combat, config.interfaceUtilisateur, config.boutique, config.partie, config.ramassage })
                EditorUtility.SetDirty(asset);
            return config;
        }

        static AudioClip FindClip(string fileName)
        {
            foreach (var guid in AssetDatabase.FindAssets(fileName + " t:AudioClip"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == fileName) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            Debug.LogWarning($"[BrotatoLike] Son introuvable : {fileName}");
            return null;
        }

        public static void ConfigureImports()
        {
            foreach (var guid in new[] { MuzzleFlash2Guid, MuzzleFlash3Guid })
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.filterMode == FilterMode.Point && Mathf.Approximately(importer.spritePixelsPerUnit, 20f)) continue;
                importer.filterMode = FilterMode.Point;
                importer.spritePixelsPerUnit = 20f;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        public static void ApplyDebrisColors(IEnumerable<EnemyData> enemies)
        {
            var defaultColor = new Color(0.45f, 0.7f, 0.4f);
            foreach (var e in enemies)
            {
                if (e == null || !DebrisColors.TryGetValue(e.displayName, out var color) || e.debrisColor != defaultColor) continue;
                e.debrisColor = color;
                EditorUtility.SetDirty(e);
            }
        }

        // ---------------------------------------------------------------- Scènes

        /// <summary>Ajoute ce qui manque dans la scène ouverte (AudioManager, sons de boutons, flash de dégâts).</summary>
        public static bool PatchOpenScene(AudioConfig config)
        {
            bool changed = false;

            var audio = Object.FindFirstObjectByType<AudioManager>();
            if (audio == null)
            {
                audio = new GameObject("Audio").AddComponent<AudioManager>();
                changed = true;
            }
            var so = new SerializedObject(audio);
            if (so.FindProperty("config").objectReferenceValue == null)
            {
                so.FindProperty("config").objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.GetComponent<ButtonSound>() != null) continue;
                button.gameObject.AddComponent<ButtonSound>();
                changed = true;
            }

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null && (gm.muzzleFlashFrames == null || gm.muzzleFlashFrames.Length == 0))
            {
                gm.muzzleFlashFrames = new[] { MuzzleFlash2Guid, MuzzleFlash3Guid }
                    .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null).ToArray();
                EditorUtility.SetDirty(gm);
                changed = true;
            }

            var hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
            if (hud != null)
            {
                var hudSo = new SerializedObject(hud);
                var flashProp = hudSo.FindProperty("damageFlash");
                var hudRoot = hudSo.FindProperty("hudRoot").objectReferenceValue as GameObject;
                if (flashProp.objectReferenceValue == null && hudRoot != null)
                {
                    var rt = UIFactory.Stretch(hudRoot.transform, "DamageFlash");
                    rt.SetAsFirstSibling();
                    flashProp.objectReferenceValue = UIFactory.AddImage(rt, new Color(1f, 0f, 0f, 0f));
                    hudSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            if (changed) EditorSceneManager.MarkSceneDirty(audio.gameObject.scene);
            return changed;
        }
    }
}
