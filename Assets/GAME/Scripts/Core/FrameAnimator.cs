using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Animation de sprites ultra légère (pas d'Animator), pilotée par son propriétaire.</summary>
    public struct FrameAnimator
    {
        Sprite[] frames;
        float fps;
        float time;

        public void Play(Sprite[] newFrames, float framesPerSecond)
        {
            if (frames == newFrames) return;
            frames = newFrames;
            fps = framesPerSecond;
            time = 0f;
        }

        public Sprite Tick(float deltaTime)
        {
            if (frames == null || frames.Length == 0) return null;
            time += deltaTime;
            return frames[(int)(time * fps) % frames.Length];
        }
    }

    public static class SortingUtil
    {
        /// <summary>Ordre de rendu basé sur Y : ce qui est plus bas à l'écran passe devant.</summary>
        public static int OrderFromY(float y) => Mathf.RoundToInt(-y * 20f);
    }
}
