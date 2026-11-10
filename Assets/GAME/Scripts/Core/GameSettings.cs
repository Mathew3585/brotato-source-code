using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Options du joueur (volumes, plein écran, secousses, limite de FPS), sauvegardées entre les sessions.</summary>
    public static class GameSettings
    {
        const string SfxKey = "opt_sfx";
        const string MusicKey = "opt_music";
        const string FullscreenKey = "opt_fullscreen";
        const string ShakeKey = "opt_shake";
        const string FpsKey = "opt_fps";

        /// <summary>Limites de FPS proposées : 0 = fréquence de l'écran, -1 = illimité.</summary>
        public static readonly int[] FpsOptions = { 0, 30, 60, 120, 144, 165, 240, -1 };

        public static float SfxVolume { get; private set; } = 0.8f;
        public static float MusicVolume { get; private set; } = 0.6f;
        public static bool Fullscreen { get; private set; } = true;
        public static bool ScreenShake { get; private set; } = true;
        public static int FpsLimit { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.8f);
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.6f);
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            ScreenShake = PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            FpsLimit = PlayerPrefs.GetInt(FpsKey, 0);
            ApplyFpsLimit();
#if !UNITY_EDITOR
            Screen.fullScreen = Fullscreen;
#endif
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        public static void SetScreenShake(bool value)
        {
            ScreenShake = value;
            PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
        }

        public static void SetFpsLimit(int value)
        {
            FpsLimit = value;
            PlayerPrefs.SetInt(FpsKey, value);
            ApplyFpsLimit();
        }

        public static string FpsLabel(int value) => value switch
        {
            0 => "Screen",
            < 0 => "Unlimited",
            _ => value.ToString(),
        };

        /// <summary>Sans VSync (elle ignorerait la limite). "Screen" = fréquence de l'écran (60, 144…).</summary>
        public static void ApplyFpsLimit()
        {
            QualitySettings.vSyncCount = 0;
            int target = FpsLimit;
            if (target == 0)
            {
                int refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
                target = refresh > 0 ? refresh : 60;
            }
            Application.targetFrameRate = target;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
