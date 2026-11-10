using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Limite les FPS : inutile de calculer 600 images par seconde.
    /// La limite se règle dans les options du jeu (GameSettings.FpsLimit) ; ce composant la réapplique au chargement de la scène.
    /// </summary>
    public class FrameRateLimiter : MonoBehaviour
    {
        void Awake() => GameSettings.ApplyFpsLimit();
    }
}
