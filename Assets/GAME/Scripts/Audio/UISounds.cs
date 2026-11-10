using UnityEngine;

namespace BrotatoLike
{
    [CreateAssetMenu(menuName = "BrotatoLike/Sons/Interface", fileName = "UISounds")]
    public class UISounds : ScriptableObject
    {
        [Header("Navigation")]
        public SoundEvent selection = new();
        public SoundEvent valider = new();
        public SoundEvent retour = new();
        public SoundEvent pause = new();

        [Header("Sélection des personnages")]
        public SoundEvent joueurRejoint = new();
        public SoundEvent pret = new();
        public SoundEvent changementPersonnage = new();
    }
}
