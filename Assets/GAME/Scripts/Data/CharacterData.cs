using UnityEngine;

namespace BrotatoLike
{
    [CreateAssetMenu(menuName = "BrotatoLike/Personnage", fileName = "Character_")]
    public class CharacterData : ScriptableObject
    {
        public string displayName = "Personnage";
        [Tooltip("Style de jeu en quelques mots (affiché à la sélection)")]
        public string tagline = "";
        public Color uiColor = Color.white;

        [Header("Visuel")]
        public Sprite[] idleFrames;
        public Sprite[] runFrames;
        public Sprite deadSprite;
        public float animFps = 10f;

        [Header("Gameplay")]
        public float baseMoveSpeed = 4.5f;
        [Tooltip("Stats de départ (PV max 10 = valeur Brotato)")]
        public StatModifier[] baseStats =
        {
            new(StatType.MaxHealth, 10),
            new(StatType.PickupRange, 2.5f),
        };
        public WeaponData startingWeapon;
    }
}
