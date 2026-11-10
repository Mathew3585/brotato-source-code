using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// État réseau global, consultable partout sans dépendre de FishNet.
    /// Hors ligne : IsOnline = false, le jeu tourne entièrement en local (coop sur le même PC).
    /// En ligne : l'hôte (IsServer) calcule toute la partie ; les clients ne font qu'afficher et envoyer leurs actions.
    /// </summary>
    public static class NetMode
    {
        public static bool IsOnline { get; set; }
        public static bool IsServer { get; set; }

        /// <summary>Client en ligne qui n'est pas l'hôte : pas de logique de jeu (dégâts, apparitions...), juste l'affichage.</summary>
        public static bool IsClientOnly => IsOnline && !IsServer;

        /// <summary>Hôte en ligne : doit envoyer l'état du jeu aux clients.</summary>
        public static bool IsOnlineHost => IsOnline && IsServer;

        /// <summary>Ce PC fait autorité sur la partie (hors ligne ou hôte).</summary>
        public static bool HasAuthority => !IsClientOnly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            IsOnline = false;
            IsServer = false;
        }
    }
}
