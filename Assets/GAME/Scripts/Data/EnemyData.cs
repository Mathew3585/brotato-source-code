using UnityEngine;

namespace BrotatoLike
{
    public enum EnemyBehavior
    {
        Chaser,  // fonce sur le joueur le plus proche
        Ranged,  // garde ses distances et tire
        Charger, // s'approche, se prépare (clignote) puis charge en ligne droite
        Turret,  // immobile, tire des salves
        Boss,    // enchaîne plusieurs attaques (salves, charges, invocations)
    }

    [CreateAssetMenu(menuName = "BrotatoLike/Ennemi", fileName = "Enemy_")]
    public class EnemyData : ScriptableObject
    {
        public string displayName = "Ennemi";
        public EnemyBehavior behavior = EnemyBehavior.Chaser;
        public bool isBoss;

        [Header("Visuel")]
        public Sprite[] runFrames;
        [Tooltip("Optionnel : sinon l'ennemi clignote en rouge quand il est touché")]
        public Sprite hitSprite;
        [Tooltip("Optionnel : sinon l'ennemi rétrécit et disparaît")]
        public Sprite deadSprite;
        public float animFps = 8f;
        public float scale = 1f;
        public Color tint = Color.white;
        [Tooltip("Le sprite regarde vers la gauche (par défaut on suppose vers la droite)")]
        public bool facesLeft;
        [Tooltip("Le sprite tourne dans la direction du mouvement (insectes vus de dessus)")]
        public bool rotateToDirection;
        public float rotationOffset = -90f;
        [Tooltip("Couleur des débris à la mort")]
        public Color debrisColor = new(0.45f, 0.7f, 0.4f);
        [Tooltip("Rebond de marche pour les sprites sans animation")]
        public float bobAmount = 0.08f;

        [Header("Stats (vague 1) + croissance par vague")]
        public float maxHealth = 4f;
        public float healthPerWave = 2f;
        public float moveSpeed = 1.8f;
        public float contactDamage = 1f;
        public float damagePerWave = 0.6f;

        [Header("Physique")]
        public float radius = 0.35f;
        [Range(0f, 1f)] public float knockbackResistance = 0f;

        [Header("Tir (Ranged / Turret / Boss)")]
        public Sprite projectileSprite;
        public Color projectileColor = Color.white;
        public float projectileScale = 1f;
        public float projectileSpeed = 5f;
        public float projectileDamage = 1f;
        public float projectileDamagePerWave = 0.5f;
        public float fireInterval = 2f;
        public int projectilesPerShot = 1;
        [Tooltip("Angle total de la salve (360 = en étoile)")]
        public float projectileSpread = 0f;
        [Tooltip("Ranged : distance que l'ennemi essaie de garder")]
        public float preferredDistance = 5f;

        [Header("Charge (Charger / Boss)")]
        public float chargeTriggerDistance = 5f;
        public float chargeWindup = 0.6f;
        public float chargeSpeed = 9f;
        public float chargeDuration = 0.45f;

        [Header("Division / invocation")]
        [Tooltip("Ennemi créé à la mort (Splitter) ou invoqué (Boss)")]
        public EnemyData splitInto;
        public int splitCount = 0;

        [Header("Butin")]
        public int materialDrop = 1;
        [Range(0f, 1f)] public float dropChance = 1f;
        [Tooltip("Chance de lâcher une caisse (le boss en lâche toujours une ; plafond par vague : WaveSet.maxCratesPerWave)")]
        [Range(0f, 1f)] public float crateChance = 0f;
        [Tooltip("Élite : apparaît aux vagues d'élites de la difficulté, flèche hors écran, grosse mort. Indépendant de la chance de caisse.")]
        public bool isElite;

        public float HealthAtWave(int waveIndex) => maxHealth + healthPerWave * waveIndex;
        public float DamageAtWave(int waveIndex) => contactDamage + damagePerWave * waveIndex;
        public float ProjectileDamageAtWave(int waveIndex) => projectileDamage + projectileDamagePerWave * waveIndex;
    }
}
