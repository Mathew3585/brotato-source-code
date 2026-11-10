using UnityEngine;

namespace BrotatoLike
{
    [CreateAssetMenu(menuName = "BrotatoLike/Sons/Combat", fileName = "CombatSounds")]
    public class CombatSounds : ScriptableObject
    {
        [Header("Armes")]
        public SoundEvent tir = new();
        public SoundEvent coupDeMelee = new();
        public SoundEvent poseTourelle = new();
        public SoundEvent explosion = new();

        [Header("Ennemis")]
        public SoundEvent impactEnnemi = new();
        public SoundEvent mortEnnemi = new();
        public SoundEvent tirEnnemi = new();

        [Header("Joueur")]
        public SoundEvent joueurTouche = new();
        public SoundEvent mortJoueur = new();
    }
}
