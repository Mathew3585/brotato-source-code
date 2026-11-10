using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Fait apparaître les ennemis pendant les vagues (avec un marqueur d'avertissement avant l'apparition)
    /// et met à jour tous les ennemis actifs.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] SpriteRenderer markerPrefab;
        [SerializeField] float markerDuration = 0.8f;
        [SerializeField] float minDistanceFromPlayers = 4.5f;
        [SerializeField] float groupSpread = 1.2f;

        struct PendingSpawn
        {
            public EnemyData data;
            public Vector2 position;
            public float timer;
            public SpriteRenderer marker;
        }

        readonly List<PendingSpawn> pending = new();
        Pool<SpriteRenderer> markerPool;
        float spawnTimer;
        GameManager gm;

        void Start()
        {
            gm = GameManager.Instance;
            markerPool = new Pool<SpriteRenderer>(markerPrefab, transform, 20);
            gm.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (gm != null) gm.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(GameState state)
        {
            if (state == GameState.Wave)
            {
                spawnTimer = 0.5f;
                if (!NetMode.HasAuthority) return;
                var danger = DifficultySet.Current;
                var boss = gm.CurrentWave.boss;
                if (boss != null)
                {
                    QueueSpawn(boss, FindSpawnPoint(), markerDuration * 2.5f);
                    if (danger.doubleBoss) QueueSpawn(boss, FindSpawnPoint(), markerDuration * 2.5f);
                }
                // Difficulté : une élite au début de certaines vagues
                if (danger.eliteWaves.Contains(gm.WaveIndex + 1))
                {
                    var elite = PickElite();
                    if (elite != null) QueueSpawn(elite, FindSpawnPoint(), markerDuration * 2f);
                }
            }
            // Fin de vague / victoire : les ennemis restants meurent avec leur animation (sans butin)
            else if (state is GameState.Intermission or GameState.Victory) KillAll();
            else ClearAll();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            EnemyGrid.Rebuild(Enemy.Active, Arena.Instance.Bounds);

            // Parcours à l'envers : un ennemi peut se retirer de la liste pendant son Tick
            for (int i = Enemy.Active.Count - 1; i >= 0; i--)
            {
                if (i < Enemy.Active.Count) Enemy.Active[i].Tick(dt);
            }

            if (gm.State == GameState.Wave && NetMode.HasAuthority) HandleSpawning(dt);
            UpdatePending(dt);
        }

        void HandleSpawning(float dt)
        {
            var wave = gm.CurrentWave;
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            spawnTimer = wave.spawnInterval;

            float count = DifficultySet.Current.enemyCount;
            int maxAlive = Mathf.RoundToInt(wave.maxAlive * count);
            int alive = Enemy.Active.Count + pending.Count;
            if (alive >= maxAlive) return;

            int players = Mathf.Max(1, Player.All.Count);
            float multiplier = (1f + gm.waveSet.extraEnemiesPerPlayer * (players - 1)) * count;
            int groupSize = Mathf.RoundToInt(Random.Range(wave.groupSizeMin, wave.groupSizeMax + 1) * multiplier);
            groupSize = Mathf.Min(groupSize, maxAlive - alive);

            // Limite de tireurs : au-delà, on ne fait apparaître que des ennemis de mêlée
            int shooterCap = Mathf.RoundToInt(wave.maxShooters * (1f + gm.waveSet.extraShootersPerPlayer * (players - 1)));
            int shooters = CountShooters();

            Vector2 center = FindSpawnPoint();
            for (int i = 0; i < groupSize; i++)
            {
                var data = gm.waveSet.PickEnemy(gm.WaveIndex, shooters < shooterCap);
                if (data == null) continue;
                if (WaveSet.IsShooter(data)) shooters++;

                var pos = Arena.Instance.Clamp(center + Random.insideUnitCircle * groupSpread, 1f);
                QueueSpawn(data, pos, markerDuration);
            }
        }

        /// <summary>En ligne (hôte) : un marqueur d'apparition vient d'être posé (NetGame le montre aux clients).</summary>
        public static event System.Action<Vector2, float, float> MarkerShown;

        /// <summary>Élite = ennemi qui lâche toujours une caisse (hors boss).</summary>
        EnemyData PickElite()
        {
            var candidates = new List<EnemyData>();
            foreach (var e in gm.waveSet.enemies)
                if (e.enemy != null && !e.enemy.isBoss && e.enemy.isElite && !candidates.Contains(e.enemy))
                    candidates.Add(e.enemy);
            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        int CountShooters()
        {
            int count = 0;
            foreach (var e in Enemy.Active)
                if (e.IsAlive && WaveSet.IsShooter(e.Data)) count++;
            foreach (var p in pending)
                if (WaveSet.IsShooter(p.data)) count++;
            return count;
        }

        void QueueSpawn(EnemyData data, Vector2 pos, float delay)
        {
            float scale = 1.4f * Mathf.Max(1f, data.scale * 0.7f);
            AddMarker(data, pos, delay, scale);
            if (NetMode.IsOnlineHost) MarkerShown?.Invoke(pos, delay, scale);
        }

        /// <summary>Client en ligne : marqueur purement visuel (l'ennemi arrivera par le réseau).</summary>
        public void ShowRemoteMarker(Vector2 pos, float delay, float scale) => AddMarker(null, pos, delay, scale);

        void AddMarker(EnemyData data, Vector2 pos, float delay, float scale)
        {
            var marker = markerPool.Get(pos);
            marker.sortingOrder = -500;
            marker.transform.localScale = Vector3.one * scale;
            pending.Add(new PendingSpawn { data = data, position = pos, timer = delay, marker = marker });
        }

        void UpdatePending(float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                p.timer -= dt;

                // Le marqueur clignote de plus en plus vite
                float blink = Mathf.PingPong(Time.time * 8f, 1f);
                var c = p.marker.color;
                c.a = Mathf.Lerp(0.3f, 0.9f, blink);
                p.marker.color = c;

                if (p.timer > 0f)
                {
                    pending[i] = p;
                    continue;
                }

                markerPool.Release(p.marker);
                pending.RemoveAt(i);
                if (gm.State != GameState.Wave || p.data == null) continue;

                var enemy = gm.EnemyPool.Get(p.position);
                enemy.Spawn(p.data, gm.WaveIndex, gm.CurrentWave.enemyHealthMultiplier);
            }
        }

        Vector2 FindSpawnPoint()
        {
            Vector2 point = Arena.Instance.RandomPoint(1.5f);
            float minSqr = minDistanceFromPlayers * minDistanceFromPlayers;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                bool ok = true;
                foreach (var pl in Player.All)
                {
                    if (pl.IsAlive && (pl.Position - point).sqrMagnitude < minSqr)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) return point;
                point = Arena.Instance.RandomPoint(1.5f);
            }
            return point;
        }

        /// <summary>Supprime tous les ennemis et apparitions en attente (fin de vague, game over).</summary>
        /// <summary>Fait mourir tous les ennemis restants (animation et débris, sans son) sans rien lâcher.</summary>
        public void KillAll()
        {
            foreach (var p in pending) markerPool.Release(p.marker);
            pending.Clear();
            for (int i = Enemy.Active.Count - 1; i >= 0; i--)
            {
                if (i < Enemy.Active.Count && Enemy.Active[i].IsAlive) Enemy.Active[i].Die(false, silent: true);
            }
        }

        public void ClearAll()
        {
            foreach (var p in pending) markerPool.Release(p.marker);
            pending.Clear();
            for (int i = Enemy.Active.Count - 1; i >= 0; i--)
            {
                if (i < Enemy.Active.Count) Enemy.Active[i].Despawn();
            }
        }
    }
}
