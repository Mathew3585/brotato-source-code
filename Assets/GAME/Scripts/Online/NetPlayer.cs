using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrotatoLike
{
    /// <summary>
    /// Personnage d'un joueur en ligne (un par PC). Le propriétaire le déplace lui-même (NetworkTransform
    /// côté client) ; l'hôte calcule tout le reste. Sert aussi à envoyer à l'hôte la visée et les actions
    /// de boutique de ce joueur.
    /// </summary>
    public class NetPlayer : NetworkBehaviour
    {
        const float AimSendInterval = 0.1f;

        [SerializeField] Player player;
        [Tooltip("Contrôles du jeu (copiés pour le joueur de ce PC)")]
        [SerializeField] InputActionAsset controls;

        public readonly SyncVar<byte> PlayerIndex = new();
        public readonly SyncVar<byte> CharacterIndex = new();
        readonly SyncVar<bool> manualAim = new();
        readonly SyncVar<Vector2> aimDirection = new(Vector2.right);

        public Player Player => player;
        bool initialized;
        float aimTimer;

        /// <summary>Le NetPlayer de CE PC.</summary>
        public static NetPlayer Local { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Local = null;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (initialized) return;
            initialized = true;
            if (IsOwner)
            {
                Local = this;
                player.EmotePlayed += SendEmote;
            }

            var gm = GameManager.Instance;
            var character = gm.characters[Mathf.Clamp(CharacterIndex.Value, 0, gm.characters.Length - 1)];
            player.InitOnline(PlayerIndex.Value, IsOwner, controls, character);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
            player.EmotePlayed -= SendEmote;
        }

        // ------------------------------------------------------------ Emotes (affichées tout de suite chez le propriétaire)

        float lastEmoteTime = -10f;

        void SendEmote(int id)
        {
            if (IsServerInitialized) RpcEmote((byte)id);
            else CmdEmote((byte)id);
        }

        [ServerRpc]
        void CmdEmote(byte id)
        {
            // Anti-spam côté hôte
            if (Time.unscaledTime - lastEmoteTime < 0.3f) return;
            lastEmoteTime = Time.unscaledTime;
            RpcEmote(id);
        }

        [ObserversRpc(ExcludeOwner = true)]
        void RpcEmote(byte id) => player.ShowEmote(id);

        void Update()
        {
            if (!initialized) return;

            if (IsOwner)
            {
                // Le propriétaire envoie sa visée régulièrement
                aimTimer -= Time.deltaTime;
                if (aimTimer > 0f) return;
                aimTimer = AimSendInterval;
                if (IsServerInitialized) SetAim(player.ManualAim, player.AimDirection);
                else CmdAim(player.ManualAim, player.AimDirection);
            }
            else
            {
                player.SetRemoteAim(manualAim.Value, aimDirection.Value);
            }
        }

        void SetAim(bool manual, Vector2 direction)
        {
            if (manualAim.Value != manual) manualAim.Value = manual;
            if ((aimDirection.Value - direction).sqrMagnitude > 0.001f) aimDirection.Value = direction;
        }

        [ServerRpc]
        void CmdAim(bool manual, Vector2 direction) => SetAim(manual, direction);

        // ------------------------------------------------------------ Boutique (exécutée par l'hôte)

        static int WaveIndex => GameManager.Instance.WaveIndex;
        static bool InShop => GameManager.Instance.State == GameState.Shop;

        [ServerRpc]
        public void CmdBuy(int offerIndex)
        {
            if (InShop) player.Shop.TryBuy(offerIndex, WaveIndex);
        }

        [ServerRpc]
        public void CmdToggleLock(int offerIndex)
        {
            if (InShop) player.Shop.ToggleLock(offerIndex);
        }

        [ServerRpc]
        public void CmdReroll()
        {
            if (InShop) player.Shop.TryReroll(WaveIndex);
        }

        [ServerRpc]
        public void CmdSell(int weaponIndex)
        {
            var list = player.Weapons.Weapons;
            if (InShop && weaponIndex >= 0 && weaponIndex < list.Count) player.Shop.TrySell(list[weaponIndex], WaveIndex);
        }

        [ServerRpc]
        public void CmdMerge(int weaponIndex)
        {
            var list = player.Weapons.Weapons;
            if (InShop && weaponIndex >= 0 && weaponIndex < list.Count) player.Weapons.Merge(list[weaponIndex]);
        }

        /// <summary>Coop : donne l'arme n° weaponIndex au joueur toIndex (J1-J4).</summary>
        [ServerRpc]
        public void CmdGiveWeapon(int weaponIndex, int toIndex)
        {
            var list = player.Weapons.Weapons;
            var to = FindPlayer(toIndex);
            if (InShop && to != null && weaponIndex >= 0 && weaponIndex < list.Count) player.Shop.TryGiveWeapon(list[weaponIndex], to);
        }

        /// <summary>Coop : donne un exemplaire de l'objet itemName au joueur toIndex (J1-J4).</summary>
        [ServerRpc]
        public void CmdGiveItem(string itemName, int toIndex)
        {
            var to = FindPlayer(toIndex);
            if (InShop && to != null && GameManager.Instance.ResolveShopItem(itemName) is ItemData item)
                player.Shop.TryGiveItem(item, to);
        }

        static Player FindPlayer(int index)
        {
            foreach (var p in Player.All)
                if (p.Index == index && p.Character != null) return p;
            return null;
        }

        /// <summary>Le choix d'amélioration est tiré sur le PC du joueur ; l'hôte l'applique.</summary>
        [ServerRpc]
        public void CmdUpgrade(int stat, float value)
        {
            if (!InShop || player.PendingLevelUps <= 0) return;
            player.ApplyUpgrade(new UpgradeTable.Choice { stat = (StatType)stat, value = value });
            player.PendingLevelUps--;
        }

        /// <summary>Caisse ouverte sur le PC du joueur : garder l'objet ou le recycler.</summary>
        [ServerRpc]
        public void CmdCrate(bool take, string itemName)
        {
            if (!InShop || player.PendingCrates <= 0) return;
            var catalog = GameManager.Instance.shopCatalog;
            ItemData item = null;
            foreach (var i in catalog.items)
                if (i != null && i.name == itemName) item = i;
            if (item == null) return;
            if (take) player.AddItem(item);
            else player.CollectMaterials(catalog.SellValue(item, WaveIndex), giveXp: false);
            player.PendingCrates--;
        }

        [ServerRpc]
        public void CmdReady(bool ready) => GameManager.Instance.SetPlayerReady(player, ready);
    }
}
