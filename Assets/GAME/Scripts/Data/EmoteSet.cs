using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Emotes de la roue (maintenir F / Y, choisir une direction, relâcher).
    /// Asset : Data/Resources/EmoteSet (créé par EmoteBuilder).
    /// </summary>
    [CreateAssetMenu(menuName = "BrotatoLike/Emotes", fileName = "EmoteSet")]
    public class EmoteSet : ScriptableObject
    {
        [Tooltip("Emotes de la roue, dans le sens des aiguilles d'une montre en partant du haut (8 conseillé)")]
        public Sprite[] emotes = new Sprite[0];
        [Tooltip("Rond pixel derrière chaque emote (1 unité de diamètre)")]
        public Sprite slotSprite;

        [Header("Roue")]
        public float wheelRadius = 1.35f;
        public float slotSize = 0.8f;
        [Tooltip("Échelle des sprites d'emoji dans la roue")]
        public float iconScale = 1.8f;
        [Tooltip("Grossissement de l'emote visée")]
        public float selectedScale = 1.35f;

        [Header("Bulle au-dessus du perso")]
        public float bubbleHeight = 1.3f;
        public float bubbleSize = 1f;
        public float bubbleIconScale = 2.4f;
        public float showDuration = 2.5f;
        [Tooltip("Délai minimum entre deux emotes d'un même joueur")]
        public float cooldown = 0.6f;

        static EmoteSet instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static EmoteSet Instance => instance != null ? instance : instance = Resources.Load<EmoteSet>("EmoteSet");

        public static int Count => Instance != null ? Instance.emotes.Length : 0;
    }
}
