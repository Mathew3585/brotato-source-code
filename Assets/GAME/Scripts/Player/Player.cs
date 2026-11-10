using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Un joueur. Trois façons d'être contrôlé :
    /// - Local : coop sur le même PC (PlayerInput, créé par le PlayerInputManager).
    /// - OnlineOwner : le joueur de CE PC dans une partie en ligne (mêmes contrôles, sans PlayerInput).
    /// - Remote : le joueur d'un autre PC (position et état reçus par le réseau).
    /// Aucun singleton : Player.All contient tous les joueurs.
    /// </summary>
    public class Player : MonoBehaviour
    {
        public enum ControlMode { Local, OnlineOwner, Remote }

        public static readonly List<Player> All = new();

        public const string KeyboardScheme = "Keyboard&Mouse";
        const float InvulnerabilityDuration = 0.5f;

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer shadow;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] float radius = 0.3f;

        public ControlMode Mode { get; private set; } = ControlMode.Local;
        public bool IsRemote => Mode == ControlMode.Remote;
        /// <summary>Joueur contrôlé sur ce PC (local ou en ligne).</summary>
        public bool IsLocallyControlled => Mode != ControlMode.Remote;
        /// <summary>Vivant avec peu de PV (seuil : JuiceSettings.lowHealthThreshold).</summary>
        public bool IsLowHealth => IsAlive && MaxHealth > 0f && Health / MaxHealth <= JuiceSettings.Settings.lowHealthThreshold;

        public CharacterData Character { get; private set; }
        public PlayerStats Stats { get; } = new();
        public WeaponHolder Weapons => weapons;
        public int Index { get; private set; }
        /// <summary>Couleur du joueur (J1 bleu, J2 rose, J3 vert, J4 jaune), indépendante du personnage.</summary>
        public Color Color => Session.ColorOf(Index);

        public Vector2 Position { get; private set; }
        public float Radius => radius;
        public bool IsAlive { get; private set; }
        /// <summary>Coop : à terre (pas vivant), un coéquipier peut le relever avant la fin du compte à rebours.</summary>
        public bool IsDowned { get; private set; }
        /// <summary>Temps restant avant la mort (1 = plein, 0 = mort jusqu'à la vague suivante).</summary>
        public float BleedOut01 { get; private set; }
        /// <summary>Progression de la réanimation (1 = relevé).</summary>
        public float Revive01 { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth => Mathf.Max(1f, Stats[StatType.MaxHealth]);
        public int Materials { get; private set; }
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public float XpToNextLevel => (Level + 3) * (Level + 3);
        public int PendingLevelUps
        {
            get => pendingLevelUps;
            set { pendingLevelUps = value; MarkNetDirty(); }
        }
        public int PendingCrates
        {
            get => pendingCrates;
            set { pendingCrates = value; MarkNetDirty(); }
        }
        public RunStats RunStats { get; private set; } = new();

        public bool ManualAim { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public bool UsesKeyboard => input != null ? input.currentControlScheme == KeyboardScheme : lastInputWasKeyboard;

        // Boutique / inventaire
        public PlayerShop Shop { get; set; }
        public bool IsReady { get; set; }
        public IReadOnlyList<ItemData> Items => items;
        /// <summary>Actions de ce joueur (déplacement, visée, UI...). Null pour un joueur distant.</summary>
        public InputActionAsset Actions { get; private set; }
        /// <summary>Système d'événements UI propre à ce joueur (navigation avec SA manette / SON clavier).</summary>
        public MultiplayerEventSystem EventSystem { get; private set; }

        /// <summary>En ligne (hôte) : l'état lourd (stats, objets, armes, boutique) a changé et doit être renvoyé.</summary>
        public bool NetDirty { get; private set; }
        /// <summary>En ligne (client) : un nouvel état vient d'arriver de l'hôte.</summary>
        public event Action<Player> NetStateApplied;

        readonly List<ItemData> items = new();

        public event Action<Player> Died;
        /// <summary>Un joueur (n'importe lequel) vient de perdre des PV.</summary>
        public static event Action<Player> AnyDamaged;
        /// <summary>Commande de debug "god".</summary>
        public static bool DebugInvincible;
        public event Action<Player> LevelledUp;
        /// <summary>Ce joueur (contrôlé sur ce PC) vient de jouer une emote : NetPlayer l'envoie aux autres PC.</summary>
        public event Action<int> EmotePlayed;

        PlayerInput input;
        InputAction moveAction, aimPointerAction, aimStickAction, toggleAimAction, pauseAction, emoteAction;
        EmoteWheel emoteWheel;
        float nextEmoteTime;
        FrameAnimator anim;
        float invulnerableTimer;
        float walkTime;
        float hurtFlash;
        float levelUpFlash;
        int pendingLevelUps;
        int pendingCrates;
        bool lastInputWasKeyboard = true;
        Vector2 remoteVelocity;
        Vector2 lastRemotePosition;
        DownedIndicator downedIndicator;
        float helpPopupTimer;
        float nextLifeSteal;

        static readonly Color ReviveColor = new(0.35f, 1f, 0.45f);

        void Awake() => input = GetComponent<PlayerInput>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnDestroy()
        {
            // Copie des actions créée pour le joueur en ligne
            if (input == null && Actions != null) Destroy(Actions);
        }

        void Start()
        {
            // Coop locale : PlayerInput n'assigne son index et ses actions qu'après son OnEnable, on les lit ici.
            // En ligne, c'est NetPlayer qui appelle InitOnline.
            if (input == null) return;
            Mode = ControlMode.Local;
            Index = input.playerIndex;
            BindActions(input.actions);
            CreateEventSystem(input.actions);
            GameManager.Instance.RegisterPlayer(this);
        }

        /// <summary>Partie en ligne : appelé par NetPlayer quand l'objet réseau est prêt.</summary>
        public void InitOnline(int index, bool isOwner, InputActionAsset controlsTemplate, CharacterData character)
        {
            Index = index;
            Mode = isOwner ? ControlMode.OnlineOwner : ControlMode.Remote;
            if (isOwner && controlsTemplate != null)
            {
                // 1 joueur par PC : ses actions écoutent tous les périphériques (clavier, souris, manettes)
                var asset = Instantiate(controlsTemplate);
                asset.Enable();
                BindActions(asset);
                CreateEventSystem(asset);
            }
            lastRemotePosition = transform.position;
            GameManager.Instance.RegisterPlayer(this, character);
        }

        void BindActions(InputActionAsset asset)
        {
            Actions = asset;
            moveAction = asset["Player/Move"];
            aimPointerAction = asset["Player/AimPointer"];
            aimStickAction = asset["Player/AimStick"];
            toggleAimAction = asset["Player/ToggleAim"];
            pauseAction = asset["Player/Pause"];
            emoteAction = asset.FindAction("Player/Emote");
        }

        void CreateEventSystem(InputActionAsset asset)
        {
            // Créé inactif : le module ne doit pas s'activer avant d'avoir reçu les actions de CE joueur,
            // sinon il prendrait les actions par défaut (tous les périphériques).
            var go = new GameObject($"EventSystem P{Index + 1}");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            EventSystem = go.AddComponent<MultiplayerEventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.point = InputActionReference.Create(asset["UI/Point"]);
            module.leftClick = InputActionReference.Create(asset["UI/Click"]);
            // Joueur clavier en coop locale : la navigation du module ne réagit pas au clavier,
            // on la remplace par une lecture directe des touches (pas de double déplacement).
            bool keyboardPlayer = false;
            if (input != null)
                foreach (var device in input.devices)
                    keyboardPlayer |= device is Keyboard;
            if (keyboardPlayer) go.AddComponent<KeyboardUINavigator>();
            else
            {
                module.move = InputActionReference.Create(asset["UI/Navigate"]);
                module.submit = InputActionReference.Create(asset["UI/Submit"]);
            }
            module.cancel = InputActionReference.Create(asset["UI/Cancel"]);
            if (input != null) input.uiInputModule = module;
            go.SetActive(true);
        }

        public void Setup(CharacterData character, Vector2 spawnPosition)
        {
            Character = character;
            Stats.Add(character.baseStats);
            Position = spawnPosition;
            transform.position = spawnPosition;
            lastRemotePosition = spawnPosition;
            IsAlive = true;
            Health = MaxHealth;
            if (shadow) shadow.color = new Color(Color.r, Color.g, Color.b, 0.6f);

            weapons.Init(this);
            if (character.startingWeapon != null) weapons.TryAdd(character.startingWeapon);
        }

        void Update()
        {
            if (Character == null) return;
            float dt = Time.deltaTime;

            if (IsRemote)
            {
                UpdateRemote(dt);
                return;
            }

            var gm = GameManager.Instance;
            if (pauseAction.WasPressedThisFrame()) gm.TogglePause(this);
            bool emoting = UpdateEmoteWheel(gm);
            if (gm.IsPaused && !NetMode.IsOnline) return;

            if (toggleAimAction.WasPressedThisFrame()) ManualAim = !ManualAim;

            Vector2 move = Vector2.zero;
            if (IsAlive && gm.PlayersCanMove && !gm.IsPaused)
            {
                // Au clavier, les touches de déplacement choisissent l'emote : le perso s'arrête pendant ce temps
                if (!(emoting && UsesKeyboard)) move = moveAction.ReadValue<Vector2>();
                if (move.sqrMagnitude > 1f) move.Normalize();
                float speed = Character.baseMoveSpeed * Stats.MoveSpeedMultiplier;
                Position = Arena.Instance.Clamp(Position + move * (speed * dt), radius);
                transform.position = Position;

                UpdateAim(move);
                if (NetMode.HasAuthority) Regenerate(dt);
            }

            UpdateVisuals(move, dt);
        }

        /// <summary>Joueur d'un autre PC : sa position arrive par le réseau (NetworkTransform).</summary>
        void UpdateRemote(float dt)
        {
            Position = transform.position;
            if (dt > 0f)
            {
                var velocity = (Position - lastRemotePosition) / dt;
                remoteVelocity = Vector2.Lerp(remoteVelocity, velocity, 1f - Mathf.Exp(-12f * dt));
            }
            lastRemotePosition = Position;

            if (IsAlive && NetMode.HasAuthority) Regenerate(dt);

            var gm = GameManager.Instance;
            if (gm != null && !gm.PlayersCanMove) remoteVelocity = Vector2.zero;
            float speed = Character.baseMoveSpeed * Stats.MoveSpeedMultiplier;
            UpdateVisuals(remoteVelocity.sqrMagnitude > 0.25f ? remoteVelocity / Mathf.Max(0.01f, speed) : Vector2.zero, dt);
        }

        /// <summary>
        /// Roue des emotes : maintenir Emote (F / Y), viser une emote (stick droit, ou touches de déplacement
        /// au clavier), relâcher pour la jouer. Renvoie vrai tant que la roue est ouverte.
        /// </summary>
        bool UpdateEmoteWheel(GameManager gm)
        {
            if (emoteAction == null) return false;
            bool allowed = gm.PlayersCanMove && !gm.IsPaused;
            if (emoteWheel == null || !emoteWheel.IsOpen)
            {
                if (!allowed || !emoteAction.WasPressedThisFrame() || EmoteSet.Count == 0) return false;
                if (emoteWheel == null) emoteWheel = EmoteWheel.Create(transform);
                emoteWheel.Open(Color);
            }
            if (!allowed)
            {
                emoteWheel.Close();
                return false;
            }

            Vector2 direction = aimStickAction.ReadValue<Vector2>();
            if (direction.sqrMagnitude < 0.25f && UsesKeyboard) direction = moveAction.ReadValue<Vector2>();
            emoteWheel.Aim(direction);
            if (emoteAction.IsPressed()) return true;

            int chosen = emoteWheel.Close();
            if (chosen >= 0 && Time.unscaledTime >= nextEmoteTime)
            {
                nextEmoteTime = Time.unscaledTime + EmoteSet.Instance.cooldown;
                ShowEmote(chosen);
                EmotePlayed?.Invoke(chosen);
            }
            return false;
        }

        /// <summary>Affiche une emote au-dessus de ce joueur (aussi appelé par le réseau pour les autres PC).</summary>
        public void ShowEmote(int id)
        {
            if (id < 0 || id >= EmoteSet.Count) return;
            if (emoteWheel == null) emoteWheel = EmoteWheel.Create(transform);
            emoteWheel.Show(id);
            AudioManager.Play(SoundId.UiConfirm, 0.7f, 1.25f);
        }

        void UpdateAim(Vector2 move)
        {
            if (input == null) DetectInputDevice();

            if (UsesKeyboard)
            {
                // Curseur caché et verrouillé (jeu au clavier) : la visée suit la direction de déplacement
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    if (move.sqrMagnitude > 0.09f) AimDirection = move.normalized;
                    return;
                }
                var cam = Camera.main;
                if (cam == null) return;
                Vector2 world = cam.ScreenToWorldPoint(aimPointerAction.ReadValue<Vector2>());
                Vector2 d = world - Position;
                if (d.sqrMagnitude > 0.01f) AimDirection = d.normalized;
            }
            else
            {
                Vector2 stick = aimStickAction.ReadValue<Vector2>();
                if (stick.sqrMagnitude > 0.09f) AimDirection = stick.normalized;
                else if (move.sqrMagnitude > 0.09f) AimDirection = move.normalized;
            }
        }

        /// <summary>En ligne (1 joueur par PC) : la visée suit le dernier périphérique utilisé (souris ou manette).</summary>
        void DetectInputDevice()
        {
            if (aimStickAction.ReadValue<Vector2>().sqrMagnitude > 0.09f) lastInputWasKeyboard = false;
            else if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1f) lastInputWasKeyboard = true;
            else if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.09f) lastInputWasKeyboard = false;
        }

        /// <summary>Visée d'un joueur distant (reçue par le réseau).</summary>
        public void SetRemoteAim(bool manual, Vector2 direction)
        {
            ManualAim = manual;
            if (direction.sqrMagnitude > 0.01f) AimDirection = direction.normalized;
        }

        /// <summary>Vol de vie (hôte / hors ligne) : à chaque coup, une chance de regagner 1 PV, 10 fois par seconde au plus.</summary>
        public void TryLifeSteal()
        {
            if (!IsAlive || Health >= MaxHealth || Time.time < nextLifeSteal) return;
            if (UnityEngine.Random.value >= Stats.LifeSteal01) return;
            nextLifeSteal = Time.time + 0.1f;
            Health = Mathf.Min(MaxHealth, Health + 1f);
        }

        /// <summary>Fin de vague : la récolte (Harvesting) donne des matériaux et de l'XP.</summary>
        public void Harvest()
        {
            int amount = Mathf.RoundToInt(Stats[StatType.Harvesting]);
            if (amount <= 0 || Character == null) return;
            CollectMaterials(amount);
            MarkNetDirty();
            DamagePopup.ShowText(Position + Vector2.up * 1.1f, $"+{amount} HARVEST", new Color(1f, 0.85f, 0.35f), 1.2f);
        }

        void Regenerate(float dt)
        {
            float regen = Stats[StatType.HealthRegen];
            if (regen > 0f) Health = Mathf.Min(MaxHealth, Health + regen / 5f * dt);
        }

        void UpdateVisuals(Vector2 move, float dt)
        {
            if (!IsAlive)
            {
                body.sprite = Character.deadSprite;
                body.enabled = true;
                body.color = Color.white;
                if (IsDowned)
                {
                    // À terre : clignote en rouge, de plus en plus vite quand le temps presse
                    float speed = Mathf.Lerp(18f, 4f, BleedOut01);
                    body.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f), (Mathf.Sin(Time.time * speed) + 1f) / 2f);
                    if (downedIndicator == null) downedIndicator = DownedIndicator.Create(transform);
                    var ws = GameManager.Instance.waveSet;
                    downedIndicator.Show(BleedOut01, Revive01, ws.reviveRadius, Color);
                }
                else if (downedIndicator != null) downedIndicator.Hide();
                return;
            }
            if (downedIndicator != null) downedIndicator.Hide();

            bool moving = move.sqrMagnitude > 0.01f;
            anim.Play(moving ? Character.runFrames : Character.idleFrames, Character.animFps);
            body.sprite = anim.Tick(dt);

            // Écrasement / étirement : petit rebond en marchant, respiration à l'arrêt
            walkTime += dt * (moving ? 14f : 3f);
            float squash = Mathf.Sin(walkTime) * (moving ? 0.07f : 0.025f);
            body.transform.localScale = new Vector3(1f - squash, 1f + squash, 1f);

            // Flash rouge quand on est touché
            // Flash doré au passage de niveau ; pouls rouge quand il reste peu de PV
            hurtFlash = Mathf.Max(0f, hurtFlash - dt);
            levelUpFlash = Mathf.Max(0f, levelUpFlash - dt);
            var juice = JuiceSettings.Settings;
            if (hurtFlash > 0f) body.color = new Color(1f, 0.35f, 0.35f);
            else if (levelUpFlash > 0f) body.color = Mathf.Repeat(levelUpFlash, 0.12f) > 0.06f ? juice.levelUpColor : Color.white;
            else if (IsLowHealth) body.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.5f), (Mathf.Sin(Time.time * 9f) + 1f) / 2f);
            else body.color = Color.white;

            if (ManualAim) body.flipX = AimDirection.x < 0f;
            else if (Mathf.Abs(move.x) > 0.05f) body.flipX = move.x < 0f;

            // Clignotement pendant l'invulnérabilité
            if (invulnerableTimer > 0f)
            {
                invulnerableTimer -= dt;
                body.enabled = Mathf.Repeat(invulnerableTimer, 0.1f) > 0.05f;
            }
            else body.enabled = true;

            body.sortingOrder = SortingUtil.OrderFromY(Position.y);
        }

        public void TakeDamage(float amount)
        {
            // En ligne, seuls les dégâts calculés par l'hôte comptent
            if (NetMode.IsClientOnly) return;
            if (!IsAlive || invulnerableTimer > 0f || DebugInvincible || GameManager.Instance.State != GameState.Wave) return;

            invulnerableTimer = InvulnerabilityDuration;
            if (UnityEngine.Random.value < Stats.Dodge01)
            {
                DamagePopup.ShowText(Position + Vector2.up * 0.6f, "DODGE", Color.cyan);
                return;
            }

            float dmg = Mathf.Max(1f, Mathf.Round(amount * Stats.ArmorMultiplier));
            Health -= dmg;
            RunStats.damageTaken += dmg;
            DamagePopup.ShowText(Position + Vector2.up * 0.6f, "-" + dmg, new Color(1f, 0.3f, 0.3f));
            PlayHurtFeedback();
            GameManager.Instance.HitStop(0.06f);

            // Coop : on tombe à terre tant qu'un coéquipier est debout pour nous relever
            if (Health <= 0f) Die(downed: AnyOtherPlayerAlive());
        }

        bool AnyOtherPlayerAlive()
        {
            foreach (var p in All)
                if (p != this && p.IsAlive) return true;
            return false;
        }

        void PlayHurtFeedback()
        {
            hurtFlash = 0.15f;
            invulnerableTimer = InvulnerabilityDuration;
            AudioManager.Play(SoundId.PlayerHurt);
            // En ligne, seul celui qui joue ce personnage voit l'écran trembler
            if (IsLocallyControlled && CameraController.Instance) CameraController.Instance.Shake(0.25f);
            AnyDamaged?.Invoke(this);
        }

        /// <param name="downed">Vrai : à terre, peut encore être relevé ; faux : mort jusqu'à la vague suivante.</param>
        void Die(bool downed)
        {
            Health = 0f;
            IsAlive = false;
            IsDowned = downed;
            BleedOut01 = 1f;
            Revive01 = 0f;
            helpPopupTimer = 0f;
            AudioManager.Play(SoundId.PlayerDeath);
            Debris.Burst(Position, Color, 20, 6f, 1.4f);
            weapons.gameObject.SetActive(false);
            body.flipX = false;
            Died?.Invoke(this);
        }

        /// <summary>
        /// Hôte / hors ligne, pendant la vague : un coéquipier debout dans le cercle fait monter la réanimation
        /// (plus vite à plusieurs) et met le compte à rebours en pause. Sinon le temps file.
        /// </summary>
        public void TickDowned(float dt, WaveSet ws)
        {
            if (!IsDowned) return;

            int helpers = 0;
            float radiusSqr = ws.reviveRadius * ws.reviveRadius;
            foreach (var p in All)
                if (p != this && p.IsAlive && (p.Position - Position).sqrMagnitude <= radiusSqr) helpers++;

            if (helpers > 0)
            {
                Revive01 += dt * helpers / Mathf.Max(0.1f, ws.reviveTime);
                if (Revive01 >= 1f) Revive(ws);
                return;
            }

            // Personne : la réanimation redescend doucement et le compte à rebours continue
            Revive01 = Mathf.Max(0f, Revive01 - dt / Mathf.Max(0.1f, ws.reviveTime) * 0.5f);
            BleedOut01 -= dt / Mathf.Max(0.1f, ws.bleedOutTime);
            if (BleedOut01 <= 0f)
            {
                BleedOut();
                return;
            }

            helpPopupTimer -= dt;
            if (helpPopupTimer <= 0f)
            {
                helpPopupTimer = 2.5f;
                DamagePopup.ShowText(Position + Vector2.up * 1.1f, "HELP!", Color, 1.1f);
            }
        }

        void Revive(WaveSet ws)
        {
            IsDowned = false;
            IsAlive = true;
            Revive01 = 0f;
            Health = Mathf.Max(1f, Mathf.Round(MaxHealth * ws.reviveHealth));
            invulnerableTimer = ws.reviveInvulnerability;
            weapons.gameObject.SetActive(true);
            DamagePopup.ShowText(Position + Vector2.up * 0.9f, "REVIVED!", ReviveColor, 1.4f);
            PlayReviveFeedback();
            MarkNetDirty();
        }

        void PlayReviveFeedback()
        {
            Debris.Burst(Position, ReviveColor, 18, 5f, 1.2f);
            AudioManager.Play(SoundId.LevelUp);
        }

        /// <summary>Personne n'est venu à temps : mort jusqu'à la vague suivante.</summary>
        void BleedOut()
        {
            IsDowned = false;
            BleedOut01 = 0f;
            Revive01 = 0f;
            Debris.Burst(Position, new Color(0.4f, 0.4f, 0.4f), 14, 3f, 1f);
        }

        /// <summary>Fin de vague : un joueur encore à terre reste mort jusqu'à la vague suivante.</summary>
        public void ClearDowned()
        {
            IsDowned = false;
            Revive01 = 0f;
        }

        /// <summary>Début de vague : résurrection et PV au max (comme dans Brotato).</summary>
        public void ResetForWave()
        {
            IsAlive = true;
            IsDowned = false;
            Revive01 = 0f;
            IsReady = false;
            Health = MaxHealth;
            invulnerableTimer = 0f;
            weapons.gameObject.SetActive(true);
        }

        public void CollectMaterials(int amount, bool giveXp = true)
        {
            Materials += amount;
            if (!giveXp) return;
            RunStats.materialsCollected += amount;
            AddXp(amount);
        }

        public int ItemCount(ItemData item)
        {
            int count = 0;
            foreach (var i in items)
                if (i == item) count++;
            return count;
        }

        /// <summary>Faux si le joueur a déjà cet objet au maximum (objets uniques).</summary>
        public bool CanOwnMore(ItemData item) => item.maxPerPlayer <= 0 || ItemCount(item) < item.maxPerPlayer;

        public void AddItem(ItemData item)
        {
            items.Add(item);
            ApplyStats(item.modifiers);
        }

        /// <summary>Retire un exemplaire de l'objet et ses bonus (don à un coéquipier).</summary>
        public bool RemoveItem(ItemData item)
        {
            if (!items.Remove(item)) return false;
            if (item.modifiers != null && item.modifiers.Length > 0)
            {
                var mods = new StatModifier[item.modifiers.Length];
                for (int i = 0; i < mods.Length; i++) mods[i] = new StatModifier(item.modifiers[i].stat, -item.modifiers[i].value);
                ApplyStats(mods);
            }
            MarkNetDirty();
            return true;
        }

        public void ApplyUpgrade(UpgradeTable.Choice choice) =>
            ApplyStats(new[] { new StatModifier(choice.stat, choice.value) });

        /// <summary>Ajoute des modificateurs de stats (valeurs négatives pour les retirer).</summary>
        public void ApplyStats(StatModifier[] modifiers)
        {
            float oldMax = MaxHealth;
            Stats.Add(modifiers);
            // Un bonus de PV max soigne d'autant ; un malus ne tue pas
            Health = Mathf.Clamp(Health + Mathf.Max(0f, MaxHealth - oldMax), 1f, MaxHealth);
            MarkNetDirty();
        }

        public void CollectCrate()
        {
            PendingCrates++;
            DamagePopup.ShowText(Position + Vector2.up * 0.9f, "CRATE!", new Color(0.55f, 0.8f, 1f), 1.2f);
        }

        public bool SpendMaterials(int amount)
        {
            if (Materials < amount) return false;
            Materials -= amount;
            MarkNetDirty();
            return true;
        }

        void AddXp(float amount)
        {
            Xp += amount;
            while (Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                Level++;
                PendingLevelUps++;
                Stats.Add(StatType.MaxHealth, 1);
                Health += 1;
                var juice = JuiceSettings.Settings;
                DamagePopup.ShowText(Position + Vector2.up * 0.9f, "LEVEL UP!", juice.levelUpColor, 1.5f);
                Debris.Burst(Position, juice.levelUpColor, juice.levelUpDebris, 6f, 1.2f);
                levelUpFlash = 0.6f;
                if (IsLocallyControlled && CameraController.Instance) CameraController.Instance.Shake(juice.levelUpShake);
                AudioManager.Play(SoundId.LevelUp);
                LevelledUp?.Invoke(this);
            }
        }

        // ------------------------------------------------------------ Réseau

        public void MarkNetDirty()
        {
            if (NetMode.IsOnlineHost) NetDirty = true;
        }

        public void ClearNetDirty() => NetDirty = false;

        /// <summary>Client en ligne : PV / vie / matériaux / XP reçus de l'hôte (envoyés souvent).</summary>
        public void ApplyNetVitals(float health, bool alive, bool downed, float bleedOut01, float revive01,
            int materials, float xp, int level, bool ready)
        {
            bool wasAlive = IsAlive;
            bool wasDowned = IsDowned;
            if (alive && wasAlive && health < Health - 0.5f) PlayHurtFeedback();
            Health = health;
            Materials = materials;
            Xp = xp;
            Level = level;
            IsReady = ready;

            if (wasAlive && !alive) Die(downed);
            else if (!wasAlive && alive)
            {
                IsAlive = true;
                IsDowned = false;
                weapons.gameObject.SetActive(true);
                if (wasDowned) PlayReviveFeedback();
            }
            else if (!alive) IsDowned = downed;

            if (!alive)
            {
                BleedOut01 = bleedOut01;
                Revive01 = revive01;
            }
        }

        /// <summary>Client en ligne : stats, objets, armes et boutique reçus de l'hôte.</summary>
        public void ApplyNetState(PlayerSave state, Func<string, ShopItemData> resolve)
        {
            pendingLevelUps = state.pendingLevelUps;
            pendingCrates = state.pendingCrates;
            RunStats = state.runStats ?? RunStats;
            Stats.SetAll(state.stats);
            Materials = state.materials;

            items.Clear();
            foreach (var name in state.items)
                if (resolve(name) is ItemData item) items.Add(item);

            // On ne recrée les armes que si la liste a changé
            bool sameWeapons = weapons.Weapons.Count == state.weapons.Count;
            for (int i = 0; sameWeapons && i < state.weapons.Count; i++)
                sameWeapons = weapons.Weapons[i].Data.name == state.weapons[i];
            if (!sameWeapons)
            {
                var list = new List<WeaponData>();
                foreach (var name in state.weapons)
                    if (resolve(name) is WeaponData w) list.Add(w);
                weapons.Restore(list);
            }

            Shop?.Restore(state, resolve);
            NetStateApplied?.Invoke(this);
        }

        // ------------------------------------------------------------ Sauvegarde

        public PlayerSave ToSave()
        {
            var save = new PlayerSave
            {
                character = Character.name,
                materials = Materials,
                level = Level,
                xp = Xp,
                pendingLevelUps = PendingLevelUps,
                pendingCrates = PendingCrates,
                stats = Stats.GetAll(),
                runStats = RunStats,
            };
            foreach (var w in weapons.Weapons) save.weapons.Add(w.Data.name);
            foreach (var i in items) save.items.Add(i.name);
            Shop?.Export(save);
            return save;
        }

        public void RestoreFrom(PlayerSave save, Func<string, ShopItemData> resolve)
        {
            Materials = save.materials;
            Level = save.level;
            Xp = save.xp;
            PendingLevelUps = save.pendingLevelUps;
            PendingCrates = save.pendingCrates;
            RunStats = save.runStats ?? new RunStats();

            items.Clear();
            foreach (var name in save.items)
                if (resolve(name) is ItemData item) items.Add(item);

            var savedWeapons = new List<WeaponData>();
            foreach (var name in save.weapons)
                if (resolve(name) is WeaponData w) savedWeapons.Add(w);
            weapons.Restore(savedWeapons);

            // Les stats sauvegardées incluent déjà objets, bonus d'armes et améliorations
            Stats.SetAll(save.stats);
            Health = MaxHealth;
            Shop?.Restore(save, resolve);
        }

        public static void ClearStaticEvents()
        {
            AnyDamaged = null;
            DebugInvincible = false;
        }

        public static Player FindNearestAlive(Vector2 position)
        {
            Player best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in All)
            {
                if (!p.IsAlive) continue;
                float d = (p.Position - position).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Le joueur contrôlé sur ce PC en ligne (null hors ligne).</summary>
        public static Player LocalOnline
        {
            get
            {
                foreach (var p in All)
                    if (p.Mode == ControlMode.OnlineOwner) return p;
                return null;
            }
        }
    }
}
