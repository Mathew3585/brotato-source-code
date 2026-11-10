using System.Collections;
using System.Collections.Generic;
using System.Text;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Menu en ligne : héberger une room Steam, rejoindre avec un code (ou une invitation Steam), puis lobby en ligne
    /// (chaque PC choisit son personnage et se met prêt ; l'hôte lance quand tout le monde est prêt).
    /// Dans le lobby, « Invite friends » (I / X) ouvre l'overlay Steam.
    /// L'UI est dans la scène MainMenu.
    /// </summary>
    public class OnlineMenu : MonoBehaviour
    {
        const float StickThreshold = 0.6f;

        [SerializeField] MainMenu mainMenu;
        [SerializeField] GameObject menuPanel;
        [SerializeField] Button onlineButton;

        [Header("Panneau en ligne")]
        [SerializeField] GameObject onlinePanel;
        [SerializeField] Button hostButton;
        [SerializeField] TMP_InputField codeInput;
        [SerializeField] Button joinButton;
        [SerializeField] Button backButton;
        [SerializeField] TextMeshProUGUI status;

        [Header("Lobby en ligne (partagé avec le lobby local)")]
        [SerializeField] GameObject lobbyPanel;
        [SerializeField] LobbySlotView[] slots;
        [SerializeField] TextMeshProUGUI footer;
        [SerializeField] TextMeshProUGUI roomCode;
        [Tooltip("Ouvre l'overlay Steam pour inviter des amis (lobby en ligne)")]
        [SerializeField] Button inviteButton;
        [Tooltip("Choix de la difficulté par l'hôte avant de lancer")]
        [SerializeField] DangerMenu dangerMenu;
        int dangerClosedFrame = -1;

        bool inOnlineLobby;
        float previousStickX;

        static OnlineManager Online => OnlineManager.Instance;

        void Start()
        {
            onlineButton.onClick.AddListener(OpenPanel);
            hostButton.onClick.AddListener(() => Online?.HostSteam());
            joinButton.onClick.AddListener(() => Online?.JoinSteam(codeInput.text));
            backButton.onClick.AddListener(ClosePanel);
            UIFactory.LinkNavigation(new List<List<Selectable>>
            {
                new() { hostButton },
                new() { codeInput, joinButton },
                new() { backButton },
            });

            onlinePanel.SetActive(false);
            roomCode.gameObject.SetActive(false);
            if (inviteButton != null)
            {
                inviteButton.onClick.AddListener(Invite);
                inviteButton.gameObject.SetActive(false);
            }
            if (Online != null) Online.Changed += RefreshStatus;
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                slots[i].leftArrow.onClick.AddListener(() => OnArrowClicked(slot, -1));
                slots[i].rightArrow.onClick.AddListener(() => OnArrowClicked(slot, +1));
            }
            StartCoroutine(ResumeSessionNextFrame());
        }

        void OnDestroy()
        {
            if (Online != null) Online.Changed -= RefreshStatus;
        }

        /// <summary>Retour de partie en ligne (fin de partie) : on revient directement au lobby en ligne.</summary>
        IEnumerator ResumeSessionNextFrame()
        {
            yield return null;
            if (Online != null && Online.InSession) EnterOnlineLobby();
            else if (Online != null && !string.IsNullOrEmpty(Online.Status))
            {
                // Message à afficher au retour (ex : "Disconnected from the host.")
                OpenPanel();
            }
        }

        // ================================================================ Panneau

        void OpenPanel()
        {
            AudioManager.Play(SoundId.UiConfirm);
            menuPanel.SetActive(false);
            onlinePanel.SetActive(true);
            RefreshStatus();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(hostButton.gameObject);
            var rt = (RectTransform)onlinePanel.transform;
            rt.localScale = Vector3.one * 0.9f;
            Tween.Scale(rt, Vector3.one, 0.25f, Ease.OutBack);
        }

        void ClosePanel()
        {
            AudioManager.Play(SoundId.UiBack);
            onlinePanel.SetActive(false);
            mainMenu.ShowMenu();
        }

        void RefreshStatus()
        {
            if (status == null) return;
            if (Online == null)
            {
                status.text = "<color=#FF6B6B>Online mode is not available (no NetworkManager in the scene).</color>";
                return;
            }
            string steamInfo = SteamBootstrap.Ready ? "" : $"<color=#FFB86B>{SteamBootstrap.Error}</color>\n";
            status.text = steamInfo + Online.Status;
            bool idle = !Online.InSession && !Online.Busy;
            hostButton.interactable = joinButton.interactable = idle && SteamBootstrap.Ready;
        }

        // ================================================================ Lobby en ligne

        void EnterOnlineLobby()
        {
            // Invitation acceptée depuis le lobby local ou le menu : on ferme d'abord l'écran local
            mainMenu.ShowMenu();
            inOnlineLobby = true;
            onlinePanel.SetActive(false);
            menuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
            roomCode.gameObject.SetActive(true);
            if (inviteButton != null) inviteButton.gameObject.SetActive(Online.CanInvite);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            AudioManager.Play(SoundId.UiJoin);
        }

        void ExitOnlineLobby()
        {
            inOnlineLobby = false;
            lobbyPanel.SetActive(false);
            roomCode.gameObject.SetActive(false);
            if (inviteButton != null) inviteButton.gameObject.SetActive(false);
            mainMenu.ShowMenu();
        }

        void Update()
        {
            if (Online == null) return;

            if (onlinePanel.activeSelf)
            {
                if (Online.InSession && NetLobbyPlayer.Local != null) EnterOnlineLobby();
                else if (!codeInput.isFocused && BackPressed()) ClosePanel();
                return;
            }

            if (!inOnlineLobby)
            {
                // Invitation Steam acceptée hors du panneau en ligne : on passe au lobby dès qu'on est connecté
                if (Online.InSession && NetLobbyPlayer.Local != null) EnterOnlineLobby();
                return;
            }
            if (!Online.InSession)
            {
                ExitOnlineLobby();
                OpenPanel();
                return;
            }

            // L'hôte choisit la difficulté : le lobby attend
            if (dangerMenu != null && dangerMenu.IsOpen)
            {
                if (!Online.IsHost || !EveryoneReady()) dangerMenu.gameObject.SetActive(false);
                return;
            }
            if (dangerClosedFrame != Time.frameCount) HandleLobbyInput();
            RefreshLobby();
        }

        /// <summary>Clic souris sur une flèche : seulement sur la case du joueur de ce PC.</summary>
        void OnArrowClicked(int slot, int delta)
        {
            if (!inOnlineLobby) return;
            var local = NetLobbyPlayer.Local;
            if (local != null && local.Index.Value == slot && !local.Ready.Value) ChangeCharacter(local, delta);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void ChangeCharacter(NetLobbyPlayer local, int delta)
        {
            int count = mainMenu.characters.Length;
            int next = (local.Character.Value + delta + count) % count;
            local.CmdSetCharacter((byte)next);
            AudioManager.Play(SoundId.CharacterChange);
            int slot = local.Index.Value;
            if (slot < slots.Length) Punch(slots[slot].portrait.transform);
        }

        /// <summary>Tout le monde est prêt (et au moins un joueur).</summary>
        static bool EveryoneReady()
        {
            bool allReady = NetLobbyPlayer.All.Count > 0;
            foreach (var p in NetLobbyPlayer.All) allReady &= p != null && p.Ready.Value;
            return allReady;
        }

        void HandleLobbyInput()
        {
            var local = NetLobbyPlayer.Local;
            if (local == null) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            bool left = kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame);
            bool right = kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame);
            bool confirm = kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
            bool back = BackPressed();
            if (pad != null)
            {
                float x = pad.leftStick.x.ReadValue();
                left |= pad.dpad.left.wasPressedThisFrame || (x < -StickThreshold && previousStickX >= -StickThreshold);
                right |= pad.dpad.right.wasPressedThisFrame || (x > StickThreshold && previousStickX <= StickThreshold);
                previousStickX = x;
                confirm |= pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            }
            if (kb != null && kb.cKey.wasPressedThisFrame && !string.IsNullOrEmpty(Online.RoomCode))
            {
                GUIUtility.systemCopyBuffer = Online.RoomCode;
                AudioManager.Play(SoundId.UiConfirm);
            }
            if ((kb != null && kb.iKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) Invite();

            if (local.Ready.Value)
            {
                // L'hôte lance la partie quand tout le monde est prêt (plus de lancement automatique)
                if (confirm && Online.IsHost && EveryoneReady())
                {
                    AudioManager.Play(SoundId.UiConfirm);
                    if (dangerMenu == null) Online.StartGame();
                    else
                        dangerMenu.Open(EventSystem.current, _ =>
                        {
                            if (Online.InSession && EveryoneReady()) Online.StartGame();
                        }, () => dangerClosedFrame = Time.frameCount);
                    return;
                }
                if (back)
                {
                    local.CmdSetReady(false);
                    AudioManager.Play(SoundId.UiBack);
                }
                return;
            }

            if (left || right) ChangeCharacter(local, right ? 1 : -1);
            if (confirm)
            {
                local.CmdSetReady(true);
                AudioManager.Play(SoundId.UiReady);
            }
            if (back)
            {
                // Quitter la room (l'hôte la ferme pour tout le monde)
                Online.LeaveSession(Online.IsHost ? "Room closed." : "You left the room.");
                ExitOnlineLobby();
                OpenPanel();
            }
        }

        void Invite()
        {
            if (Online == null || !Online.CanInvite) return;
            AudioManager.Play(SoundId.UiConfirm);
            Online.InviteFriends();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        static bool BackPressed()
        {
            var kb = Keyboard.current;
            bool back = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame);
            foreach (var pad in Gamepad.all) back |= pad.buttonEast.wasPressedThisFrame;
            return back;
        }

        void RefreshLobby()
        {
            bool lan = Online.RoomCode.Contains(".") || Online.RoomCode == "localhost";
            roomCode.text = lan
                ? $"LAN ROOM - IP: <color=#FFD966>{Online.RoomCode}</color>"
                : $"ROOM CODE: <color=#FFD966>{Online.RoomCode}</color>   <size=60%>(press C to copy)</size>";

            int ready = 0, total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var v = slots[i];
                var p = NetLobbyPlayer.ByIndex(i);
                bool has = p != null;
                v.emptyGroup.SetActive(!has);
                v.filledGroup.SetActive(has);
                if (!has)
                {
                    v.header.text = "P" + (i + 1);
                    v.emptyText.text = "Waiting for\na friend...";
                    v.emptyText.alpha = 0.35f + 0.25f * Mathf.Sin(Time.time * 3f + i);
                    continue;
                }

                total++;
                if (p.Ready.Value) ready++;
                var c = mainMenu.characters[Mathf.Clamp(p.Character.Value, 0, mainMenu.characters.Length - 1)];
                bool isLocal = p.IsOwner;
                v.header.text = $"P{i + 1} - {p.DisplayName.Value}" + (isLocal ? " (you)" : "");
                v.portrait.sprite = Frame(p.Ready.Value ? c.runFrames : c.idleFrames, c.animFps, i * 0.2f);
                v.characterName.text = string.IsNullOrEmpty(c.tagline)
                    ? c.displayName
                    : $"{c.displayName} <size=55%><color=#BBBBBB>{c.tagline}</color></size>";
                v.stats.text = DescribeStats(c);
                v.weaponIcon.sprite = c.startingWeapon != null ? c.startingWeapon.Icon : null;
                v.weaponIcon.enabled = c.startingWeapon != null;
                v.weaponName.text = c.startingWeapon != null ? "Weapon: " + c.startingWeapon.displayName : "";
                // Flèches sur sa propre case tant qu'on n'est pas prêt
                bool canChange = isLocal && !p.Ready.Value;
                v.leftArrow.gameObject.SetActive(canChange);
                v.rightArrow.gameObject.SetActive(canChange);
                v.status.text = p.Ready.Value ? "READY!" : isLocal ? "Enter / A: ready" : "Choosing...";
                v.status.color = p.Ready.Value ? Session.ColorOf(i) : new Color(1f, 1f, 1f, 0.8f);
                if (v.hint != null) v.hint.gameObject.SetActive(false);
            }

            if (ready == total && total > 0)
                footer.text = Online.IsHost
                    ? "<color=#7CFF7C>Everyone is ready! Press Enter / A to start</color>"
                    : "<color=#7CFF7C>Everyone is ready! Waiting for the host to start...</color>";
            else footer.text = $"Players ready: {ready} / {total}";
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
