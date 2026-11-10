namespace BrotatoLike
{
    /// <summary>
    /// Boutique d'un joueur (chaque joueur a la sienne en coop). Les offres verrouillées sont conservées
    /// d'une vague à l'autre.
    /// </summary>
    public class PlayerShop
    {
        public class Offer
        {
            public ShopItemData item;
            public bool locked;
            public bool sold;
        }

        public enum BuyResult { Ok, NotEnoughMaterials, InventoryFull, MaxReached, Unavailable }

        public readonly Offer[] Offers;
        public int RerollCount { get; private set; }

        readonly Player player;
        readonly ShopCatalog catalog;

        public PlayerShop(Player player, ShopCatalog catalog)
        {
            this.player = player;
            this.catalog = catalog;
            Offers = new Offer[catalog.offerCount];
            for (int i = 0; i < Offers.Length; i++) Offers[i] = new Offer();
        }

        public void Open(int waveIndex)
        {
            RerollCount = 0;
            Roll(waveIndex);
        }

        static readonly System.Collections.Generic.List<WeaponData> ownedBuffer = new();

        void Roll(int waveIndex)
        {
            ownedBuffer.Clear();
            foreach (var w in player.Weapons.Weapons) ownedBuffer.Add(w.Data);

            foreach (var offer in Offers)
            {
                if (offer.locked && !offer.sold) continue;
                offer.item = catalog.RollOffer(waveIndex, player.Stats[StatType.Luck], player.CanOwnMore, ownedBuffer);
                offer.sold = false;
                offer.locked = false;
            }
            player.MarkNetDirty();
        }

        public void Export(PlayerSave save)
        {
            save.offerItems.Clear();
            save.offerLocked.Clear();
            save.offerSold.Clear();
            foreach (var o in Offers)
            {
                save.offerItems.Add(o.item != null ? o.item.name : "");
                save.offerLocked.Add(o.locked);
                save.offerSold.Add(o.sold);
            }
            save.rerollCount = RerollCount;
        }

        public void Restore(PlayerSave save, System.Func<string, ShopItemData> resolve)
        {
            for (int i = 0; i < Offers.Length && i < save.offerItems.Count; i++)
            {
                Offers[i].item = resolve(save.offerItems[i]);
                Offers[i].locked = i < save.offerLocked.Count && save.offerLocked[i];
                Offers[i].sold = i < save.offerSold.Count && save.offerSold[i];
            }
            RerollCount = save.rerollCount;
        }

        public int Price(Offer offer, int waveIndex) => catalog.PriceAtWave(offer.item, waveIndex);
        public int RerollPrice(int waveIndex) => catalog.RerollPrice(waveIndex, RerollCount);
        public int SellValue(Weapon weapon, int waveIndex) => catalog.SellValue(weapon.Data, waveIndex);

        public bool TryReroll(int waveIndex)
        {
            if (!player.SpendMaterials(RerollPrice(waveIndex))) return false;
            RerollCount++;
            Roll(waveIndex);
            return true;
        }

        public void ToggleLock(int index)
        {
            var offer = Offers[index];
            if (!offer.sold && offer.item != null) offer.locked = !offer.locked;
            player.MarkNetDirty();
        }

        public BuyResult CanBuy(int index, int waveIndex)
        {
            var offer = Offers[index];
            if (offer.sold || offer.item == null) return BuyResult.Unavailable;
            if (player.Materials < Price(offer, waveIndex)) return BuyResult.NotEnoughMaterials;

            switch (offer.item)
            {
                case WeaponData weapon when !player.Weapons.CanAcquire(weapon):
                    return BuyResult.InventoryFull;
                case ItemData item when item.maxPerPlayer > 0 && player.ItemCount(item) >= item.maxPerPlayer:
                    return BuyResult.MaxReached;
            }
            return BuyResult.Ok;
        }

        public BuyResult TryBuy(int index, int waveIndex)
        {
            var result = CanBuy(index, waveIndex);
            if (result != BuyResult.Ok) return result;

            var offer = Offers[index];
            player.SpendMaterials(Price(offer, waveIndex));
            if (offer.item is WeaponData weapon) player.Weapons.Acquire(weapon);
            else if (offer.item is ItemData item) player.AddItem(item);

            offer.sold = true;
            offer.locked = false;
            player.MarkNetDirty();
            return BuyResult.Ok;
        }

        public bool TrySell(Weapon weapon, int waveIndex)
        {
            if (weapon == null || player.Weapons.Weapons.Count <= 1) return false; // on garde au moins une arme
            player.CollectMaterials(SellValue(weapon, waveIndex), giveXp: false);
            player.Weapons.Remove(weapon);
            return true;
        }

        // ------------------------------------------------------------ Don à un coéquipier (coop)

        public enum GiveResult { Ok, Invalid, LastWeapon, NoRoom, MaxReached }

        /// <summary>Un joueur (n'importe lequel) vient de donner une arme ou un objet : (donneur, receveur, ce qui a été donné).</summary>
        public static event System.Action<Player, Player, ShopItemData> Transferred;

        public static void ClearStaticEvents() => Transferred = null;

        public GiveResult CanGiveWeapon(Weapon weapon, Player to)
        {
            if (weapon == null || to == null || to == player) return GiveResult.Invalid;
            if (player.Weapons.Weapons.Count <= 1) return GiveResult.LastWeapon; // on garde au moins une arme
            // Inventaire plein : passe quand même si le receveur a la même arme (fusion au tier suivant)
            if (!to.Weapons.CanAcquire(weapon.Data)) return GiveResult.NoRoom;
            return GiveResult.Ok;
        }

        public GiveResult CanGiveItem(ItemData item, Player to)
        {
            if (item == null || to == null || to == player || player.ItemCount(item) == 0) return GiveResult.Invalid;
            if (!to.CanOwnMore(item)) return GiveResult.MaxReached;
            return GiveResult.Ok;
        }

        public GiveResult TryGiveWeapon(Weapon weapon, Player to)
        {
            var result = CanGiveWeapon(weapon, to);
            if (result != GiveResult.Ok) return result;
            var data = weapon.Data;
            player.Weapons.Remove(weapon);
            to.Weapons.Acquire(data);
            Transferred?.Invoke(player, to, data);
            return GiveResult.Ok;
        }

        public GiveResult TryGiveItem(ItemData item, Player to)
        {
            var result = CanGiveItem(item, to);
            if (result != GiveResult.Ok) return result;
            player.RemoveItem(item);
            to.AddItem(item);
            Transferred?.Invoke(player, to, item);
            return GiveResult.Ok;
        }
    }
}
