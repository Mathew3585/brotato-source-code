using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// HUD de la partie : vague + timer, barre du boss, un panneau par joueur, messages au centre, menu pause.
    /// Toute l'UI est dans la scène (modifiable dans l'éditeur) ; ce script ne fait que la mettre à jour.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("HUD (masqué pendant la boutique)")]
        [SerializeField] GameObject hudRoot;
        [SerializeField] TextMeshProUGUI waveText;
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] TextMeshProUGUI centerText;

        [Tooltip("Image plein écran rouge qui clignote quand un joueur est touché")]
        [SerializeField] Image damageFlash;

        [Header("Boss")]
        [SerializeField] RectTransform bossBar;
        [SerializeField] RectTransform bossFill;
        [SerializeField] TextMeshProUGUI bossName;

        [Header("Joueurs (J1 à J4)")]
        [SerializeField] PlayerPanelView[] playerPanels;

        [Header("Fin de partie")]
        [SerializeField] EndScreenView endScreen;

        [Header("Pause")]
        [SerializeField] GameObject pauseRoot;
        [SerializeField] RectTransform pausePanel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button optionsButton;
        [SerializeField] Button menuButton;
        [SerializeField] OptionsMenu options;

        readonly Dictionary<PlayerPanelView, Player> panelOwners = new();
        // Dernières valeurs affichées (pour faire rebondir ce qui augmente)
        readonly Dictionary<PlayerPanelView, PanelState> panelStates = new();
        float damageFlashAlpha;
        int lastTimerSecond = -1;

        readonly List<(Image image, Sprite[] frames, float fps)> endPortraits = new();

        class PanelState
        {
            public int materials, level;
            /// <summary>Caisses ramassées pendant la vague (icône à côté du panneau), -1 = pas encore affiché.</summary>
            public int crates = -1;
            public float health;
            public float lastPunch, lastShake;
        }
        GameManager gm;
        Player pauseOwner;

        void Start()
        {
            gm = GameManager.Instance;

            foreach (var panel in playerPanels) panel.gameObject.SetActive(false);
            bossBar.gameObject.SetActive(false);
            pauseRoot.SetActive(false);
            if (endScreen != null) endScreen.gameObject.SetActive(false);
            resumeButton.onClick.AddListener(() => gm.TogglePause(gm.PausedBy));
            menuButton.onClick.AddListener(() =>
            {
                gm.SaveRun();
                GameManager.GoToMainMenu();
            });
            var rows = new List<List<Selectable>> { new() { resumeButton } };
            if (optionsButton != null)
            {
                optionsButton.onClick.AddListener(OpenOptions);
                rows.Add(new List<Selectable> { optionsButton });
            }
            rows.Add(new List<Selectable> { menuButton });
            UIFactory.LinkNavigation(rows);
            if (options != null) options.gameObject.SetActive(false);
            if (NetMode.IsOnline)
            {
                // En ligne, "Menu principal" quitte la room
                var label = menuButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = NetMode.IsServer ? "Close the room" : "Leave the game";
            }

            gm.PlayerJoined += AddPanel;
            gm.StateChanged += OnStateChanged;
            gm.PauseChanged += OnPauseChanged;
            Player.AnyDamaged += OnPlayerDamaged;
            if (damageFlash != null) damageFlash.color = new Color(1f, 0f, 0f, 0f);
            foreach (var p in Player.All)
                if (p.Character != null) AddPanel(p);
        }

        void OnDestroy()
        {
            if (gm == null) return;
            gm.PlayerJoined -= AddPanel;
            gm.StateChanged -= OnStateChanged;
            gm.PauseChanged -= OnPauseChanged;
            Player.AnyDamaged -= OnPlayerDamaged;
        }

        void OnPlayerDamaged(Player player)
        {
            damageFlashAlpha = 0.3f;
        }

        /// <summary>Voile rouge : flash quand on est touché + pouls léger quand un joueur de ce PC a peu de PV.</summary>
        void UpdateDamageFlash()
        {
            if (damageFlash == null) return;
            damageFlashAlpha = Mathf.Max(0f, damageFlashAlpha - Time.unscaledDeltaTime * 0.85f);
            float alpha = damageFlashAlpha;

            float vignette = JuiceSettings.Settings.lowHealthVignette;
            if (vignette > 0f && gm.State == GameState.Wave && !gm.IsPaused)
            {
                foreach (var p in Player.All)
                {
                    if (!p.IsLocallyControlled || !p.IsLowHealth) continue;
                    alpha = Mathf.Max(alpha, vignette * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f)));
                    break;
                }
            }
            if (!Mathf.Approximately(damageFlash.color.a, alpha)) damageFlash.color = new Color(1f, 0f, 0f, alpha);
        }

        void OnStateChanged(GameState state)
        {
            // Le HUD disparaît pendant la boutique (elle a son propre écran)
            hudRoot.SetActive(state != GameState.Shop);
            if (state is GameState.GameOver or GameState.Victory) ShowEndScreen(state == GameState.Victory);
            if (state == GameState.Intermission)
            {
                // « Wave cleared! » arrive avec un rebond
                Tween.StopAll(centerText.transform);
                Tween.Scale(centerText.transform, 0f, 1f, 0.45f, Ease.OutBack);
            }
            if (state != GameState.Wave) return;

            // Annonce de la vague
            UpdateTop();
            Tween.CompleteAll(waveText.transform);
            Tween.Scale(waveText.transform, 2.2f, 1f, 0.5f, Ease.OutBack);
            Tween.PunchScale(timerText.transform, new Vector3(0.3f, 0.3f, 0f), 0.4f);
        }

        void AddPanel(Player player)
        {
            if (player.Index >= playerPanels.Length) return;
            var panel = playerPanels[player.Index];
            panelOwners[panel] = player;
            panel.gameObject.SetActive(true);
            panel.stripe.color = player.Color;
            panel.title.text = $"P{player.Index + 1} - {player.Character.displayName}";
            panel.title.color = player.Color;
            PlaceCrate(panel);
        }

        /// <summary>L'icône des caisses se place du côté intérieur de l'écran (à droite des panneaux de gauche, et inversement).</summary>
        static void PlaceCrate(PlayerPanelView panel)
        {
            var crate = panel.crateRoot;
            if (crate == null) return;
            bool panelOnRight = ((RectTransform)panel.transform).pivot.x > 0.5f;
            float x = Mathf.Abs(crate.anchoredPosition.x);
            crate.anchorMin = crate.anchorMax = new Vector2(panelOnRight ? 0f : 1f, 1f);
            crate.pivot = new Vector2(panelOnRight ? 1f : 0f, 1f);
            crate.anchoredPosition = new Vector2(panelOnRight ? -x : x, crate.anchoredPosition.y);
            crate.gameObject.SetActive(false);
        }

        // ---------- Fin de partie ----------

        void ShowEndScreen(bool victory)
        {
            if (endScreen == null) return;
            endScreen.gameObject.SetActive(true);
            endScreen.title.text = victory ? "VICTORY!" : "GAME OVER";
            endScreen.title.color = victory ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.4f, 0.4f);
            endScreen.subtitle.text = victory
                ? $"All {gm.WaveCount} waves survived!"
                : $"Reached wave {gm.WaveIndex + 1} / {gm.WaveCount}";
            endScreen.hint.text = !NetMode.IsOnline ? "Enter / A: restart   •   Esc / B: main menu"
                : NetMode.IsServer ? "Enter / A: back to the lobby   •   Esc / B: close the room"
                : "Waiting for the host...   •   Esc / B: leave";

            // Joueur qui a fait le plus de dégâts : cadre doré
            Player best = null;
            int shown = 0;
            foreach (var p in Player.All)
            {
                if (p.Character == null) continue;
                shown++;
                if (best == null || p.RunStats.damageDealt > best.RunStats.damageDealt) best = p;
            }

            endPortraits.Clear();
            for (int i = 0; i < endScreen.players.Length; i++)
            {
                var col = endScreen.players[i];
                var player = i < Player.All.Count ? Player.All[i] : null;
                col.root.SetActive(player != null && player.Character != null);
                if (!col.root.activeSelf) continue;

                col.colorBar.color = player.Color;
                col.title.color = player.Color;
                col.title.text = $"P{player.Index + 1} - {player.Character.displayName}";
                FillEndCard(col, player, player == best, shown > 1);

                var s = player.RunStats;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Level: <color=#7CFF7C>{player.Level}</color>");
                sb.AppendLine($"Enemies killed: <color=#FFD966>{s.kills}</color>");
                sb.AppendLine($"Damage dealt: <color=#FFD966>{Mathf.RoundToInt(s.damageDealt)}</color>");
                sb.AppendLine($"Damage taken: <color=#FF6B6B>{Mathf.RoundToInt(s.damageTaken)}</color>");
                sb.AppendLine($"Materials collected: <color=#FFD966>{s.materialsCollected}</color>");
                sb.AppendLine($"Items: {player.Items.Count}");
                sb.AppendLine();
                sb.AppendLine("<color=#AAAAAA>Best weapons</color>");
                foreach (var (name, damage) in s.TopWeapons(3))
                    sb.AppendLine($"{name}: {Mathf.RoundToInt(damage)}");
                if (s.structureDamage > 0f)
                {
                    sb.AppendLine();
                    sb.AppendLine($"<color=#AAAAAA>Structures</color>: {Mathf.RoundToInt(s.structureDamage)}");
                }
                col.stats.text = sb.ToString();
            }

            endScreen.panel.localScale = Vector3.one * 0.85f;
            Tween.Scale(endScreen.panel, Vector3.one, 0.4f, Ease.OutBack, useUnscaledTime: true);
        }

        /// <summary>Carte en haut de la colonne : personnage animé, meilleure arme, cadre doré pour le meilleur joueur.</summary>
        void FillEndCard(EndScreenView.PlayerColumn col, Player player, bool isBest, bool severalPlayers)
        {
            if (col.goldFrame != null) col.goldFrame.SetActive(isBest);
            if (col.badge != null) col.badge.SetActive(isBest && severalPlayers);

            var character = player.Character;
            if (col.portrait != null)
            {
                var frames = character.idleFrames != null && character.idleFrames.Length > 0 ? character.idleFrames : null;
                col.portrait.sprite = frames != null ? frames[0] : character.deadSprite;
                col.portrait.preserveAspect = true;
                if (frames != null && frames.Length > 1) endPortraits.Add((col.portrait, frames, character.animFps));
            }

            string weaponName = null;
            foreach (var (name, _) in player.RunStats.TopWeapons(1)) weaponName = name;
            if (col.weaponName != null)
                col.weaponName.text = weaponName != null ? $"<color=#AAAAAA>Best weapon</color>\n{weaponName}" : "<color=#AAAAAA>No weapon</color>";
            if (col.weaponIcon != null)
            {
                var icon = FindWeaponIcon(player, weaponName);
                col.weaponIcon.sprite = icon;
                col.weaponIcon.enabled = icon != null;
            }

            if (isBest && col.goldFrame != null)
            {
                var t = col.goldFrame.transform;
                Tween.PunchScale(t, new Vector3(0.04f, 0.04f, 0f), 0.6f, startDelay: 0.4f, useUnscaledTime: true);
            }
        }

        /// <summary>Icône de l'arme (le nom est celui sans le tier) : arme possédée de plus haut tier, sinon catalogue.</summary>
        Sprite FindWeaponIcon(Player player, string rootName)
        {
            if (rootName == null) return null;
            WeaponData found = null;
            foreach (var w in player.Weapons.Weapons)
                if (RunStats.RootName(w.Data) == rootName && (found == null || w.Data.tier > found.tier)) found = w.Data;
            if (found == null && gm.shopCatalog != null)
                foreach (var w in gm.shopCatalog.weapons)
                    if (w != null && RunStats.RootName(w) == rootName && (found == null || w.tier > found.tier)) found = w;
            return found != null ? found.Icon : null;
        }

        /// <summary>Personnages de l'écran de fin : animation d'attente (temps réel, le jeu peut être figé).</summary>
        void TickEndPortraits()
        {
            if (endPortraits.Count == 0 || endScreen == null || !endScreen.gameObject.activeInHierarchy) return;
            foreach (var (image, frames, fps) in endPortraits)
                image.sprite = frames[(int)(Time.unscaledTime * Mathf.Max(1f, fps)) % frames.Length];
        }

        // ---------- Pause ----------

        void OpenOptions()
        {
            if (options == null || pauseOwner == null) return;
            AudioManager.Play(SoundId.UiConfirm);
            pausePanel.gameObject.SetActive(false);
            options.Open(pauseOwner.EventSystem, () =>
            {
                pausePanel.gameObject.SetActive(true);
                if (pauseOwner == null || pauseOwner.EventSystem == null) return;
                pauseOwner.EventSystem.playerRoot = pausePanel.gameObject;
                pauseOwner.EventSystem.SetSelectedGameObject(optionsButton.gameObject);
            });
        }

        void OnPauseChanged()
        {
            pauseRoot.SetActive(gm.IsPaused);
            pausePanel.gameObject.SetActive(true);
            if (!gm.IsPaused && options != null && options.gameObject.activeSelf) options.gameObject.SetActive(false);

            // Rend la main au joueur précédent
            if (pauseOwner != null && pauseOwner.EventSystem != null)
            {
                pauseOwner.EventSystem.SetSelectedGameObject(null);
                pauseOwner.EventSystem.playerRoot = null;
            }
            pauseOwner = gm.IsPaused ? gm.PausedBy : null;
            if (pauseOwner == null || pauseOwner.EventSystem == null) return;

            // Seul le joueur qui a mis la pause navigue dans ce menu
            pauseOwner.EventSystem.playerRoot = pausePanel.gameObject;
            pauseOwner.EventSystem.SetSelectedGameObject(resumeButton.gameObject);
            pausePanel.localScale = Vector3.one * 0.9f;
            Tween.Scale(pausePanel, Vector3.one, 0.25f, Ease.OutBack, useUnscaledTime: true);
        }

        // ---------- Mise à jour ----------

        void Update()
        {
            UpdateTop();
            UpdateCenter();
            UpdateDamageFlash();
            TickEndPortraits();
            foreach (var (panel, player) in panelOwners)
                if (player != null) UpdatePanel(panel, player);
        }

        void UpdateTop()
        {
            bool inRun = gm.State is GameState.Wave or GameState.Intermission;
            waveText.text = inRun ? $"WAVE {gm.WaveIndex + 1} / {gm.WaveCount}" : "";
            timerText.text = gm.State != GameState.Wave ? ""
                : gm.IsBossWave && gm.StateTimer <= 0f ? "BOSS"
                : Mathf.CeilToInt(gm.StateTimer).ToString();
            int warning = JuiceSettings.Settings.timerWarningSeconds;
            timerText.color = gm.State == GameState.Wave && gm.StateTimer <= warning ? new Color(1f, 0.4f, 0.4f) : Color.white;

            // Dernières secondes : le timer pulse avec un tic à chaque seconde
            int second = gm.State == GameState.Wave ? Mathf.CeilToInt(gm.StateTimer) : -1;
            if (second != lastTimerSecond && second > 0 && second <= warning && !gm.IsPaused)
            {
                Tween.CompleteAll(timerText.transform);
                Tween.PunchScale(timerText.transform, new Vector3(0.4f, 0.4f, 0f), 0.3f);
                AudioManager.Play(SoundId.CountdownTick);
            }
            lastTimerSecond = second;
            UpdateBossBar();
        }

        void UpdateBossBar()
        {
            var boss = gm.CurrentBoss;
            bool show = boss != null && boss.IsAlive && gm.State == GameState.Wave;
            if (bossBar.gameObject.activeSelf != show)
            {
                bossBar.gameObject.SetActive(show);
                if (show)
                {
                    bossBar.localScale = new Vector3(0f, 1f, 1f);
                    Tween.Scale(bossBar, Vector3.one, 0.5f, Ease.OutBack);
                }
            }
            if (!show) return;
            bossFill.anchorMax = new Vector2(Mathf.Clamp01(boss.Health / boss.MaxHealth), 1f);
            bossName.text = $"{boss.Data.displayName.ToUpper()}   {Mathf.CeilToInt(boss.Health)} / {Mathf.CeilToInt(boss.MaxHealth)}";
        }

        void UpdateCenter()
        {
            if (gm.IsPaused)
            {
                centerText.text = "";
                return;
            }

            centerText.text = gm.State switch
            {
                GameState.WaitingForPlayers => NetMode.IsOnline
                    ? "Waiting for the other players..."
                    : "Press a key on the keyboard\nor a button on a gamepad\nto join",
                GameState.Countdown =>
                    $"Starting in {Mathf.CeilToInt(gm.StateTimer)}",
                GameState.Intermission =>
                    $"Wave {gm.WaveIndex + 1} cleared!",
                GameState.GameOver when endScreen == null =>
                    $"GAME OVER\n<size=50%>Reached wave {gm.WaveIndex + 1}\nEnter / A: restart   •   Esc / B: main menu</size>",
                GameState.Victory when endScreen == null =>
                    "VICTORY!\n<size=50%>Enter / A: restart   •   Esc / B: main menu</size>",
                _ => "",
            };
        }

        void UpdatePanel(PlayerPanelView p, Player pl)
        {
            PunchChanges(p, pl);
            UpdateCrates(p, pl);
            float hp01 = pl.MaxHealth > 0 ? Mathf.Clamp01(pl.Health / pl.MaxHealth) : 0f;
            p.hpFill.anchorMax = new Vector2(hp01, 1f);
            p.hpText.text = pl.IsAlive ? $"{Mathf.CeilToInt(pl.Health)} / {Mathf.CeilToInt(pl.MaxHealth)}"
                : pl.IsDowned ? "DOWN - HELP!" : "DEAD";

            p.xpFill.anchorMax = new Vector2(Mathf.Clamp01(pl.Xp / pl.XpToNextLevel), 1f);
            p.level.text = "Lv. " + pl.Level;
            p.materials.text = "Materials: " + pl.Materials;
            p.aim.text = pl.ManualAim ? "Aim: manual" : "Aim: auto";
        }

        /// <summary>Icône de caisse à côté du panneau, avec « x2 » : mise à jour seulement quand le nombre change, rebond à chaque caisse.</summary>
        void UpdateCrates(PlayerPanelView p, Player pl)
        {
            if (p.crateRoot == null || !panelStates.TryGetValue(p, out var st) || st.crates == pl.PendingCrates) return;
            bool gained = st.crates >= 0 && pl.PendingCrates > st.crates;
            st.crates = pl.PendingCrates;
            p.crateRoot.gameObject.SetActive(pl.PendingCrates > 0);
            if (p.crateCount != null) p.crateCount.text = pl.PendingCrates > 1 ? "x" + pl.PendingCrates : "";
            float punch = JuiceSettings.Settings.hudPunch;
            if (gained && punch > 0f)
            {
                Tween.CompleteAll(p.crateRoot);
                p.crateRoot.localScale = Vector3.one;
                Tween.PunchScale(p.crateRoot, new Vector3(punch * 2f, punch * 2f, 0f), 0.25f, useUnscaledTime: true);
            }
        }

        /// <summary>Le compteur de matériaux et le niveau rebondissent quand ils montent ; le panneau tremble quand on perd des PV.</summary>
        void PunchChanges(PlayerPanelView p, Player pl)
        {
            if (!panelStates.TryGetValue(p, out var st))
            {
                panelStates[p] = new PanelState { materials = pl.Materials, level = pl.Level, health = pl.Health };
                return;
            }
            float punch = JuiceSettings.Settings.hudPunch;
            float now = Time.unscaledTime;

            if (punch > 0f && (pl.Materials > st.materials || pl.Level > st.level) && now - st.lastPunch > 0.2f)
            {
                st.lastPunch = now;
                var target = pl.Level > st.level ? p.level.transform : p.materials.transform;
                Tween.PunchScale(target, new Vector3(punch, punch, 0f), 0.18f, useUnscaledTime: true);
            }
            if (pl.Health < st.health - 0.01f && now - st.lastShake > 0.3f)
            {
                st.lastShake = now;
                Tween.ShakeLocalPosition(p.transform, new Vector3(10f, 6f, 0f), 0.25f, useUnscaledTime: true);
            }
            st.materials = pl.Materials;
            st.level = pl.Level;
            st.health = pl.Health;
        }
    }
}
