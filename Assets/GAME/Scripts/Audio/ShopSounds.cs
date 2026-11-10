using UnityEngine;

namespace BrotatoLike
{
    [CreateAssetMenu(menuName = "BrotatoLike/Sons/Boutique", fileName = "ShopSounds")]
    public class ShopSounds : ScriptableObject
    {
        public SoundEvent achat = new();
        public SoundEvent achatRefuse = new();
        public SoundEvent relance = new();
        public SoundEvent cadenas = new();
        public SoundEvent fusion = new();
        public SoundEvent vente = new();
        public SoundEvent amelioration = new();
    }
}
