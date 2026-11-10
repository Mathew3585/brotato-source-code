using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using Steamworks;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace BrotatoLike
{
    /// <summary>
    /// Sessions en ligne (FishNet) :
    /// - Steam : l'hôte crée un lobby Steam avec un code à 6 caractères, les amis le rejoignent avec ce code.
    ///   On peut aussi inviter ses amis Steam (overlay) : ils rejoignent depuis l'invitation ou « Join game ».
    /// - LAN / IP : seulement pour tester seul (commandes debug hostlan / joinlan), plus de bouton dans le menu.
    /// Placé sur le même objet que le NetworkManager (persistant entre les scènes).
    /// </summary>
    public class OnlineManager : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        const string GameKey = "cursedfarm_brotatolike";
        const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sans 0/O ni 1/I
        const int TugboatIndex = 0;
        const int SteamIndex = 1;

        public static OnlineManager Instance { get; private set; }

        [Header("Prefabs réseau")]
        [SerializeField] NetworkObject lobbyPlayerPrefab;
        [SerializeField] NetworkObject netGamePrefab;
        [SerializeField] NetworkObject playerAvatarPrefab;
        [SerializeField] ushort lanPort = 7770;

        NetworkManager nm;
        Multipass multipass;
        Tugboat tugboat;
        FishySteamworks.FishySteamworks steam;

        CallResult<LobbyCreated_t> lobbyCreated;
        CallResult<LobbyMatchList_t> lobbyList;
        CallResult<LobbyEnter_t> lobbyEnter;
        Callback<GameLobbyJoinRequested_t> joinRequested;
        Callback<GameRichPresenceJoinRequested_t> presenceJoinRequested;
        // Invitation acceptée hors du menu (en partie, pendant une connexion) : traitée au retour au menu
        CSteamID pendingLobby;
        CSteamID currentLobby;
        bool usingSteam;
        bool wasConnected;

        /// <summary>Code à donner aux amis (code Steam ou adresse IP en LAN).</summary>
        public string RoomCode { get; private set; } = "";
        public string Status { get; private set; } = "";
        public bool InSession => nm != null && (nm.IsClientStarted || nm.IsServerStarted);
        public bool IsHost => nm != null && nm.IsServerStarted;
        public bool Busy { get; private set; }
        public NetworkManager Network => nm;
        /// <summary>On est dans un lobby Steam : on peut inviter des amis.</summary>
        public bool CanInvite => usingSteam && currentLobby.IsValid() && SteamBootstrap.Ready;

        /// <summary>Le statut ou la session a changé (pour rafraîchir l'UI).</summary>
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            nm = GetComponent<NetworkManager>();
            // Le NetworkManager en double (scène rechargée) est détruit par FishNet : on ignore ce doublon
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
            multipass = GetComponent<Multipass>();
            tugboat = GetComponent<Tugboat>();
            steam = GetComponent<FishySteamworks.FishySteamworks>();
        }

        void Start()
        {
            if (Instance != this) return;
            nm.ClientManager.OnClientConnectionState += OnClientConnectionState;
            nm.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            nm.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            nm.SceneManager.OnLoadEnd += OnSceneLoadEnd;
            // Clients : le rideau se ferme quand l'hôte change de scène
            nm.SceneManager.OnLoadStart += _ => SceneTransition.Cover(null);
            nm.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChange;

            if (SteamBootstrap.Ready)
            {
                // Invitation acceptée (overlay / chat Steam) ou « Join game » dans la liste d'amis
                joinRequested = Callback<GameLobbyJoinRequested_t>.Create(r => RequestJoin(r.m_steamIDLobby));
                presenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(r => RequestJoin(ParseLobby(r.m_rgchConnect)));
                // Jeu lancé depuis une invitation : Steam passe « +connect_lobby <id> » en ligne de commande
                RequestJoin(ParseLobby(string.Join(" ", Environment.GetCommandLineArgs())));
            }
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            NetMode.IsOnline = NetMode.IsServer = false;
        }

        void Update()
        {
            if (Instance != this) return;
            NetMode.IsOnline = InSession;
            NetMode.IsServer = IsHost;

            if (pendingLobby.IsValid() && !InSession && !Busy && UnitySceneManager.GetActiveScene().name == Session.MenuScene)
            {
                var lobby = pendingLobby;
                pendingLobby = CSteamID.Nil;
                JoinLobby(lobby);
            }
        }

        void SetStatus(string status, bool busy = false)
        {
            Status = status;
            Busy = busy;
            Changed?.Invoke();
        }

        // ================================================================ Héberger / rejoindre (Steam)

        public void HostSteam()
        {
            if (InSession || Busy) return;
            if (!SteamBootstrap.Ready)
            {
                SetStatus(SteamBootstrap.Error);
                return;
            }
            SetStatus("Creating room...", true);
            lobbyCreated ??= CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxPlayers));
        }

        void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                SetStatus("Could not create the Steam room.");
                return;
            }
            currentLobby = new CSteamID(result.m_ulSteamIDLobby);
            RoomCode = GenerateCode();
            SteamMatchmaking.SetLobbyData(currentLobby, "game", GameKey);
            SteamMatchmaking.SetLobbyData(currentLobby, "code", RoomCode);
            SteamMatchmaking.SetLobbyData(currentLobby, "host", SteamUser.GetSteamID().ToString());

            usingSteam = true;
            multipass.SetClientTransport(SteamIndex);
            if (!multipass.StartConnection(true, SteamIndex))
            {
                SetStatus("Could not start the Steam server.");
                LeaveSteamLobby();
                return;
            }
            nm.ClientManager.StartConnection();
            GUIUtility.systemCopyBuffer = RoomCode;
            SetPresence();
            SetStatus($"Room created! Code {RoomCode} copied to clipboard.");
        }

        public void JoinSteam(string code)
        {
            if (InSession || Busy) return;
            if (!SteamBootstrap.Ready)
            {
                SetStatus(SteamBootstrap.Error);
                return;
            }
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                SetStatus("Type the room code of your friend.");
                return;
            }
            SetStatus($"Looking for room {code}...", true);
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", GameKey, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("code", code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            lobbyList ??= CallResult<LobbyMatchList_t>.Create(OnLobbyList);
            lobbyList.Set(SteamMatchmaking.RequestLobbyList());
        }

        void OnLobbyList(LobbyMatchList_t result, bool ioFailure)
        {
            if (ioFailure || result.m_nLobbiesMatching == 0)
            {
                SetStatus("Room not found. Check the code (and that your friend is hosting).");
                return;
            }
            JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
        }

        void JoinLobby(CSteamID lobby)
        {
            SetStatus("Joining the room...", true);
            lobbyEnter ??= CallResult<LobbyEnter_t>.Create(OnLobbyEnter);
            lobbyEnter.Set(SteamMatchmaking.JoinLobby(lobby));
        }

        // ================================================================ Invitations Steam

        /// <summary>Ouvre l'overlay Steam pour inviter des amis dans le lobby actuel.</summary>
        public void InviteFriends()
        {
            if (!CanInvite) return;
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobby);
        }

        /// <summary>Un ami nous invite : on rejoint tout de suite si on est libre au menu, sinon plus tard.</summary>
        void RequestJoin(CSteamID lobby)
        {
            if (!lobby.IsValid() || lobby == currentLobby) return;
            if (InSession)
            {
                // Déjà dans une room : on la quitte d'abord (l'invitation est traitée au retour au menu)
                LeaveSession("Joining your friend's room...");
            }
            pendingLobby = lobby;
        }

        /// <summary>Lit l'identifiant de lobby dans « +connect_lobby 1234... ».</summary>
        static CSteamID ParseLobby(string text)
        {
            if (string.IsNullOrEmpty(text)) return CSteamID.Nil;
            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i] == "+connect_lobby" && ulong.TryParse(parts[i + 1], out ulong id)) return new CSteamID(id);
            return CSteamID.Nil;
        }

        /// <summary>« Join game » dans la liste d'amis Steam pointe vers notre lobby.</summary>
        void SetPresence()
        {
            if (currentLobby.IsValid()) SteamFriends.SetRichPresence("connect", "+connect_lobby " + currentLobby.m_SteamID);
        }

        void OnLobbyEnter(LobbyEnter_t result, bool ioFailure)
        {
            if (ioFailure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                SetStatus("Could not join the room (full or closed).");
                return;
            }
            currentLobby = new CSteamID(result.m_ulSteamIDLobby);
            string host = SteamMatchmaking.GetLobbyData(currentLobby, "host");
            RoomCode = SteamMatchmaking.GetLobbyData(currentLobby, "code");
            if (SteamMatchmaking.GetLobbyData(currentLobby, "game") != GameKey || string.IsNullOrEmpty(host))
            {
                SetStatus("This room is not a Cursed Farm room.");
                LeaveSteamLobby();
                return;
            }
            usingSteam = true;
            SetPresence();
            steam.SetClientAddress(host);
            multipass.SetClientTransport(SteamIndex);
            SetStatus("Connecting to the host...", true);
            if (!nm.ClientManager.StartConnection())
            {
                SetStatus("Could not connect to the host.");
                LeaveSteamLobby();
            }
        }

        void LeaveSteamLobby()
        {
            if (currentLobby.IsValid()) SteamMatchmaking.LeaveLobby(currentLobby);
            currentLobby = CSteamID.Nil;
            if (SteamBootstrap.Ready) SteamFriends.ClearRichPresence();
        }

        static string GenerateCode()
        {
            var rng = new System.Random();
            return new string(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[rng.Next(CodeAlphabet.Length)]).ToArray());
        }

        // ================================================================ Héberger / rejoindre (LAN, pour les tests : commandes debug)

        public void HostLan()
        {
            if (InSession || Busy) return;
            usingSteam = false;
            tugboat.SetPort(lanPort);
            multipass.SetClientTransport(TugboatIndex);
            if (!multipass.StartConnection(true, TugboatIndex))
            {
                SetStatus("Could not start the LAN server (port already used?).");
                return;
            }
            tugboat.SetClientAddress("localhost");
            nm.ClientManager.StartConnection();
            RoomCode = LocalIp();
            SetStatus($"LAN room created. Friends join with IP {RoomCode}");
        }

        public void JoinLan(string address)
        {
            if (InSession || Busy) return;
            address = string.IsNullOrWhiteSpace(address) ? "localhost" : address.Trim();
            usingSteam = false;
            tugboat.SetPort(lanPort);
            tugboat.SetClientAddress(address);
            multipass.SetClientTransport(TugboatIndex);
            RoomCode = address;
            SetStatus($"Connecting to {address}...", true);
            if (!nm.ClientManager.StartConnection()) SetStatus("Could not connect.");
        }

        static string LocalIp()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork) return ip.ToString();
            }
            catch (Exception) { }
            return "localhost";
        }

        // ================================================================ Quitter

        /// <summary>Quitte la session (l'hôte la ferme pour tout le monde) et revient au menu principal.</summary>
        public void LeaveSession(string message = "")
        {
            wasConnected = false;
            if (nm.IsServerStarted) nm.ServerManager.StopConnection(true);
            if (nm.IsClientStarted) nm.ClientManager.StopConnection();
            LeaveSteamLobby();
            RoomCode = "";
            NetMode.IsOnline = NetMode.IsServer = false;
            Time.timeScale = 1f;
            SetStatus(message);
            if (UnitySceneManager.GetActiveScene().name != Session.MenuScene)
                SceneTransition.Cover(() => UnitySceneManager.LoadScene(Session.MenuScene));
        }

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                wasConnected = true;
                SetStatus(IsHost ? Status : "Connected!");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                bool lost = wasConnected;
                if (Busy || lost)
                {
                    // Connexion perdue (hôte parti) ou échouée
                    LeaveSession(lost ? "Disconnected from the host." : "Could not connect.");
                }
            }
        }

        // ================================================================ Serveur : joueurs

        void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Started) return;
            // Partie déjà lancée ou complète : on refuse
            bool inGame = UnitySceneManager.GetActiveScene().name == Session.GameScene;
            if (NetLobbyPlayer.All.Count >= MaxPlayers || inGame) conn.Disconnect(true);
        }

        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer) return;
            int index = Enumerable.Range(0, MaxPlayers).FirstOrDefault(i => NetLobbyPlayer.ByIndex(i) == null && !IsIndexTaken(i));
            if (NetLobbyPlayer.All.Count >= MaxPlayers)
            {
                conn.Disconnect(true);
                return;
            }
            var lobbyPlayer = Instantiate(lobbyPlayerPrefab);
            var lp = lobbyPlayer.GetComponent<NetLobbyPlayer>();
            lp.Index.Value = (byte)index;
            lp.Character.Value = (byte)index;
            nm.ServerManager.Spawn(lobbyPlayer, conn);
            if (!NetLobbyPlayer.All.Contains(lp)) NetLobbyPlayer.All.Add(lp);
        }

        static bool IsIndexTaken(int index)
        {
            foreach (var p in NetLobbyPlayer.All)
                if (p != null && p.Index.Value == index) return true;
            return false;
        }

        /// <summary>Hôte : tout le monde est prêt, on charge la scène de jeu chez tous les joueurs.</summary>
        public void StartGame()
        {
            if (!IsHost) return;
            foreach (var p in NetLobbyPlayer.All) p.Ready.Value = false;
            if (currentLobby.IsValid()) SteamMatchmaking.SetLobbyJoinable(currentLobby, false);
            var load = new SceneLoadData(Session.GameScene) { ReplaceScenes = ReplaceOption.All };
            SceneTransition.Cover(() => nm.SceneManager.LoadGlobalScenes(load));
        }

        /// <summary>Hôte : fin de partie, tout le monde revient au lobby en ligne (menu principal).</summary>
        public void ReturnToLobby()
        {
            if (!IsHost) return;
            foreach (var p in NetLobbyPlayer.All) p.Ready.Value = false;
            if (currentLobby.IsValid()) SteamMatchmaking.SetLobbyJoinable(currentLobby, true);
            var load = new SceneLoadData(Session.MenuScene) { ReplaceScenes = ReplaceOption.All };
            SceneTransition.Cover(() => nm.SceneManager.LoadGlobalScenes(load));
        }

        void OnSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!args.QueueData.AsServer) return;
            foreach (var scene in args.LoadedScenes)
            {
                if (scene.name != Session.GameScene) continue;
                var game = Instantiate(netGamePrefab);
                nm.ServerManager.Spawn(game, null, scene);
            }
        }

        /// <summary>Serveur : un client vient de charger la scène de jeu, on lui crée son personnage.</summary>
        void OnClientPresenceChange(ClientPresenceChangeEventArgs args)
        {
            if (!args.Added || args.Scene.name != Session.GameScene || !nm.IsServerStarted) return;
            NetLobbyPlayer lobby = null;
            foreach (var p in NetLobbyPlayer.All)
                if (p != null && p.Owner == args.Connection) lobby = p;
            if (lobby == null) return;

            var avatar = Instantiate(playerAvatarPrefab);
            var np = avatar.GetComponent<NetPlayer>();
            np.PlayerIndex.Value = lobby.Index.Value;
            np.CharacterIndex.Value = lobby.Character.Value;
            avatar.transform.position = (Vector2)new Vector2((lobby.Index.Value - 1.5f) * 1.5f, 0f);
            nm.ServerManager.Spawn(avatar, args.Connection, args.Scene);
        }

        /// <summary>Nombre de joueurs attendus dans la partie (pour lancer le décompte).</summary>
        public static int ExpectedPlayers => NetLobbyPlayer.All.Count;
    }
}
