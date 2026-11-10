using System;
using System.Collections.Generic;
using System.Text;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Menu principal + écran de sélection des personnages (façon Brotato) :
    /// chaque périphérique (clavier, chaque manette) qui appuie sur un bouton ajoute un joueur,
    /// chacun choisit son personnage et se met prêt ; la partie démarre quand tout le monde est prêt.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        const int MaxPlayers = 4;
        const float StartDelay = 1.5f;
        const float StickThreshold = 0.6f;

        public CharacterData[] characters;

        [Header("Menu principal")]
        [SerializeField] GameObject menuPanel;
        [SerializeField] Transform title;
        [SerializeField] Button continueButton;
        [SerializeField] Button newGameButton;
        [Tooltip("Bouton Play online (géré par OnlineMenu)")]
        [SerializeField] Button onlineButton;
        [SerializeField] Button optionsButton;
        [SerializeField] Button quitButton;
        [SerializeField] OptionsMenu options;
        [SerializeField] Button challengesButton;
        [SerializeField] ChallengesMenu challenges;
        [Tooltip("Images animées avec les personnages (dans l'ordre de la liste characters)")]
        [SerializeField] Image[] menuFarmers;

        [Header("Sélection des personnages")]
        [SerializeField] GameObject lobbyPanel;
        [SerializeField] LobbySlotView[] slots;
        [SerializeField] TextMeshProUGUI footer;
        [Tooltip("Choix de la difficulté, ouvert quand tout le monde est prêt (partagé avec le lobby en ligne)")]
        [SerializeField] DangerMenu dangerMenu;

        /// <summary>Frame où le choix de difficulté s'est fermé (la touche Retour ne doit pas aussi agir sur le lobby).</summary>
        int dangerClosedFrame = -1;

        class Joined
        {
            public InputDevice[] devices;
            public string scheme;
            public Gamepad pad; // null = clavier/souris
            public int characterIndex;
            public bool ready;
            public int joinFrame;
            public float previousStickX;
        }

        readonly List<Joined> joined = new();
        RunSave resumeSave; // non null = reprise d'une partie sauvegardée
        int RequiredPlayers => resumeSave != null ? resumeSave.players.Count : MaxPlayers;
        Vector2[] slotPositions;
        InputDevice lastDevice;
        IDisposable anyButtonListener;
        bool inLobby;
        float startTimer = -1f;

        void Start()
        {
            Time.timeScale = 1f;
            Session.Players.Clear();
            anyButtonListener = InputSystem.onAnyButtonPress.Call(control => lastDevice = control.device);

            Tween.Scale(title, 1.04f, 1.2f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
            AddMissingMenuFarmers();
            newGameButton.onClick.AddListener(() =>
            {
                resumeSave = null;
                OpenLobby();
            });
            if (continueButton != null) continueButton.onClick.AddListener(ContinueRun);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (challengesButton != null) challengesButton.onClick.AddListener(OpenChallenges);
            if (challenges != null) challenges.gameObject.SetActive(false);
            quitButton.onClick.AddListener(Quit);
            if (options != null) options.gameObject.SetActive(false);
            RefreshMenuButtons();

            slotPositions = new Vector2[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                var slot = slots[i];
                slotPositions[i] = slot.Rect.anchoredPosition;
                var color = Session.ColorOf(i);
                slot.colorBar.color = color;
                slot.header.color = color;
                UIFactory.SetAccent(slot.leftArrow, color);
                UIFactory.SetAccent(slot.rightArrow, color);
                slot.leftArrow.navigation = slot.rightArrow.navigation = new Navigation { mode = Navigation.Mode.None };
                slot.leftArrow.onClick.AddListener(() => OnArrowClicked(index, -1));
                slot.rightArrow.onClick.AddListener(() => OnArrowClicked(index, +1));
            }

            ShowMenu();
        }

        void OnDestroy() => anyButtonListener?.Dispose();

        void OnArrowClicked(int slotIndex, int delta)
        {
            // Clic souris : uniquement pour le joueur clavier de cet emplacement
            if (slotIndex < joined.Count && joined[slotIndex].pad == null && !joined[slotIndex].ready)
                ChangeCharacter(slotIndex, delta);
            // Évite qu'une manette "clique" ensuite ce bouton resté sélectionné
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // ================================================================ Menu

        public void ShowMenu()
        {
            inLobby = false;
            joined.Clear();
            startTimer = -1f;
            menuPanel.SetActive(true);
            lobbyPanel.SetActive(false);
            RefreshMenuButtons();
        }

        /// <summary>"Continuer" n'apparaît que s'il y a une partie sauvegardée.</summary>
        void RefreshMenuButtons()
        {
            bool canContinue = continueButton != null && SaveSystem.HasSave;
            if (continueButton != null) continueButton.gameObject.SetActive(canContinue);

            var rows = new List<List<Selectable>>();
            if (canContinue) rows.Add(new List<Selectable> { continueButton });
            rows.Add(new List<Selectable> { newGameButton });
            if (onlineButton != null) rows.Add(new List<Selectable> { onlineButton });
            if (challengesButton != null) rows.Add(new List<Selectable> { challengesButton });
            if (optionsButton != null) rows.Add(new List<Selectable> { optionsButton });
            rows.Add(new List<Selectable> { quitButton });
            UIFactory.LinkNavigation(rows);

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(rows[0][0].gameObject);
        }

        void ContinueRun()
        {
            resumeSave = SaveSystem.Load();
            if (resumeSave == null || resumeSave.players.Count == 0)
            {
                AudioManager.Play(SoundId.BuyFail);
                resumeSave = null;
                SaveSystem.Delete();
                RefreshMenuButtons();
                return;
            }
            OpenLobby();
        }

        void OpenOptions()
        {
            if (options == null) return;
            AudioManager.Play(SoundId.UiConfirm);
            menuPanel.SetActive(false);
            options.Open(EventSystem.current, () =>
            {
                menuPanel.SetActive(true);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(optionsButton.gameObject);
            });
        }

        void OpenChallenges()
        {
            if (challenges == null) return;
            AudioManager.Play(SoundId.UiConfirm);
            menuPanel.SetActive(false);
            challenges.Open(EventSystem.current, () =>
            {
                menuPanel.SetActive(true);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(challengesButton.gameObject);
            });
        }

        void OpenLobby()
        {
            AudioManager.Play(SoundId.UiConfirm);
            inLobby = true;
            menuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            for (int i = 0; i < slots.Length; i++)
            {
                var rt = slots[i].Rect;
                Tween.StopAll(rt);
                rt.anchoredPosition = slotPositions[i] + new Vector2(0f, -900f);
                Tween.UIAnchoredPosition(rt, slotPositions[i], 0.45f, Ease.OutBack, startDelay: i * 0.06f);
            }

            // Celui qui a cliqué "Nouvelle partie" rejoint directement
            if (lastDevice is Gamepad pad) Join(pad);
            else if (lastDevice is Keyboard or Mouse && Keyboard.current != null) Join(Keyboard.current);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================================================================ Sélection

        void Update()
        {
            AnimateMenuFarmers();
            if (!inLobby) return;
            if (dangerMenu != null && (dangerMenu.IsOpen || dangerClosedFrame == Time.frameCount)) return;

            HandleJoinAndBack();
            for (int i = joined.Count - 1; i >= 0; i--)
                if (i < joined.Count) HandleSlotInput(i);
            UpdateStart();
            RefreshLobby();
        }

        /// <summary>
        /// Un personnage ajouté à la liste sans reconstruire la scène : on duplique la première image
        /// (le conteneur « Characters » a un HorizontalLayoutGroup qui les aligne).
        /// </summary>
        void AddMissingMenuFarmers()
        {
            if (menuFarmers == null || menuFarmers.Length == 0 || characters.Length <= menuFarmers.Length) return;
            var all = new List<Image>(menuFarmers);
            while (all.Count < characters.Length)
            {
                var copy = Instantiate(menuFarmers[0], menuFarmers[0].transform.parent);
                copy.name = "Character " + (all.Count + 1);
                all.Add(copy);
            }
            menuFarmers = all.ToArray();
        }

        void AnimateMenuFarmers()
        {
            for (int i = 0; i < menuFarmers.Length && i < characters.Length; i++)
                menuFarmers[i].sprite = Frame(characters[i].runFrames, characters[i].animFps, i * 0.13f);
        }

        void HandleJoinAndBack()
        {
            var kb = Keyboard.current;
            if (joined.Count < RequiredPlayers)
            {
                if (kb != null && !IsUsed(kb) && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Join(kb);
                foreach (var pad in Gamepad.all)
                    if (!IsUsed(pad) && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) Join(pad);
            }

            // Personne n'a rejoint : retour au menu
            if (joined.Count > 0) return;
            bool back = kb != null && kb.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) back |= pad.buttonEast.wasPressedThisFrame;
            if (!back) return;
            AudioManager.Play(SoundId.UiBack);
            ShowMenu();
        }

        bool IsUsed(InputDevice device)
        {
            foreach (var j in joined)
                if (Array.IndexOf(j.devices, device) >= 0) return true;
            return false;
        }

        void Join(InputDevice device)
        {
            if (joined.Count >= RequiredPlayers || IsUsed(device)) return;

            var j = new Joined { joinFrame = Time.frameCount, characterIndex = joined.Count % characters.Length };
            if (resumeSave != null)
            {
                // Reprise : chaque emplacement garde le personnage de la sauvegarde
                int saved = Array.FindIndex(characters, c => c != null && c.name == resumeSave.players[joined.Count].character);
                if (saved >= 0) j.characterIndex = saved;
            }
            if (device is Gamepad pad)
            {
                j.pad = pad;
                j.devices = new InputDevice[] { pad };
                j.scheme = "Gamepad";
            }
            else
            {
                var devices = new List<InputDevice> { Keyboard.current };
                if (Mouse.current != null) devices.Add(Mouse.current);
                j.devices = devices.ToArray();
                j.scheme = Player.KeyboardScheme;
            }
            joined.Add(j);
            startTimer = -1f;
            AudioManager.Play(SoundId.UiJoin);
            if (joined.Count <= slots.Length) Punch(slots[joined.Count - 1].transform);
        }

        void HandleSlotInput(int index)
        {
            var j = joined[index];
            if (j.joinFrame == Time.frameCount) return; // la touche qui a fait rejoindre ne compte pas

            bool left, right, confirm, back;
            if (j.pad != null)
            {
                float x = j.pad.leftStick.x.ReadValue();
                left = j.pad.dpad.left.wasPressedThisFrame || (x < -StickThreshold && j.previousStickX >= -StickThreshold);
                right = j.pad.dpad.right.wasPressedThisFrame || (x > StickThreshold && j.previousStickX <= StickThreshold);
                j.previousStickX = x;
                confirm = j.pad.buttonSouth.wasPressedThisFrame || j.pad.startButton.wasPressedThisFrame;
                back = j.pad.buttonEast.wasPressedThisFrame;
            }
            else
            {
                var kb = Keyboard.current;
                if (kb == null) return;
                left = kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame; // touche A en QWERTY = Q en AZERTY
                right = kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
                confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                back = kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
            }

            if (j.ready)
            {
                if (back)
                {
                    j.ready = false;
                    startTimer = -1f;
                    AudioManager.Play(SoundId.UiBack);
                }
                return;
            }

            if (left) ChangeCharacter(index, -1);
            if (right) ChangeCharacter(index, +1);
            if (confirm)
            {
                j.ready = true;
                AudioManager.Play(SoundId.UiReady);
                if (index < slots.Length) Punch(slots[index].status.transform);
            }
            if (back)
            {
                joined.RemoveAt(index);
                startTimer = -1f;
                AudioManager.Play(SoundId.UiBack);
            }
        }

        void ChangeCharacter(int index, int delta)
        {
            var j = joined[index];
            if (resumeSave != null) return; // personnages imposés par la sauvegarde
            j.characterIndex = (j.characterIndex + delta + characters.Length) % characters.Length;
            AudioManager.Play(SoundId.CharacterChange);
            if (index >= slots.Length) return;
            Punch(slots[index].portrait.transform);
            Punch(slots[index].characterName.transform);
        }

        void UpdateStart()
        {
            bool allReady = joined.Count > 0 && (resumeSave == null || joined.Count == RequiredPlayers);
            foreach (var j in joined) allReady &= j.ready;

            if (!allReady)
            {
                startTimer = -1f;
                return;
            }
            if (startTimer < 0f) startTimer = StartDelay;
            startTimer -= Time.deltaTime;
            if (startTimer > 0f) return;
            startTimer = -1f;

            // Reprise : la difficulté est celle de la sauvegarde
            if (resumeSave != null || dangerMenu == null)
            {
                if (resumeSave != null) Session.Difficulty = resumeSave.difficulty;
                StartGame();
                return;
            }
            AudioManager.Play(SoundId.UiConfirm);
            dangerMenu.Open(EventSystem.current, _ => StartGame(), () =>
            {
                // Retour : tout le monde redevient « pas prêt » pour pouvoir changer de personnage
                dangerClosedFrame = Time.frameCount;
                foreach (var j in joined) j.ready = false;
            });
        }

        void StartGame()
        {
            AudioManager.Play(SoundId.UiConfirm);
            inLobby = false;
            Session.Players.Clear();
            Session.Resume = resumeSave;
            if (resumeSave == null) SaveSystem.Delete(); // nouvelle partie : l'ancienne sauvegarde est écrasée
            foreach (var j in joined)
                Session.Players.Add(new Session.Slot { devices = j.devices, controlScheme = j.scheme, character = characters[j.characterIndex] });
            SceneTransition.LoadScene(Session.GameScene);
        }

        // ================================================================ Affichage

        void RefreshLobby()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var v = slots[i];
                bool has = i < joined.Count;
                v.emptyGroup.SetActive(!has);
                v.filledGroup.SetActive(has);

                if (!has)
                {
                    v.header.text = "P" + (i + 1);
                    v.emptyText.text = "Press\n<color=#FFD966>Enter</color> or <color=#FFD966>A</color>\nto join";
                    v.emptyText.alpha = 0.35f + 0.25f * Mathf.Sin(Time.time * 3f + i);
                    continue;
                }

                var j = joined[i];
                var c = characters[j.characterIndex];
                v.header.text = $"P{i + 1} - {(j.pad != null ? "Gamepad" : "Keyboard")}";
                v.portrait.sprite = Frame(j.ready ? c.runFrames : c.idleFrames, c.animFps, i * 0.2f);
                v.characterName.text = string.IsNullOrEmpty(c.tagline)
                    ? c.displayName
                    : $"{c.displayName} <size=55%><color=#BBBBBB>{c.tagline}</color></size>";
                v.stats.text = DescribeStats(c);
                v.weaponIcon.sprite = c.startingWeapon != null ? c.startingWeapon.Icon : null;
                v.weaponIcon.enabled = c.startingWeapon != null;
                v.weaponName.text = c.startingWeapon != null ? "Weapon: " + c.startingWeapon.displayName : "";
                // Flèches seulement pour une nouvelle partie (en reprise, les personnages sont imposés)
                bool canChange = !j.ready && resumeSave == null;
                v.leftArrow.gameObject.SetActive(canChange);
                v.rightArrow.gameObject.SetActive(canChange);

                v.status.text = j.ready ? "READY!" : j.pad != null ? "A: ready" : "Enter: ready";
                v.status.color = j.ready ? Session.ColorOf(i) : new Color(1f, 1f, 1f, 0.8f);
                if (v.hint != null) v.hint.gameObject.SetActive(false);
            }

            if (resumeSave != null && joined.Count < RequiredPlayers)
                footer.text = $"Resuming run (wave {resumeSave.waveIndex + 2}): {RequiredPlayers} player(s) needed";
            else if (joined.Count == 0) footer.text = "";
            else if (startTimer >= 0f) footer.text = $"<color=#7CFF7C>Everyone is ready! Starting in {Mathf.CeilToInt(startTimer)}...</color>";
            else
            {
                int ready = 0;
                foreach (var j in joined)
                    if (j.ready) ready++;
                footer.text = $"Players ready: {ready} / {joined.Count}";
            }
        }

        static string DescribeStats(CharacterData c)
        {
            var sb = new StringBuilder();
            foreach (var m in c.baseStats)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(StatInfo.FormatModifier(m.stat, m.value));
            }
            return sb.ToString();
        }

        static Sprite Frame(Sprite[] frames, float fps, float offset)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[(int)((Time.time + offset) * fps) % frames.Length];
        }

        static void Punch(Transform t)
        {
            Tween.CompleteAll(t);
            t.localScale = Vector3.one;
            Tween.PunchScale(t, new Vector3(0.12f, 0.12f, 0f), 0.25f);
        }
    }
}
