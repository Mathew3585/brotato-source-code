using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BrotatoLike
{
    public enum GameState
    {
        WaitingForPlayers, // personne n'a encore rejoint
        Countdown,         // décompte avant la première vague (d'autres joueurs peuvent rejoindre)
        Wave,              // vague en cours
        Intermission,      // fin de vague : les matériaux restants volent vers les joueurs
        Shop,              // améliorations de niveau + boutique, chaque joueur à son rythme
        GameOver,
        Victory,
    }

    /// <summary>Chef d'orchestre de la partie : états, vagues, pools, butin.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Données")]
        public CharacterData[] characters;
        public WaveSet waveSet;
        public ShopCatalog shopCatalog;

        [Header("Prefabs")]
        public Enemy enemyPrefab;
        public Projectile projectilePrefab;
        public Pickup pickupPrefab;

        [Header("Butin")]
        [Tooltip("Sprites des matériaux par valeur croissante (1, 2-4, 5+)")]
        public Sprite[] materialSprites;
        public Sprite crateSprite;

        [Header("Effets")]
        public Sprite[] explosionFrames;
        [Tooltip("Flash au bout du canon quand une arme tire")]
        public Sprite[] muzzleFlashFrames;

        [Header("Rythme")]
        public float startCountdown = 3f;
        [Tooltip("Durée entre la fin de vague et l'ouverture de la boutique")]
        public float collectDuration = 1.5f;

        [Header("Références scène")]
        public EnemySpawner spawner;

        public GameState State { get; private set; } = GameState.WaitingForPlayers;
        public int WaveIndex { get; private set; }
        public int WaveCount => waveSet.waves.Count;
        public WaveSet.Wave CurrentWave => waveSet.waves[Mathf.Clamp(WaveIndex, 0, WaveCount - 1)];
        /// <summary>Temps restant dans l'état courant (vague, décompte, entracte).</summary>
        public float StateTimer { get; private set; }
        public bool IsPaused { get; private set; }
        /// <summary>Joueur qui a mis la pause (c'est lui qui navigue dans le menu pause).</summary>
        public Player PausedBy { get; private set; }
        /// <summary>Boss vivant de la vague en cours (null sinon).</summary>
        public Enemy CurrentBoss { get; private set; }
        /// <summary>Tous les boss vivants (deux au plus haut niveau de difficulté).</summary>
        readonly List<Enemy> bosses = new();
        public bool IsBossWave => State == GameState.Wave && CurrentWave.boss != null;
        /// <summary>Coop : les ennemis ont plus de PV quand il y a plus de joueurs.</summary>
        public float CoopHealthMultiplier => 1f + waveSet.extraHealthPerPlayer * Mathf.Max(0, Player.All.Count - 1);

        public bool PlayersCanMove => State is GameState.Countdown or GameState.Wave or GameState.Intermission;

        public Pool<Enemy> EnemyPool { get; private set; }
        public Pool<Projectile> ProjectilePool { get; private set; }
        public Pool<Pickup> PickupPool { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<Player> PlayerJoined;
        public event Action PauseChanged;

        float stateAge;
        RunSave pendingResume;
        int resumedPlayers;
        float hitStopTimer;
        int lastCountdownSecond = -1;
        int cratesThisWave;

        // Le rechargement de domaine est désactivé dans ce projet : on remet les statiques à zéro à chaque Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Player.All.Clear();
            Player.ClearStaticEvents();
            PlayerShop.ClearStaticEvents();
            Enemy.Active.Clear();
            Pickup.Active.Clear();
            EnemyGrid.Clear();
        }

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            Enemy.Active.Clear();
            Pickup.Active.Clear();
            EnemyGrid.Clear();

            EnemyPool = new Pool<Enemy>(enemyPrefab, CreateContainer("Enemies"), 150);
            ProjectilePool = new Pool<Projectile>(projectilePrefab, CreateContainer("Projectiles"), 100);
            PickupPool = new Pool<Pickup>(pickupPrefab, CreateContainer("Pickups"), 100);
        }

        void Start()
        {
            if (NetMode.IsOnline)
            {
                // En ligne : un personnage par PC, créé par le réseau (OnlineManager / NetPlayer)
                var netPim = FindFirstObjectByType<PlayerInputManager>();
                if (netPim != null)
                {
                    netPim.DisableJoining();
                    netPim.enabled = false;
                }
                return;
            }

            // Joueurs venant de l'écran de sélection : on les recrée avec leurs périphériques.
            // Sans session (scène lancée directement dans l'éditeur), on rejoint en appuyant sur un bouton.
            if (Session.Players.Count == 0) return;
            pendingResume = Session.Resume;
            Session.Resume = null;
            var pim = FindFirstObjectByType<PlayerInputManager>();
            if (pim == null) return;
            pim.DisableJoining();
            for (int i = 0; i < Session.Players.Count; i++)
            {
                var slot = Session.Players[i];
                // Ignore une manette débranchée entre-temps
                var devices = Array.FindAll(slot.devices, d => d != null && d.added);
                if (devices.Length == 0) continue;
                PlayerInput.Instantiate(pim.playerPrefab, i, slot.controlScheme, -1, devices);
            }
        }

        Transform CreateContainer(string containerName)
        {
            var t = new GameObject(containerName).transform;
            t.SetParent(transform);
            return t;
        }

        void Update()
        {
            UpdateHitStop();
            float dt = Time.deltaTime;
            stateAge += dt;

            if (NetMode.IsClientOnly)
            {
                UpdateClient(dt);
                return;
            }
            if (NetMode.IsOnlineHost) UpdateOnlineHost();

            switch (State)
            {
                case GameState.Countdown:
                    StateTimer -= dt;
                    int second = Mathf.CeilToInt(StateTimer);
                    if (second != lastCountdownSecond && second > 0)
                    {
                        lastCountdownSecond = second;
                        AudioManager.Play(SoundId.CountdownTick);
                    }
                    if (StateTimer <= 0f) StartWave(0);
                    break;

                case GameState.Wave:
                    // Vague de boss : le timer s'arrête à 0, la vague finit quand le boss meurt
                    StateTimer = Mathf.Max(0f, StateTimer - dt);
                    foreach (var p in Player.All) p.TickDowned(dt, waveSet);
                    if (StateTimer <= 0f && CurrentWave.boss == null) EndWave();
                    break;

                case GameState.Intermission:
                    StateTimer -= dt;
                    if (StateTimer <= 0f) OpenShop();
                    break;

                case GameState.GameOver:
                case GameState.Victory:
                    if (stateAge > 1.5f && AnyConfirmPressed()) Restart();
                    else if (stateAge > 1.5f && AnyBackPressed()) GoToMainMenu();
                    break;
            }

            TickPickups(dt);
        }

        void TickPickups(float dt)
        {
            for (int i = Pickup.Active.Count - 1; i >= 0; i--)
            {
                if (i < Pickup.Active.Count) Pickup.Active[i].Tick(dt);
            }
        }

        // ---------- Structures (tourelles posées à chaque vague) ----------

        /// <summary>
        /// Pose une tourelle par objet "structure" possédé. Positions fixes autour du joueur de départ,
        /// identiques chez l'hôte et chez les clients.
        /// </summary>
        void DeployStructures()
        {
            var center = (Vector2)Arena.Instance.transform.position;
            foreach (var p in Player.All)
            {
                if (p.Character == null) continue;
                int k = 0;
                foreach (var item in p.Items)
                {
                    if (item == null || item.deployTurret == null) continue;
                    float angle = (p.Index * 90f + 45f + k * 37f) * Mathf.Deg2Rad;
                    float radius = 3f + 1.2f * (k / 4);
                    var pos = Arena.Instance.Clamp(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, 1f);
                    DeployedTurrets.Deploy(p, item.deployTurret, pos, float.MaxValue);
                    k++;
                }
            }
        }

        // ---------- En ligne ----------

        /// <summary>Hôte : gère les joueurs partis en cours de partie.</summary>
        void UpdateOnlineHost()
        {
            if (Player.All.Count == 0) return;
            if (State == GameState.Shop)
            {
                foreach (var p in Player.All)
                    if (!p.IsReady) return;
                StartWave(WaveIndex + 1);
            }
            else if (State == GameState.Wave)
            {
                foreach (var p in Player.All)
                    if (p.IsAlive) return;
                SetState(GameState.GameOver, 0f);
                AudioManager.Play(SoundId.GameOver);
            }
        }

        /// <summary>Client en ligne : pas de logique de jeu, seulement l'affichage.</summary>
        void UpdateClient(float dt)
        {
            if (State is GameState.Wave or GameState.Countdown or GameState.Intermission)
                StateTimer = Mathf.Max(0f, StateTimer - dt);
            if (State is GameState.GameOver or GameState.Victory && stateAge > 1.5f && AnyBackPressed()) GoToMainMenu();
            TickPickups(dt);
        }

        /// <summary>Client en ligne : nouvel état de partie reçu de l'hôte.</summary>
        public void ApplyNetState(GameState state, int wave, float timer)
        {
            WaveIndex = wave;
            if (state == State)
            {
                StateTimer = timer;
                return;
            }
            var previous = State;
            if (state == GameState.Wave) AudioManager.Play(SoundId.WaveStart);
            else if (state == GameState.Intermission) AudioManager.Play(SoundId.WaveEnd);
            else if (state == GameState.GameOver) AudioManager.Play(SoundId.GameOver);
            else if (state == GameState.Victory) AudioManager.Play(SoundId.Victory);
            if (state == GameState.Countdown && previous == GameState.WaitingForPlayers) lastCountdownSecond = -1;
            SetState(state, timer);
            // Client en ligne : mêmes tourelles que l'hôte (positions identiques), juste pour l'affichage
            if (state == GameState.Wave) DeployStructures();
        }

        /// <summary>Client en ligne : corrige le timer affiché (envoyé souvent par l'hôte).</summary>
        public void ApplyNetTimer(float timer)
        {
            if (Mathf.Abs(StateTimer - timer) > 0.25f) StateTimer = timer;
        }

        // ---------- Joueurs ----------

        public void RegisterPlayer(Player player, CharacterData characterOverride = null)
        {
            var character = characterOverride != null ? characterOverride
                : player.Index < Session.Players.Count && Session.Players[player.Index].character != null
                    ? Session.Players[player.Index].character
                    : characters[player.Index % characters.Length];
            Vector2 spawn = (Vector2)Arena.Instance.transform.position + new Vector2((player.Index - 1.5f) * 1.5f, 0f);
            player.Setup(character, spawn);
            player.Shop = new PlayerShop(player, shopCatalog);
            player.Died += OnPlayerDied;

            // Reprise d'une partie sauvegardée : on restaure chaque joueur, puis on rouvre la boutique
            if (pendingResume != null)
            {
                if (player.Index < pendingResume.players.Count)
                    player.RestoreFrom(pendingResume.players[player.Index], ResolveShopItem);
                PlayerJoined?.Invoke(player);
                if (++resumedPlayers >= Mathf.Min(pendingResume.players.Count, Session.Players.Count))
                {
                    WaveIndex = pendingResume.waveIndex;
                    pendingResume = null;
                    SetState(GameState.Shop, 0f);
                }
                return;
            }

            if (NetMode.IsOnline)
            {
                PlayerJoined?.Invoke(player);
                // Hôte : le décompte démarre quand tous les joueurs de la session sont arrivés
                if (NetMode.IsServer && State == GameState.WaitingForPlayers && Player.All.Count >= OnlineManager.ExpectedPlayers)
                    SetState(GameState.Countdown, startCountdown);
                return;
            }

            if (State == GameState.Shop) player.Shop.Open(WaveIndex);
            if (State == GameState.WaitingForPlayers) SetState(GameState.Countdown, startCountdown);
            PlayerJoined?.Invoke(player);
        }

        void OnPlayerDied(Player player)
        {
            if (NetMode.IsClientOnly) return; // en ligne, c'est l'hôte qui décide de la fin de partie
            foreach (var p in Player.All)
                if (p.IsAlive) return;
            SaveSystem.Delete();
            SetState(GameState.GameOver, 0f);
            AudioManager.Play(SoundId.GameOver);
        }

        // ---------- Vagues ----------

        void StartWave(int index)
        {
            WaveIndex = index;
            cratesThisWave = 0;
            foreach (var p in Player.All) p.ResetForWave();
            SetState(GameState.Wave, CurrentWave.duration);
            AudioManager.Play(SoundId.WaveStart);
            DeployStructures();
        }

        void EndWave()
        {
            // Récolte (stat Harvesting) : matériaux bonus à chaque fin de vague, même pour un joueur mort
            foreach (var p in Player.All) p.Harvest();

            // Les matériaux restants au sol sont distribués aux joueurs vivants
            int i = 0;
            foreach (var pickup in Pickup.Active)
            {
                var alive = NthAlivePlayer(i++);
                if (alive != null) pickup.FlyTo(alive);
            }

            if (WaveIndex >= WaveCount - 1)
            {
                SaveSystem.Delete();
                DifficultySet.Unlock(Session.Difficulty + 1); // victoire : débloque le niveau suivant
                SetState(GameState.Victory, 0f);
                AudioManager.Play(SoundId.Victory);
            }
            else
            {
                SetState(GameState.Intermission, collectDuration);
                AudioManager.Play(SoundId.WaveEnd);
            }
        }

        void OpenShop()
        {
            foreach (var p in Player.All)
            {
                p.IsReady = false;
                p.Shop.Open(WaveIndex);
            }
            SetState(GameState.Shop, 0f);
            SaveRun();
        }

        // ---------- Debug (console) ----------

        public void DebugEndWave()
        {
            if (State == GameState.Wave) EndWave();
        }

        /// <summary>Ouvre la boutique juste avant la vague demandée (numéro à partir de 1).</summary>
        public void DebugGoToWave(int waveNumber)
        {
            if (Player.All.Count == 0) return;
            int target = Mathf.Clamp(waveNumber, 1, WaveCount) - 1;
            if (target == 0)
            {
                StartWave(0);
                return;
            }
            WaveIndex = target - 1;
            foreach (var p in Player.All) p.ResetForWave();
            OpenShop();
        }

        /// <summary>Sauvegarde la partie (uniquement pendant la boutique : c'est le point de reprise).</summary>
        public void SaveRun()
        {
            if (State != GameState.Shop || NetMode.IsOnline) return;
            var save = new RunSave { waveIndex = WaveIndex, difficulty = Session.Difficulty };
            foreach (var p in Player.All)
                if (p.Character != null) save.players.Add(p.ToSave());
            SaveSystem.Save(save);
        }

        public ShopItemData ResolveShopItem(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            foreach (var w in shopCatalog.weapons)
                if (w != null && w.name == assetName) return w;
            foreach (var i in shopCatalog.items)
                if (i != null && i.name == assetName) return i;
            return null;
        }

        public CharacterData FindCharacter(string assetName)
        {
            foreach (var c in characters)
                if (c != null && c.name == assetName) return c;
            return null;
        }

        /// <summary>Appelé par l'UI de boutique : la vague suivante démarre quand tous les joueurs sont prêts.</summary>
        public void SetPlayerReady(Player player, bool ready)
        {
            player.IsReady = ready;
            if (State != GameState.Shop) return;
            foreach (var p in Player.All)
                if (!p.IsReady) return;
            StartWave(WaveIndex + 1);
        }

        static Player NthAlivePlayer(int n)
        {
            int aliveCount = 0;
            foreach (var p in Player.All)
                if (p.IsAlive) aliveCount++;
            if (aliveCount == 0) return null;

            int target = n % aliveCount;
            foreach (var p in Player.All)
            {
                if (!p.IsAlive) continue;
                if (target-- == 0) return p;
            }
            return null;
        }

        public void RegisterBoss(Enemy boss)
        {
            bosses.Add(boss);
            if (CurrentBoss == null) CurrentBoss = boss;
            AudioManager.Play(SoundId.BossSpawn);
            if (CameraController.Instance) CameraController.Instance.Shake(0.4f);
        }

        public void OnBossKilled(Enemy boss)
        {
            if (!bosses.Remove(boss)) return;
            // La barre de vie passe au boss suivant ; la vague se termine quand tous sont morts
            CurrentBoss = bosses.Count > 0 ? bosses[0] : null;
            if (CameraController.Instance) CameraController.Instance.Shake(0.6f);
            AudioManager.Play(SoundId.BossDeath);
            HitStop(0.25f);
            if (bosses.Count == 0 && State == GameState.Wave) EndWave();
        }

        void SetState(GameState state, float timer)
        {
            if (state != GameState.Wave)
            {
                CurrentBoss = null;
                bosses.Clear();
            }
            EnemyBullets.ClearAll();
            if (state != GameState.Wave)
            {
                DeployedTurrets.ClearAll();
                // Fin de vague : les joueurs encore à terre restent morts jusqu'à la vague suivante
                foreach (var p in Player.All) p.ClearDowned();
            }
            State = state;
            StateTimer = timer;
            stateAge = 0f;
            StateChanged?.Invoke(state);
            Challenges.OnStateChanged(this, state);
        }

        // ---------- Butin ----------

        public void DropMaterials(Vector2 position, int amount)
        {
            while (amount > 0)
            {
                int value = amount >= 5 ? 5 : amount >= 2 ? 2 : 1;
                amount -= value;
                int spriteIndex = value >= 5 ? 2 : value >= 2 ? 1 : 0;
                var sprite = materialSprites[Mathf.Min(spriteIndex, materialSprites.Length - 1)];
                var pickup = PickupPool.Get(position);
                pickup.Spawn(value, sprite, UnityEngine.Random.insideUnitCircle * 3f, netSprite: (byte)spriteIndex);
            }
        }

        /// <param name="force">Boss : la caisse ne compte pas dans la limite par vague.</param>
        public void DropCrate(Vector2 position, bool force = false)
        {
            if (crateSprite == null) return;
            if (!force)
            {
                if (cratesThisWave >= waveSet.maxCratesPerWave) return;
                cratesThisWave++;
            }
            var pickup = PickupPool.Get(position);
            pickup.Spawn(1, crateSprite, UnityEngine.Random.insideUnitCircle * 2f, crate: true, netSprite: 3);
        }

        // ---------- Pause / redémarrage ----------

        public void TogglePause(Player by)
        {
            if (State is GameState.GameOver or GameState.Victory or GameState.WaitingForPlayers or GameState.Shop) return;
            if (OptionsMenu.AnyOpen || OptionsMenu.LastClosedFrame == Time.frameCount) return;
            IsPaused = !IsPaused;
            PausedBy = IsPaused ? by : null;
            hitStopTimer = 0f;
            // En ligne, impossible de figer le jeu des autres : le menu s'ouvre mais la partie continue
            Time.timeScale = IsPaused && !NetMode.IsOnline ? 0f : 1f;
            AudioManager.Play(IsPaused ? SoundId.UiPause : SoundId.UiBack);
            PauseChanged?.Invoke();
        }

        /// <summary>
        /// Micro-pause ("hit-stop") : le jeu ralentit presque à l'arrêt quelques centièmes de seconde
        /// pour donner du poids aux gros coups.
        /// </summary>
        public void HitStop(float duration)
        {
            if (IsPaused || State != GameState.Wave || NetMode.IsOnline) return;
            hitStopTimer = Mathf.Max(hitStopTimer, duration);
            Time.timeScale = 0.05f;
        }

        void UpdateHitStop()
        {
            if (hitStopTimer <= 0f) return;
            hitStopTimer -= Time.unscaledDeltaTime;
            if (hitStopTimer <= 0f && !IsPaused) Time.timeScale = 1f;
        }

        public static void GoToMainMenu()
        {
            if (NetMode.IsOnline && OnlineManager.Instance != null)
            {
                OnlineManager.Instance.LeaveSession();
                return;
            }
            SceneTransition.Cover(() =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(Session.MenuScene);
            });
        }

        static bool AnyBackPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            foreach (var pad in Gamepad.all)
                if (pad.buttonEast.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame) return true;
            return false;
        }

        static bool AnyConfirmPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) return true;
            return false;
        }

        static void Restart()
        {
            if (NetMode.IsOnline)
            {
                // En ligne : l'hôte ramène tout le monde au lobby (choix des personnages)
                if (NetMode.IsServer && OnlineManager.Instance != null) OnlineManager.Instance.ReturnToLobby();
                return;
            }
            SceneTransition.Cover(() =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });
        }
    }
}
