using UnityEngine;

namespace BrotatoLike
{
    [CreateAssetMenu(menuName = "BrotatoLike/Sons/Ramassage", fileName = "PickupSounds")]
    public class PickupSounds : ScriptableObject
    {
        public SoundEvent materiau = new();
        public SoundEvent caisse = new();
        public SoundEvent niveauSuperieur = new();
    }
}
