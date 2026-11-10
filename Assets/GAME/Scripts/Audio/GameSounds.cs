using UnityEngine;

namespace BrotatoLike
{
    [CreateAssetMenu(menuName = "BrotatoLike/Sons/Partie", fileName = "GameSounds")]
    public class GameSounds : ScriptableObject
    {
        [Header("Vagues")]
        public SoundEvent decompte = new();
        public SoundEvent debutVague = new();
        public SoundEvent finVague = new();

        [Header("Boss")]
        public SoundEvent arriveeBoss = new();
        public SoundEvent mortBoss = new();

        [Header("Fin de partie")]
        public SoundEvent victoire = new();
        public SoundEvent gameOver = new();
    }
}
