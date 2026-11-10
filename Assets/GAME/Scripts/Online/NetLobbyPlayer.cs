using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace BrotatoLike
{
    /// <summary>
    /// Un joueur connecté à la session en ligne (un par PC). Objet réseau global : il survit aux changements de scène.
    /// Contient son numéro (J1..J4), son personnage, s'il est prêt et son pseudo.
    /// </summary>
    public class NetLobbyPlayer : NetworkBehaviour
    {
        public static readonly List<NetLobbyPlayer> All = new();

        public readonly SyncVar<byte> Index = new();
        public readonly SyncVar<byte> Character = new();
        public readonly SyncVar<bool> Ready = new();
        public readonly SyncVar<string> DisplayName = new("");

        /// <summary>Le joueur de CE PC.</summary>
        public static NetLobbyPlayer Local
        {
            get
            {
                foreach (var p in All)
                    if (p != null && p.IsOwner) return p;
                return null;
            }
        }

        public static NetLobbyPlayer ByIndex(int index)
        {
            foreach (var p in All)
                if (p != null && p.Index.Value == index) return p;
            return null;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!All.Contains(this)) All.Add(this);
            if (IsOwner) CmdSetName(SteamBootstrap.PlayerName(Index.Value));
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            All.Remove(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            All.Remove(this);
        }

        [ServerRpc]
        public void CmdSetCharacter(byte character)
        {
            if (!Ready.Value) Character.Value = character;
        }

        [ServerRpc]
        public void CmdSetReady(bool ready) => Ready.Value = ready;

        [ServerRpc]
        void CmdSetName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) playerName = "Player " + (Index.Value + 1);
            DisplayName.Value = playerName.Length > 20 ? playerName.Substring(0, 20) : playerName;
        }
    }
}
