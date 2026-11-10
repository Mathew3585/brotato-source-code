using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    [CreateAssetMenu(menuName = "BrotatoLike/Liste de vagues", fileName = "WaveSet")]
    public class WaveSet : ScriptableObject
    {
        [Serializable]
        public class Wave
        {
            public float duration = 20f;
            [Tooltip("Secondes entre deux groupes d'ennemis")]
            public float spawnInterval = 1.5f;
            public int groupSizeMin = 1;
            public int groupSizeMax = 3;
            public int maxAlive = 40;
            [Tooltip("Nombre max d'ennemis qui tirent (mages, canons...) en même temps, en solo. Augmente avec les joueurs.")]
            public int maxShooters = 99;
            [Tooltip("Multiplicateur de PV des ennemis de cette vague (difficulté)")]
            public float enemyHealthMultiplier = 1f;
            [Tooltip("Boss de la vague : la vague ne se termine que quand il meurt")]
            public EnemyData boss;
        }

        [Serializable]
        public class SpawnEntry
        {
            public EnemyData enemy;
            public float weight = 1f;
            [Tooltip("Numéro de vague (à partir de 1) où cet ennemi commence à apparaître")]
            public int fromWave = 1;
            [Tooltip("Dernière vague (0 = jusqu'à la fin)")]
            public int toWave = 0;
        }

        public List<Wave> waves = new();
        public List<SpawnEntry> enemies = new();
        [Tooltip("Chaque joueur en plus ajoute ce % d'ennemis par groupe")]
        public float extraEnemiesPerPlayer = 0.5f;
        [Tooltip("Chaque joueur en plus ajoute ce % de PV à tous les ennemis (boss compris)")]
        public float extraHealthPerPlayer = 0.35f;

        [Tooltip("Coop : chaque joueur en plus autorise ce % de tireurs en plus")]
        public float extraShootersPerPlayer = 0.5f;

        [Header("Butin")]
        [Tooltip("Nombre max de caisses lâchées par vague (élites et chance globale ; le boss en donne toujours une)")]
        public int maxCratesPerWave = 3;
        [Tooltip("Chance qu'un ennemi normal (ni élite ni boss) lâche une caisse")]
        [Range(0f, 0.05f)] public float crateChancePerKill = 0.001f;
        [Tooltip("Multiplicateur global de la chance de lâcher des matériaux (s'ajoute à celui de la difficulté)")]
        [Range(0f, 1f)] public float materialDropMultiplier = 0.6f;

        [Header("Coop : réanimation")]
        [Tooltip("Temps (s) pendant lequel un joueur à terre peut être relevé avant de mourir jusqu'à la vague suivante")]
        public float bleedOutTime = 10f;
        [Tooltip("Temps (s) qu'un coéquipier doit rester à côté pour relever le joueur")]
        public float reviveTime = 3f;
        [Tooltip("Distance max (unités) entre le sauveteur et le joueur à terre")]
        public float reviveRadius = 1.3f;
        [Tooltip("Part des PV max rendue au joueur relevé")]
        [Range(0.05f, 1f)] public float reviveHealth = 0.3f;
        [Tooltip("Invulnérabilité (s) juste après avoir été relevé")]
        public float reviveInvulnerability = 1.5f;

        /// <param name="allowShooters">Faux quand la limite de tireurs de la vague est atteinte.</param>
        public EnemyData PickEnemy(int waveIndex, bool allowShooters = true)
        {
            int waveNumber = waveIndex + 1;
            float total = 0f;
            foreach (var e in enemies)
                if (IsEligible(e, waveNumber, allowShooters)) total += e.weight;
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.value * total;
            foreach (var e in enemies)
            {
                if (!IsEligible(e, waveNumber, allowShooters)) continue;
                roll -= e.weight;
                if (roll <= 0f) return e.enemy;
            }
            return null;
        }

        /// <summary>Ennemi qui tire des projectiles (mage, tête de feu, canon).</summary>
        public static bool IsShooter(EnemyData e) =>
            e != null && e.behavior is EnemyBehavior.Ranged or EnemyBehavior.Turret;

        static bool IsEligible(SpawnEntry e, int waveNumber, bool allowShooters) =>
            (allowShooters || !IsShooter(e.enemy)) && IsEligible(e, waveNumber);

        static bool IsEligible(SpawnEntry e, int waveNumber) =>
            e.enemy != null && waveNumber >= e.fromWave && (e.toWave <= 0 || waveNumber <= e.toWave);
    }
}
