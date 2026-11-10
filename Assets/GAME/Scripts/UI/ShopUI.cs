using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Écran entre deux vagues : une colonne par joueur, contrôlée par SA manette / SON clavier.
    /// Chaque colonne enchaîne : caisses → améliorations de niveau → boutique → prêt.
    /// Les 4 colonnes sont dans la scène ; seules celles des joueurs présents sont affichées.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] GameObject shopRoot;
        [Tooltip("Colonnes J1 à J4 (mises en page dans la scène)")]
        [SerializeField] ShopColumnView[] columnViews;
        [SerializeField] float columnGap = 20f;
        [SerializeField] float columnTop = -20f;

        readonly List<Column> columns = new();
        GameManager gm;

        void Start()
        {
            gm = GameManager.Instance;
            shopRoot.SetActive(false);
            foreach (var v in columnViews) v.gameObject.SetActive(false);

            gm.StateChanged += OnStateChanged;
            gm.PlayerJoined += OnPlayerJoined;
            PlayerShop.Transferred += OnTransferred;
        }

        void OnDestroy()
        {
            PlayerShop.Transferred -= OnTransferred;
            if (gm == null) return;
            gm.StateChanged -= OnStateChanged;
            gm.PlayerJoined -= OnPlayerJoined;
        }

        /// <summary>Coop locale : la colonne du receveur se met à jour et annonce le cadeau.</summary>
        void OnTransferred(Player from, Player to, ShopItemData data)
        {
            foreach (var c in columns)
                if (c.Player == to) c.OnReceived(from, data);
        }

        void OnStateChanged(GameState state)
        {
            if (state == GameState.Shop) Open();
            else if (shopRoot.activeSelf) Close();
        }

        void OnPlayerJoined(Player player)
        {
            if (gm.State == GameState.Shop) Open();
        }

        void Open()
        {
            ClearColumns();
            shopRoot.SetActive(true);

            // En ligne, chaque PC ne voit que la boutique de son propre joueur
            var shown = new List<Player>();
            if (NetMode.IsOnline)
            {
                var local = Player.LocalOnline;
                if (local != null) shown.Add(local);
            }
            else shown.AddRange(Player.All);

            int n = Mathf.Min(shown.Count, columnViews.Length);
            float width = columnViews[0].Rect.sizeDelta.x;
            float total = n * width + (n - 1) * columnGap;
            for (int i = 0; i < n; i++)
            {
                float x = -total / 2f + i * (width + columnGap);
                columns.Add(new Column(columnViews[i], shown[i], new Vector2(x, columnTop), i));
            }
        }

        void Close()
        {
            ClearColumns();
            shopRoot.SetActive(false);
        }

        void ClearColumns()
        {
            foreach (var c in columns) c.Unbind();
            columns.Clear();
        }

        void Update()
        {
            foreach (var c in columns) c.Tick();
        }

        // =====================================================================

        class Column
        {
            enum Phase { Crate, LevelUp, Shop, Ready }

            readonly ShopColumnView v;
            readonly Player player;
            Phase phase;
            UpgradeTable.Choice[] choices;
            Weapon selectedWeapon;
            /// <summary>Objet possédé sélectionné (pour le donner à un coéquipier).</summary>
            ItemData selectedItem;
            /// <summary>Coop à 3-4 joueurs : on choisit à quel coéquipier donner.</summary>
            bool choosingTarget;
            /// <summary>Page affichée : boutique (ou caisse / améliorations) ou stats. Q / E, LB / RB.</summary>
            bool statsPage;
            float messageTime;
            const float MessageHold = 1f, MessageFade = 1.2f;
            readonly List<Player> teammates = new();
            ItemData crateItem;

            public Player Player => player;
            bool CanGive => v.giveButton != null && teammates.Count > 0;
            /// <summary>Objets possédés regroupés (un par type, avec leur nombre), dans l'ordre d'achat.</summary>
            readonly List<(ItemData item, int count)> ownedItems = new();

            int Wave => GameManager.Instance.WaveIndex;
            /// <summary>Client en ligne : les actions sont envoyées à l'hôte, qui renvoie le nouvel état.</summary>
            static bool Remote => NetMode.IsClientOnly && NetPlayer.Local != null;

            public Column(ShopColumnView view, Player player, Vector2 position, int index)
            {
                v = view;
                this.player = player;
                v.gameObject.SetActive(true);
                ApplyPlayerColor(player.Color);
                BindButtons();
                player.EventSystem.playerRoot = v.gameObject;
                player.NetStateApplied += OnNetState;
                if (v.tooltipRoot != null) v.tooltipRoot.SetActive(false);

                // Entrée animée
                v.Rect.anchoredPosition = position + new Vector2(0f, -1100f);
                Tween.UIAnchoredPosition(v.Rect, position, 0.45f, Ease.OutBack, startDelay: index * 0.07f);

                EnterPhase(player.IsReady ? Phase.Ready : NextPhase());
            }

            public void Unbind()
            {
                foreach (var t in v.GetComponentsInChildren<Transform>(true)) Tween.CompleteAll(t);
                foreach (var g in v.GetComponentsInChildren<Graphic>(true)) Tween.StopAll(g);
                foreach (var b in v.GetComponentsInChildren<Button>(true)) b.onClick.RemoveAllListeners();
                player.NetStateApplied -= OnNetState;
                player.EventSystem.SetSelectedGameObject(null);
                player.EventSystem.playerRoot = null;
                v.gameObject.SetActive(false);
            }

            void ApplyPlayerColor(Color color)
            {
                v.colorBar.color = color;
                v.title.color = color;
                v.readyText.color = color;
                foreach (var b in v.GetComponentsInChildren<Button>(true))
                    if (System.Array.IndexOf(v.itemIcons, b.targetGraphic as Image) < 0) UIFactory.SetAccent(b, color);
            }

            void BindButtons()
            {
                v.takeButton.onClick.AddListener(() => ResolveCrate(take: true));
                v.recycleButton.onClick.AddListener(() => ResolveCrate(take: false));
                for (int i = 0; i < v.upgrades.Length; i++)
                {
                    int index = i;
                    v.upgrades[i].button.onClick.AddListener(() => ChooseUpgrade(index));
                }
                for (int i = 0; i < v.offers.Length; i++)
                {
                    int index = i;
                    v.offers[i].card.onClick.AddListener(() => Buy(index));
                    v.offers[i].lockButton.onClick.AddListener(() => ToggleLock(index));
                }
                v.rerollButton.onClick.AddListener(Reroll);
                v.readyButton.onClick.AddListener(SetReady);
                for (int i = 0; i < v.weaponSlots.Length; i++)
                {
                    int index = i;
                    v.weaponSlots[i].onClick.AddListener(() => SelectWeapon(index));
                }
                v.sellButton.onClick.AddListener(SellSelected);
                v.mergeButton.onClick.AddListener(MergeSelected);
                v.unreadyButton.onClick.AddListener(CancelReady);

                for (int i = 0; i < v.itemIcons.Length; i++)
                {
                    int index = i;
                    if (v.itemIcons[i].TryGetComponent<Button>(out var b)) b.onClick.AddListener(() => SelectItem(index));
                }
                if (v.giveButton != null) v.giveButton.onClick.AddListener(Give);
                if (v.giveTargets != null)
                    for (int i = 0; i < v.giveTargets.Length; i++)
                    {
                        int index = i;
                        v.giveTargets[i].onClick.AddListener(() =>
                        {
                            if (index < teammates.Count) GiveTo(teammates[index]);
                        });
                    }
            }

            /// <summary>Client en ligne : l'hôte a renvoyé l'état (achat validé, relance...).</summary>
            void OnNetState(Player p)
            {
                if (phase == Phase.Shop && player.IsReady) phase = Phase.Ready;
                Refresh(forceSelectFirst: false);
            }

            // ------------------------------------------------------------ Phases

            Phase NextPhase() =>
                player.PendingCrates > 0 ? Phase.Crate
                : player.PendingLevelUps > 0 ? Phase.LevelUp
                : Phase.Shop;

            void EnterPhase(Phase newPhase)
            {
                phase = newPhase;
                if (phase == Phase.Crate) RollCrate();
                if (phase == Phase.LevelUp) RollChoices();
                if (phase == Phase.Ready) Tween.Alpha(v.readyOverlayImage, 0f, 0.65f, 0.2f);
                Refresh(forceSelectFirst: true);
            }

            void RollCrate()
            {
                crateItem = GameManager.Instance.shopCatalog.RollCrateItem(Wave, player.Stats[StatType.Luck], player.CanOwnMore);
                if (crateItem != null) return;
                // Catalogue vide : on ignore les caisses
                player.PendingCrates = 0;
                phase = NextPhase();
            }

            void RollChoices() => choices = UpgradeTable.Roll(v.upgrades.Length, Wave, player.Stats[StatType.Luck]);

            public void Tick()
            {
                UpdateTooltip();
                UpdateMessage();
                if (PagePressed()) ToggleStatsPage();

                // Retour arrière (Échap / B) : annule "prêt", le choix du coéquipier, ou désélectionne l'arme / l'objet
                if (player.Actions == null || !player.Actions["UI/Cancel"].WasPressedThisFrame()) return;
                if (phase == Phase.Ready) CancelReady();
                else if (choosingTarget)
                {
                    AudioManager.Play(SoundId.UiBack);
                    choosingTarget = false;
                    Refresh(forceSelectFirst: false);
                    SelectGiveButton();
                }
                else if (selectedWeapon != null || selectedItem != null)
                {
                    AudioManager.Play(SoundId.UiBack);
                    selectedWeapon = null;
                    selectedItem = null;
                    Refresh(forceSelectFirst: false);
                }
            }

            // ------------------------------------------------------------ Actions

            void ResolveCrate(bool take)
            {
                if (phase != Phase.Crate || crateItem == null) return;
                if (Remote) NetPlayer.Local.CmdCrate(take, crateItem.name);
                if (take) player.AddItem(crateItem);
                else player.CollectMaterials(GameManager.Instance.shopCatalog.SellValue(crateItem, Wave), giveXp: false);
                AudioManager.Play(take ? SoundId.Buy : SoundId.Sell);
                player.PendingCrates--;
                Punch((take ? v.takeButton : v.recycleButton).transform);
                Punch(v.materials.transform);

                if (player.PendingCrates > 0)
                {
                    RollCrate();
                    Punch(v.crateIcon.transform);
                    Refresh(forceSelectFirst: false);
                }
                else EnterPhase(NextPhase());
            }

            void ChooseUpgrade(int index)
            {
                if (phase != Phase.LevelUp || choices == null || index >= choices.Length) return;
                if (Remote) NetPlayer.Local.CmdUpgrade((int)choices[index].stat, choices[index].value);
                player.ApplyUpgrade(choices[index]);
                player.PendingLevelUps--;
                AudioManager.Play(SoundId.Upgrade);
                Punch(v.upgrades[index].button.transform);

                if (player.PendingLevelUps > 0)
                {
                    RollChoices();
                    Refresh(forceSelectFirst: false);
                }
                else EnterPhase(NextPhase());
            }

            void Buy(int index)
            {
                if (phase != Phase.Shop) return;
                PlayerShop.BuyResult result;
                bool merges = player.Shop.Offers[index].item is WeaponData bought && player.Weapons.WouldMerge(bought);
                if (Remote)
                {
                    // Vérifié ici pour un retour immédiat, appliqué par l'hôte
                    result = player.Shop.CanBuy(index, Wave);
                    if (result == PlayerShop.BuyResult.Ok) NetPlayer.Local.CmdBuy(index);
                }
                else result = player.Shop.TryBuy(index, Wave);
                if (result == PlayerShop.BuyResult.Ok)
                {
                    Punch(v.offers[index].card.transform);
                    Punch(v.materials.transform);
                    ShowMessage(merges ? "<color=#66FF88>Merged!</color>" : "");
                    AudioManager.Play(merges ? SoundId.Merge : SoundId.Buy);
                }
                else
                {
                    Shake(v.offers[index].card.transform);
                    AudioManager.Play(SoundId.BuyFail);
                    ShowMessage(result switch
                    {
                        PlayerShop.BuyResult.NotEnoughMaterials => "Not enough materials!",
                        PlayerShop.BuyResult.InventoryFull => "No room for another weapon (6 max)",
                        PlayerShop.BuyResult.MaxReached => "Maximum reached for this item",
                        _ => "",
                    });
                }
                Refresh(forceSelectFirst: false);
            }

            void ToggleLock(int index)
            {
                if (Remote) NetPlayer.Local.CmdToggleLock(index);
                else player.Shop.ToggleLock(index);
                AudioManager.Play(SoundId.Lock);
                Punch(v.offers[index].lockButton.transform);
                Refresh(forceSelectFirst: false);
            }

            void Reroll()
            {
                bool rerolled;
                if (Remote)
                {
                    rerolled = player.Materials >= player.Shop.RerollPrice(Wave);
                    if (rerolled) NetPlayer.Local.CmdReroll();
                }
                else rerolled = player.Shop.TryReroll(Wave);
                if (rerolled)
                {
                    foreach (var o in v.offers) Punch(o.card.transform);
                    ShowMessage("");
                    AudioManager.Play(SoundId.Reroll);
                }
                else
                {
                    Shake(v.rerollButton.transform);
                    AudioManager.Play(SoundId.BuyFail);
                    ShowMessage("Not enough materials!");
                }
                Refresh(forceSelectFirst: false);
            }

            void SetReady()
            {
                AudioManager.Play(SoundId.UiReady);
                EnterPhase(Phase.Ready);
                // Peut lancer la vague suivante (et fermer la boutique) si tout le monde est prêt
                if (Remote) NetPlayer.Local.CmdReady(true);
                else GameManager.Instance.SetPlayerReady(player, true);
            }

            void CancelReady()
            {
                AudioManager.Play(SoundId.UiBack);
                if (Remote) NetPlayer.Local.CmdReady(false);
                else GameManager.Instance.SetPlayerReady(player, false);
                player.IsReady = false;
                EnterPhase(Phase.Shop);
            }

            void SelectWeapon(int index)
            {
                var list = player.Weapons.Weapons;
                if (index >= list.Count) return;
                selectedWeapon = selectedWeapon == list[index] ? null : list[index];
                selectedItem = null;
                choosingTarget = false;
                AudioManager.Play(selectedWeapon != null ? SoundId.UiConfirm : SoundId.UiBack);
                Refresh(forceSelectFirst: false);
            }

            /// <summary>Clic sur un objet possédé : le sélectionne pour pouvoir le donner (coop seulement).</summary>
            void SelectItem(int index)
            {
                if (phase != Phase.Shop || !CanGive || index >= ownedItems.Count) return;
                var item = ownedItems[index].item;
                selectedItem = selectedItem == item ? null : item;
                selectedWeapon = null;
                choosingTarget = false;
                AudioManager.Play(selectedItem != null ? SoundId.UiConfirm : SoundId.UiBack);
                Refresh(forceSelectFirst: false);
            }

            // ------------------------------------------------------------ Don à un coéquipier

            void Give()
            {
                if (!CanGive || (selectedWeapon == null && selectedItem == null)) return;
                // À deux, pas de choix : on donne directement à l'autre joueur
                if (teammates.Count == 1)
                {
                    GiveTo(teammates[0]);
                    return;
                }
                choosingTarget = true;
                AudioManager.Play(SoundId.UiConfirm);
                Refresh(forceSelectFirst: false);
                player.EventSystem.SetSelectedGameObject(v.giveTargets[0].gameObject);
            }

            void GiveTo(Player to)
            {
                if (phase != Phase.Shop) return;
                string what = selectedWeapon != null ? selectedWeapon.Data.displayName : selectedItem != null ? selectedItem.displayName : "";
                PlayerShop.GiveResult result;
                if (selectedWeapon != null)
                {
                    if (Remote)
                    {
                        result = player.Shop.CanGiveWeapon(selectedWeapon, to);
                        if (result == PlayerShop.GiveResult.Ok) NetPlayer.Local.CmdGiveWeapon(IndexOf(selectedWeapon), to.Index);
                    }
                    else result = player.Shop.TryGiveWeapon(selectedWeapon, to);
                }
                else if (selectedItem != null)
                {
                    if (Remote)
                    {
                        result = player.Shop.CanGiveItem(selectedItem, to);
                        if (result == PlayerShop.GiveResult.Ok) NetPlayer.Local.CmdGiveItem(selectedItem.name, to.Index);
                    }
                    else result = player.Shop.TryGiveItem(selectedItem, to);
                }
                else return;

                choosingTarget = false;
                if (result == PlayerShop.GiveResult.Ok)
                {
                    selectedWeapon = null;
                    // On garde l'objet sélectionné s'il en reste un exemplaire (pour en donner plusieurs d'affilée)
                    if (selectedItem != null && (Remote || player.ItemCount(selectedItem) == 0)) selectedItem = null;
                    AudioManager.Play(SoundId.Sell);
                    ShowMessage($"<color=#66FF88>{what} given to P{to.Index + 1}!</color>");
                }
                else
                {
                    Shake(v.giveButton.transform);
                    AudioManager.Play(SoundId.BuyFail);
                    ShowMessage(result switch
                    {
                        PlayerShop.GiveResult.LastWeapon => "You must keep at least one weapon",
                        PlayerShop.GiveResult.NoRoom => $"P{to.Index + 1} has no room for another weapon",
                        PlayerShop.GiveResult.MaxReached => $"P{to.Index + 1} already has the maximum of this item",
                        _ => "",
                    });
                }
                Refresh(forceSelectFirst: false);
                SelectGiveButton();
            }

            /// <summary>Coop locale : un coéquipier vient de nous donner quelque chose.</summary>
            public void OnReceived(Player from, ShopItemData data)
            {
                AudioManager.Play(SoundId.Buy);
                ShowMessage($"<color=#66FF88>Got {data.displayName} from P{from.Index + 1}!</color>");
                Refresh(forceSelectFirst: false);
                Punch(data is WeaponData ? v.weaponsLabel.transform : v.itemsLabel.transform);
            }

            /// <summary>Garde la sélection manette sur la rangée d'actions (ou sur les armes si elle a disparu).</summary>
            void SelectGiveButton()
            {
                var es = player.EventSystem;
                if (v.giveButton != null && v.giveButton.gameObject.activeInHierarchy) es.SetSelectedGameObject(v.giveButton.gameObject);
                else if (selectedItem == null && player.Weapons.Weapons.Count > 0) es.SetSelectedGameObject(v.weaponSlots[0].gameObject);
            }

            void RefreshTeammates()
            {
                teammates.Clear();
                foreach (var p in Player.All)
                    if (p != player && p.Character != null) teammates.Add(p);
                teammates.Sort((a, b) => a.Index.CompareTo(b.Index));
            }

            void SellSelected()
            {
                bool sold;
                if (Remote)
                {
                    sold = selectedWeapon != null && player.Weapons.Weapons.Count > 1;
                    if (sold) NetPlayer.Local.CmdSell(IndexOf(selectedWeapon));
                }
                else sold = player.Shop.TrySell(selectedWeapon, Wave);
                if (!sold)
                {
                    Shake(v.sellButton.transform);
                    AudioManager.Play(SoundId.BuyFail);
                    ShowMessage("You must keep at least one weapon");
                    return;
                }
                selectedWeapon = null;
                Punch(v.materials.transform);
                AudioManager.Play(SoundId.Sell);
                Refresh(forceSelectFirst: false);
                player.EventSystem.SetSelectedGameObject(v.weaponSlots[0].gameObject);
            }

            void MergeSelected()
            {
                int i = IndexOf(selectedWeapon);
                if (Remote)
                {
                    if (player.Weapons.FindMergePartner(selectedWeapon) == null) return;
                    NetPlayer.Local.CmdMerge(i);
                }
                else if (!player.Weapons.Merge(selectedWeapon)) return;
                AudioManager.Play(SoundId.Merge);
                if (i >= 0) Punch(v.weaponSlots[i].transform);
                Refresh(forceSelectFirst: false);
            }

            // ------------------------------------------------------------ Affichage

            void Refresh(bool forceSelectFirst)
            {
                bool crate = phase == Phase.Crate;
                bool levelUp = phase == Phase.LevelUp;
                bool shop = phase == Phase.Shop;
                // La page Stats remplace la caisse, les améliorations ou les offres (pas l'inventaire)
                bool stats = statsPage && phase != Phase.Ready && v.statsPage != null;
                v.crateGroup.SetActive(crate && !stats);
                v.levelUpGroup.SetActive(levelUp && !stats);
                v.shopGroup.SetActive(!levelUp && !crate && !stats);
                if (v.statsPage != null) v.statsPage.SetActive(stats);
                v.readyOverlay.SetActive(phase == Phase.Ready);

                v.title.text = $"P{player.Index + 1} - {player.Character.displayName}  <size=70%><color=#FFFFFF>Lv. {player.Level}</color></size>";
                v.materials.text = "Materials: " + player.Materials;
                RefreshTeammates();

                if (crate) RefreshCrate();
                if (levelUp) RefreshLevelUp();
                RefreshOffers();
                RefreshInventory();
                RefreshStats();
                RefreshTabs();

                // Navigation : seulement les boutons de la phase en cours
                var rows = new List<List<Selectable>>();
                if (crate)
                {
                    if (!stats) rows.Add(new List<Selectable> { v.takeButton, v.recycleButton });
                }
                else if (levelUp)
                {
                    if (!stats)
                        foreach (var u in v.upgrades)
                            if (u.button.gameObject.activeSelf) rows.Add(new List<Selectable> { u.button });
                }
                else if (shop)
                {
                    if (!stats)
                    {
                        foreach (var o in v.offers) rows.Add(new List<Selectable> { o.card, o.lockButton });
                        rows.Add(new List<Selectable> { v.rerollButton, v.readyButton });
                    }
                    var slotRow = new List<Selectable>();
                    for (int i = 0; i < player.Weapons.Weapons.Count && i < v.weaponSlots.Length; i++) slotRow.Add(v.weaponSlots[i]);
                    rows.Add(slotRow);
                    var actionRow = new List<Selectable>();
                    if (v.sellButton.gameObject.activeSelf) actionRow.Add(v.sellButton);
                    if (v.mergeButton.gameObject.activeSelf) actionRow.Add(v.mergeButton);
                    if (v.giveButton != null && v.giveButton.gameObject.activeSelf) actionRow.Add(v.giveButton);
                    if (v.giveTargets != null)
                        foreach (var t in v.giveTargets)
                            if (t.gameObject.activeSelf) actionRow.Add(t);
                    rows.Add(actionRow);
                    // Objets possédés : sélectionnables pour lire leur infobulle à la manette
                    var itemRow = new List<Selectable>();
                    foreach (var icon in v.itemIcons)
                        if (icon.gameObject.activeSelf && icon.TryGetComponent<Selectable>(out var s)) itemRow.Add(s);
                    rows.Add(itemRow);
                }
                else rows.Add(new List<Selectable> { v.unreadyButton });
                rows.RemoveAll(r => r.Count == 0);
                UIFactory.LinkNavigation(rows);

                // Garde une sélection valide pour la manette (rien à sélectionner sur la page Stats d'une caisse / d'un level up)
                var es = player.EventSystem;
                var current = es.currentSelectedGameObject;
                bool valid = current != null && current.activeInHierarchy && IsInRows(rows, current);
                if (forceSelectFirst || !valid) es.SetSelectedGameObject(rows.Count > 0 ? rows[0][0].gameObject : null);

                GameManager.Instance.SaveRun();
            }

            void RefreshCrate()
            {
                v.crateTitle.text = player.PendingCrates > 1 ? $"CRATE! <size=70%>(x{player.PendingCrates})</size>" : "CRATE!";
                if (crateItem == null) return;
                var tierColor = ShopItemData.TierColor(crateItem.tier);
                v.crateStripe.color = tierColor;
                v.crateIcon.sprite = crateItem.Icon;
                v.crateName.text = crateItem.displayName;
                v.crateName.color = tierColor;
                v.crateDesc.text = crateItem.Description;
                v.recycleLabel.text = $"Recycle (+{GameManager.Instance.shopCatalog.SellValue(crateItem, Wave)})";
            }

            void RefreshLevelUp()
            {
                v.levelUpTitle.text = player.PendingLevelUps > 1
                    ? $"LEVEL UP! <size=70%>(x{player.PendingLevelUps})</size>"
                    : "LEVEL UP!";
                for (int i = 0; i < v.upgrades.Length; i++)
                {
                    var u = v.upgrades[i];
                    bool has = choices != null && i < choices.Length;
                    u.button.gameObject.SetActive(has);
                    if (!has) continue;
                    var c = choices[i];
                    var tierColor = ShopItemData.TierColor(c.tier);
                    u.stripe.color = tierColor;
                    u.tier.text = "Tier " + ShopItemData.TierRoman(c.tier);
                    u.tier.color = tierColor;
                    u.text.text = StatInfo.FormatModifier(c.stat, c.value);
                }
            }

            void RefreshOffers()
            {
                var shop = player.Shop;
                int rerollPrice = shop.RerollPrice(Wave);
                v.rerollLabel.text = $"Reroll ({rerollPrice})";
                v.rerollLabel.color = player.Materials >= rerollPrice ? Color.white : new Color(1f, 0.45f, 0.45f);

                for (int i = 0; i < v.offers.Length && i < shop.Offers.Length; i++)
                {
                    var view = v.offers[i];
                    var o = shop.Offers[i];
                    bool visible = o.item != null && !o.sold;
                    view.icon.enabled = view.name.enabled = view.desc.enabled = view.price.enabled = visible;
                    view.sold.enabled = o.sold;
                    view.lockIcon.enabled = visible;
                    view.stripe.color = visible ? ShopItemData.TierColor(o.item.tier) : Color.clear;
                    if (!visible) continue;

                    view.icon.sprite = o.item.Icon;
                    string kind = o.item is WeaponData w
                        ? player.Weapons.WouldMerge(w) ? $"<color=#66FF88>Merge > tier {ShopItemData.TierRoman(w.nextTier.tier)}</color>" : "Weapon"
                        : "Item";
                    view.name.text = $"{o.item.displayName} <size=70%><color=#AAAAAA>{kind}</color></size>";
                    view.name.color = ShopItemData.TierColor(o.item.tier);
                    view.desc.text = o.item.Description;
                    int price = shop.Price(o, Wave);
                    view.price.text = price.ToString();
                    view.price.color = player.Materials >= price ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.4f, 0.4f);
                    view.lockIcon.sprite = GameManager.Instance.shopCatalog.lockIcon;
                    view.lockIcon.color = o.locked ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                }
            }

            void RefreshInventory()
            {
                var weapons = player.Weapons.Weapons;
                if (selectedWeapon != null && IndexOf(selectedWeapon) < 0) selectedWeapon = null;

                v.weaponsLabel.text = $"Weapons ({weapons.Count}/{WeaponHolder.MaxWeapons})" +
                                      (selectedWeapon != null ? $"  <color=#FFD966>{selectedWeapon.Data.displayName}</color>" : "");
                for (int i = 0; i < v.weaponSlots.Length; i++)
                {
                    bool has = i < weapons.Count;
                    v.weaponSlots[i].interactable = has;
                    v.weaponIcons[i].enabled = has;
                    v.weaponTiers[i].text = has ? ShopItemData.TierRoman(weapons[i].Data.tier) : "";
                    if (!has) continue;
                    v.weaponIcons[i].sprite = weapons[i].Data.Icon;
                    v.weaponTiers[i].color = ShopItemData.TierColor(weapons[i].Data.tier);
                    v.weaponIcons[i].color = weapons[i] == selectedWeapon ? Color.white : new Color(1f, 1f, 1f, 0.85f);
                }

                if (selectedItem != null && player.ItemCount(selectedItem) == 0) selectedItem = null;
                if (phase != Phase.Shop || (selectedWeapon == null && selectedItem == null) || !CanGive) choosingTarget = false;

                bool showActions = selectedWeapon != null && phase == Phase.Shop && !choosingTarget;
                v.sellButton.gameObject.SetActive(showActions);
                v.mergeButton.gameObject.SetActive(showActions && player.Weapons.FindMergePartner(selectedWeapon) != null);
                if (showActions) v.sellLabel.text = $"Sell (+{player.Shop.SellValue(selectedWeapon, Wave)})";
                RefreshGive();

                // Objets : une icône par type d'objet, avec "x6" s'il est possédé plusieurs fois
                var items = player.Items;
                ownedItems.Clear();
                foreach (var item in items)
                {
                    int index = ownedItems.FindIndex(o => o.item == item);
                    if (index >= 0) ownedItems[index] = (item, ownedItems[index].count + 1);
                    else ownedItems.Add((item, 1));
                }
                v.itemsLabel.text = $"Items ({items.Count})" +
                                    (selectedItem != null ? $"  <color=#FFD966>{selectedItem.displayName}</color>" : "");
                for (int i = 0; i < v.itemIcons.Length; i++)
                {
                    bool has = i < ownedItems.Count;
                    v.itemIcons[i].gameObject.SetActive(has);
                    if (has) v.itemIcons[i].sprite = ownedItems[i].item.Icon;
                    if (has) v.itemIcons[i].color = selectedItem == null || ownedItems[i].item == selectedItem ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    if (v.itemCounts != null && i < v.itemCounts.Length && v.itemCounts[i] != null)
                        v.itemCounts[i].text = has && ownedItems[i].count > 1 ? "x" + ownedItems[i].count : "";
                }
            }

            /// <summary>Bouton « Give » (arme ou objet sélectionné), ou boutons « P2 / P3 / P4 » pendant le choix du coéquipier.</summary>
            void RefreshGive()
            {
                if (v.giveButton == null) return;
                bool hasSelection = (selectedWeapon != null || selectedItem != null) && phase == Phase.Shop && CanGive;
                v.giveButton.gameObject.SetActive(hasSelection && !choosingTarget);
                if (v.giveLabel != null) v.giveLabel.text = teammates.Count == 1 ? $"Give > P{teammates[0].Index + 1}" : "Give";

                if (v.giveTargets == null) return;
                for (int i = 0; i < v.giveTargets.Length; i++)
                {
                    bool show = hasSelection && choosingTarget && i < teammates.Count;
                    v.giveTargets[i].gameObject.SetActive(show);
                    if (!show) continue;
                    var mate = teammates[i];
                    UIFactory.SetAccent(v.giveTargets[i], mate.Color);
                    if (v.giveTargetLabels != null && i < v.giveTargetLabels.Length)
                    {
                        v.giveTargetLabels[i].text = $"P{mate.Index + 1}";
                        v.giveTargetLabels[i].color = mate.Color;
                    }
                }
            }

            /// <summary>Détail de l'arme ou de l'objet survolé (souris) ou sélectionné (manette / clavier).</summary>
            void UpdateTooltip()
            {
                if (v.tooltipRoot == null) return;
                GameObject focus = null;
                var hovered = TooltipSource.Hovered;
                if (hovered != null && hovered.transform.IsChildOf(v.transform)) focus = hovered.gameObject;
                else if (player.EventSystem != null) focus = player.EventSystem.currentSelectedGameObject;

                ShopItemData data = null;
                if (focus != null)
                {
                    for (int i = 0; i < v.weaponSlots.Length && data == null; i++)
                        if (v.weaponSlots[i].gameObject == focus && i < player.Weapons.Weapons.Count) data = player.Weapons.Weapons[i].Data;
                    for (int i = 0; i < v.itemIcons.Length && data == null; i++)
                        if (v.itemIcons[i].gameObject == focus && i < ownedItems.Count) data = ownedItems[i].item;
                }

                bool show = data != null && phase != Phase.Ready;
                if (v.tooltipRoot.activeSelf != show) v.tooltipRoot.SetActive(show);
                if (!show) return;
                string kind = data is WeaponData ? "Weapon" : "Item";
                v.tooltipTitle.text = $"{data.displayName} <size=70%><color=#AAAAAA>{kind} - Tier {ShopItemData.TierRoman(data.tier)}</color></size>";
                v.tooltipTitle.color = ShopItemData.TierColor(data.tier);
                v.tooltipText.text = data.Description;
            }

            // ------------------------------------------------------------ Page Stats

            bool PagePressed()
            {
                if (phase == Phase.Ready || v.statsPage == null || player.Actions == null) return false;
                var prev = player.Actions.FindAction("UI/PrevPage");
                var next = player.Actions.FindAction("UI/NextPage");
                return (prev != null && prev.WasPressedThisFrame()) || (next != null && next.WasPressedThisFrame());
            }

            /// <summary>Deux pages seulement : Q et E (LB et RB) passent toutes les deux à l'autre.</summary>
            void ToggleStatsPage()
            {
                statsPage = !statsPage;
                AudioManager.Play(SoundId.UiSelect);
                choosingTarget = false;
                Refresh(forceSelectFirst: true);
                if (v.statsPage != null && statsPage) Punch(v.statsPage.transform);
            }

            /// <summary>« Q &lt;  SHOP   STATS  &gt; E » (LB / RB à la manette), l'onglet actif à la couleur du joueur.</summary>
            void RefreshTabs()
            {
                if (v.tabBar == null) return;
                v.tabBar.gameObject.SetActive(phase != Phase.Ready);
                bool keyboard = player.UsesKeyboard;
                string prev = keyboard ? "Q" : "LB";
                string next = keyboard ? "E" : "RB";
                string first = phase switch
                {
                    Phase.Crate => "CRATE",
                    Phase.LevelUp => "LEVEL UP",
                    _ => "SHOP",
                };
                string hex = ColorUtility.ToHtmlStringRGB(player.Color);
                string Tab(string label, bool on) => on ? $"<color=#{hex}><u>{label}</u></color>" : $"<color=#666666>{label}</color>";
                v.tabBar.text = $"<color=#AAAAAA>{prev} <noparse><</noparse></color>    {Tab(first, !statsPage)}     {Tab("STATS", statsPage)}    <color=#AAAAAA><noparse>></noparse> {next}</color>";
            }

            static readonly (string title, StatType[] stats)[] StatSections =
            {
                ("SURVIVAL", new[] { StatType.MaxHealth, StatType.HealthRegen, StatType.LifeSteal, StatType.Armor, StatType.Dodge, StatType.MoveSpeed }),
                ("OFFENSE", new[] { StatType.Damage, StatType.MeleeDamage, StatType.RangedDamage, StatType.Engineering,
                    StatType.AttackSpeed, StatType.CritChance, StatType.Range }),
                ("UTILITY", new[] { StatType.Luck, StatType.Harvesting, StatType.PickupRange }),
            };

            /// <summary>Toutes les stats, par catégorie : nom, valeur (verte / rouge), courte explication en gris.</summary>
            void RefreshStatsPage()
            {
                if (v.statsPageText == null || !statsPage) return;
                var s = player.Stats;
                var sb = new System.Text.StringBuilder("<line-height=27>");
                foreach (var (title, stats) in StatSections)
                {
                    sb.Append($"<size=80%><color=#FFD966>{title}</color></size>\n");
                    foreach (var t in stats)
                    {
                        float value = s[t];
                        string col = t == StatType.MaxHealth ? "#FFFFFF" : value > 0 ? "#7CFF7C" : value < 0 ? "#FF6B6B" : "#DDDDDD";
                        sb.Append($"{StatInfo.Name(t)}<pos=44%><color={col}>{StatInfo.FormatValue(t, value)}</color>")
                          .Append($"<pos=58%><size=65%><color=#8A8A8A>{StatInfo.Description(t)}</color></size>\n");
                    }
                }
                v.statsPageText.text = sb.ToString();
            }

            void RefreshStats()
            {
                RefreshStatsPage();
                var s = player.Stats;
                string Line(StatType t)
                {
                    float value = s[t];
                    string col = value > 0 ? "#7CFF7C" : value < 0 ? "#FF6B6B" : "#DDDDDD";
                    return $"{StatInfo.Name(t)} <color={col}>{StatInfo.FormatValue(t, value)}</color>";
                }

                v.statsLeft.text = string.Join("\n", Line(StatType.MaxHealth), Line(StatType.HealthRegen), Line(StatType.Armor),
                    Line(StatType.Dodge), Line(StatType.MoveSpeed), Line(StatType.Luck));
                v.statsRight.text = string.Join("\n", Line(StatType.Damage), Line(StatType.AttackSpeed), Line(StatType.CritChance),
                    Line(StatType.Range), Line(StatType.PickupRange));
            }

            /// <summary>Message sous les offres : reste 1 s, puis s'efface en 1,2 s (géré dans Tick, temps réel).</summary>
            void ShowMessage(string text)
            {
                Tween.StopAll(v.message);
                v.message.text = text;
                v.message.alpha = 1f;
                messageTime = Time.unscaledTime;
            }

            void UpdateMessage()
            {
                if (string.IsNullOrEmpty(v.message.text)) return;
                float t = Time.unscaledTime - messageTime - MessageHold;
                if (t <= 0f) return;
                float alpha = 1f - t / MessageFade;
                if (alpha > 0f)
                {
                    v.message.alpha = alpha * alpha;
                    return;
                }
                v.message.text = "";
                v.message.alpha = 1f;
            }

            int IndexOf(Weapon w)
            {
                var list = player.Weapons.Weapons;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] == w) return i;
                return -1;
            }

            static bool IsInRows(List<List<Selectable>> rows, GameObject go)
            {
                foreach (var row in rows)
                foreach (var s in row)
                    if (s.gameObject == go) return true;
                return false;
            }

            static void Punch(Transform t)
            {
                Tween.CompleteAll(t);
                t.localScale = Vector3.one;
                Tween.PunchScale(t, new Vector3(0.08f, 0.08f, 0f), 0.25f);
            }

            static void Shake(Transform t)
            {
                Tween.CompleteAll(t);
                Tween.ShakeLocalPosition(t, new Vector3(12f, 0f, 0f), 0.3f);
            }
        }
    }
}
