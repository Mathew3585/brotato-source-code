using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Réglages du ressenti (« juice ») : écrasement à l'impact, étincelles, secousses, combo de ramassage...
    /// Asset : Data/Resources/JuiceSettings (valeurs par défaut si absent). Mettre une valeur à 0 désactive l'effet.
    /// </summary>
    [CreateAssetMenu(menuName = "BrotatoLike/Juice Settings", fileName = "JuiceSettings")]
    public class JuiceSettings : ScriptableObject
    {
        [Header("Ennemi touché")]
        [Tooltip("Écrasement du sprite à l'impact (0.25 = 25 % plus large, 25 % moins haut)")]
        [Range(0f, 0.6f)] public float hitSquash = 0.28f;
        [Tooltip("Durée du retour à la normale")]
        public float hitSquashTime = 0.12f;
        [Tooltip("Étincelles à chaque coup (coup critique : le double, en doré)")]
        public int hitSparks = 2;

        [Header("Ennemi tué")]
        [Tooltip("Petite secousse de caméra à chaque ennemi tué (les secousses ne s'additionnent pas)")]
        public float killShake = 0.05f;
        [Tooltip("L'ennemi gonfle un peu avant de rétrécir en mourant (0.4 = +40 %)")]
        [Range(0f, 1f)] public float deathPop = 0.4f;

        [Header("Matériaux")]
        [Tooltip("Apparition des matériaux : petit « pop » d'échelle")]
        public float pickupPopTime = 0.18f;
        [Tooltip("Ramassages enchaînés : le son monte d'autant à chaque matériau...")]
        public float pickupPitchStep = 0.04f;
        [Tooltip("... jusqu'à ce maximum")]
        public float pickupPitchMax = 1.7f;
        [Tooltip("Délai max entre deux ramassages pour continuer le combo")]
        public float pickupComboWindow = 0.45f;

        [Header("Niveau supérieur")]
        public int levelUpDebris = 26;
        public float levelUpShake = 0.15f;
        public Color levelUpColor = new(1f, 0.9f, 0.35f);

        [Header("Joueur")]
        [Tooltip("En dessous de cette part de PV : le perso pulse en rouge et l'écran a un voile rouge")]
        [Range(0f, 1f)] public float lowHealthThreshold = 0.3f;
        [Range(0f, 0.5f)] public float lowHealthVignette = 0.12f;

        [Header("HUD")]
        [Tooltip("Dernières secondes de la vague : le timer pulse avec un tic")]
        public int timerWarningSeconds = 5;
        [Tooltip("Rebond du compteur de matériaux / du niveau quand ils augmentent")]
        public float hudPunch = 0.25f;

        // ---------------------------------------------------------------- Accès

        static JuiceSettings instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static JuiceSettings Settings
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<JuiceSettings>("JuiceSettings");
                if (instance == null) instance = CreateInstance<JuiceSettings>();
                return instance;
            }
        }
    }
}
