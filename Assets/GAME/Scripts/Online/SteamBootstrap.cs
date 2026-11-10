using System;
using Steamworks;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Démarre l'API Steam (Steamworks.NET) une fois pour toute la session et traite ses callbacks.
    /// Pendant le développement, l'AppID vient du fichier steam_appid.txt à la racine du projet (480 = test).
    /// </summary>
    [DefaultExecutionOrder(-10000)] // avant le NetworkManager et FishySteamworks
    public class SteamBootstrap : MonoBehaviour
    {
        public static bool Ready { get; private set; }
        public static string Error { get; private set; } = "";

        static SteamBootstrap instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Ready = false;
            Error = "";
            instance = null;
        }

        void Awake()
        {
            if (instance != null)
            {
                Destroy(this);
                return;
            }
            instance = this;
            if (Ready) return;

            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Error = "Steamworks.NET is not installed correctly.";
                    return;
                }
                Ready = SteamAPI.Init();
                if (!Ready) Error = "Steam is not running (start Steam and log in).";
            }
            catch (Exception e)
            {
                Error = "Steam could not start: " + e.Message;
                Ready = false;
            }
        }

        void Update()
        {
            if (Ready) SteamAPI.RunCallbacks();
        }

        void OnApplicationQuit()
        {
            if (!Ready) return;
            SteamAPI.Shutdown();
            Ready = false;
        }

        /// <summary>Pseudo Steam du joueur (ou un nom par défaut sans Steam).</summary>
        public static string PlayerName(int fallbackIndex)
        {
            if (Ready)
            {
                try { return SteamFriends.GetPersonaName(); }
                catch (Exception) { }
            }
            return "Player " + (fallbackIndex + 1);
        }
    }
}
