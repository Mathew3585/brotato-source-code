using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Mode en ligne : prefabs réseau (joueur du lobby, synchronisation de partie, personnage en ligne),
    /// NetworkManager (Steam + LAN) dans la scène MainMenu, et UI "Play online". N'écrase rien d'existant.
    /// Le panneau en ligne n'a plus de boutons LAN (retirés des scènes existantes) ; le lobby a un bouton « Invite friends ».
    /// </summary>
    static class NetworkBuilder
    {
        const string NetDir = "Assets/GAME/Prefabs/Network";
        const string InputPath = "Assets/GAME/Input/GameControls.inputactions";
        const string ButtonPath = "Assets/GAME/Prefabs/UI/UIButton.prefab";
        const float PanelHeight = 540f;
        static readonly Color Accent = UIFactory.Accent;

        public class Prefabs
        {
            public NetworkObject lobbyPlayer, game, avatar;
            public SinglePrefabObjects list;
        }

        // ================================================================ Prefabs

        public static Prefabs BuildPrefabs(Dictionary<string, Sprite> props)
        {
            if (!AssetDatabase.IsValidFolder(NetDir)) AssetDatabase.CreateFolder("Assets/GAME/Prefabs", "Network");
            var p = new Prefabs();

            p.lobbyPlayer = Prefab("NetLobbyPlayer", go =>
            {
                var nob = go.AddComponent<NetworkObject>();
                go.AddComponent<NetLobbyPlayer>();
                // Global : survit au passage du menu à la partie
                var so = new SerializedObject(nob);
                so.FindProperty("_isGlobal").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            p.game = Prefab("NetGame", go =>
            {
                go.AddComponent<NetworkObject>();
                go.AddComponent<NetGame>();
            });

            p.avatar = Prefab("NetPlayer", go =>
            {
                var weaponPrefab = AssetDatabase.LoadAssetAtPath<Weapon>("Assets/GAME/Prefabs/Weapon.prefab");
                var shadow = Sprite(go.transform, "Shadow", props["Shadow"], -900, new Vector2(0f, -0.5f));
                var body = Sprite(go.transform, "Body", null, 0, Vector2.zero);
                var weaponsGo = new GameObject("Weapons");
                weaponsGo.transform.SetParent(go.transform, false);
                var holder = weaponsGo.AddComponent<WeaponHolder>();
                Set(holder, "weaponPrefab", weaponPrefab);

                go.AddComponent<NetworkObject>();
                var player = go.AddComponent<Player>();
                Set(player, "body", body);
                Set(player, "shadow", shadow);
                Set(player, "weapons", holder);

                // Position envoyée par le propriétaire (déplacement réactif), seulement la position
                var nt = go.AddComponent<NetworkTransform>();
                var ntSo = new SerializedObject(nt);
                ntSo.FindProperty("_clientAuthoritative").boolValue = true;
                ntSo.FindProperty("_synchronizeRotation").boolValue = false;
                ntSo.FindProperty("_synchronizeScale").boolValue = false;
                ntSo.ApplyModifiedPropertiesWithoutUndo();

                var net = go.AddComponent<NetPlayer>();
                Set(net, "player", player);
                Set(net, "controls", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath));
            });

            // Liste des objets réseau (identique chez l'hôte et les clients)
            const string listPath = NetDir + "/NetworkPrefabs.asset";
            p.list = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(listPath);
            if (p.list == null)
            {
                p.list = ScriptableObject.CreateInstance<SinglePrefabObjects>();
                AssetDatabase.CreateAsset(p.list, listPath);
            }
            foreach (var nob in new[] { p.lobbyPlayer, p.game, p.avatar })
            {
                bool present = false;
                foreach (var existing in p.list.Prefabs) present |= existing == nob;
                if (!present) p.list.AddObject(nob, true, false);
            }
            EditorUtility.SetDirty(p.list);
            return p;
        }

        static NetworkObject Prefab(string name, System.Action<GameObject> build)
        {
            string path = $"{NetDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
            if (existing != null) return existing;
            var go = new GameObject(name);
            build(go);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<NetworkObject>();
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order, Vector2 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ Scène MainMenu

        /// <summary>Ajoute le NetworkManager et l'UI en ligne dans la scène MainMenu ouverte si absents.</summary>
        public static bool PatchMenuScene(Prefabs prefabs)
        {
            bool changed = false;
            if (Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include) == null)
            {
                BuildNetworkManager(prefabs);
                changed = true;
            }

            var menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
            if (menu != null && menu.GetComponent<OnlineMenu>() == null)
            {
                BuildOnlineUI(menu);
                changed = true;
            }
            var om = menu != null ? menu.GetComponent<OnlineMenu>() : null;
            if (om != null) changed |= PatchOnlineUI(om);
            return changed;
        }

        /// <summary>
        /// Scène existante : retire les boutons LAN (on joue soit en local, soit en ligne via Steam), resserre le panneau,
        /// et ajoute le bouton « Invite friends » (overlay Steam) en bas à droite du lobby. N'ajoute que ce qui manque.
        /// </summary>
        static bool PatchOnlineUI(OnlineMenu om)
        {
            var so = new SerializedObject(om);
            var onlinePanel = (GameObject)so.FindProperty("onlinePanel").objectReferenceValue;
            var lobbyPanel = (GameObject)so.FindProperty("lobbyPanel").objectReferenceValue;
            if (onlinePanel == null || lobbyPanel == null) return false;
            bool changed = false;

            var panel = (RectTransform)onlinePanel.transform.Find("Panel");
            if (panel != null)
            {
                bool removed = false;
                foreach (var name in new[] { "LanLabel", "HostLan", "JoinLan" })
                {
                    var t = panel.Find(name);
                    if (t == null) continue;
                    Object.DestroyImmediate(t.gameObject);
                    removed = true;
                }
                if (removed)
                {
                    // Statut et « Back » remontent à la place des boutons LAN
                    panel.sizeDelta = new Vector2(panel.sizeDelta.x, PanelHeight);
                    if (so.FindProperty("status").objectReferenceValue is TextMeshProUGUI status)
                        ((RectTransform)status.transform).anchoredPosition = new Vector2(50f, -340f);
                    if (so.FindProperty("backButton").objectReferenceValue is Button back)
                    {
                        var rt = (RectTransform)back.transform;
                        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -450f);
                    }
                    if (so.FindProperty("codeInput").objectReferenceValue is TMP_InputField input && input.placeholder is TMP_Text hint)
                    {
                        hint.text = "Room code...";
                        EditorUtility.SetDirty(hint);
                    }
                    changed = true;
                }
            }

            var invite = so.FindProperty("inviteButton");
            if (invite.objectReferenceValue == null)
            {
                var host = so.FindProperty("hostButton").objectReferenceValue as Button;
                invite.objectReferenceValue = InviteButton(lobbyPanel.transform, host);
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }

        /// <summary>Bouton « Invite friends » du lobby en ligne : instance du prefab UIButton (repli : bouton UIFactory).</summary>
        static Button InviteButton(Transform lobbyPanel, Button styleFrom)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            Button button;
            TextMeshProUGUI label;
            if (asset != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, lobbyPanel);
                go.name = "Invite";
                button = go.GetComponent<Button>();
                label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            else
            {
                button = UIFactory.Button(lobbyPanel, "Invite", 0, 0, 1, 1, Accent);
                button.gameObject.AddComponent<ButtonSound>();
                label = UIFactory.ButtonLabel(button, 28);
            }
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-30f, 25f);
            rt.sizeDelta = new Vector2(380f, 64f);

            var reference = styleFrom != null ? styleFrom.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (reference != null)
            {
                label.font = reference.font;
                label.fontSharedMaterial = reference.fontSharedMaterial;
                label.color = reference.color;
            }
            label.fontSize = 28f;
            label.text = "Invite friends <size=75%><color=#BBBBBB>(I / X)</color></size>";
            button.gameObject.SetActive(false);
            return button;
        }

        static void BuildNetworkManager(Prefabs prefabs)
        {
            var go = new GameObject("Network");
            go.AddComponent<SteamBootstrap>();
            var nm = go.AddComponent<NetworkManager>();
            var tm = go.AddComponent<TransportManager>();
            var tugboat = go.AddComponent<Tugboat>();
            var steam = go.AddComponent<FishySteamworks.FishySteamworks>();
            var multipass = go.AddComponent<Multipass>();

            var mpSo = new SerializedObject(multipass);
            var list = mpSo.FindProperty("_transports");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = tugboat; // index 0 = LAN
            list.GetArrayElementAtIndex(1).objectReferenceValue = steam;   // index 1 = Steam
            mpSo.ApplyModifiedPropertiesWithoutUndo();
            tm.Transport = multipass;

            var steamSo = new SerializedObject(steam);
            steamSo.FindProperty("_peerToPeer").boolValue = true;
            steamSo.FindProperty("_maximumClients").intValue = OnlineManager.MaxPlayers;
            steamSo.ApplyModifiedPropertiesWithoutUndo();
            var tugSo = new SerializedObject(tugboat);
            tugSo.FindProperty("_maximumClients").intValue = OnlineManager.MaxPlayers;
            tugSo.ApplyModifiedPropertiesWithoutUndo();

            nm.SpawnablePrefabs = prefabs.list;

            var online = go.AddComponent<OnlineManager>();
            var so = new SerializedObject(online);
            so.FindProperty("lobbyPlayerPrefab").objectReferenceValue = prefabs.lobbyPlayer;
            so.FindProperty("netGamePrefab").objectReferenceValue = prefabs.game;
            so.FindProperty("playerAvatarPrefab").objectReferenceValue = prefabs.avatar;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(nm);
            EditorUtility.SetDirty(tm);
        }

        static void BuildOnlineUI(MainMenu menu)
        {
            var menuSo = new SerializedObject(menu);
            var menuPanel = (GameObject)menuSo.FindProperty("menuPanel").objectReferenceValue;
            var lobbyPanel = (GameObject)menuSo.FindProperty("lobbyPanel").objectReferenceValue;
            var newGame = (Button)menuSo.FindProperty("newGameButton").objectReferenceValue;
            var optionsBtn = (Button)menuSo.FindProperty("optionsButton").objectReferenceValue;
            var quit = (Button)menuSo.FindProperty("quitButton").objectReferenceValue;
            var cont = (Button)menuSo.FindProperty("continueButton").objectReferenceValue;
            var canvas = menuPanel.transform.parent;

            // ---- Bouton "Play online" sous "New game" : Continue / New game / Play online / Options / Quit
            var column = (RectTransform)newGame.transform.parent;
            column.sizeDelta = new Vector2(440f, 490f);
            column.anchoredPosition = new Vector2(column.anchoredPosition.x, -290f);
            if (cont != null) ((RectTransform)cont.transform).anchoredPosition = new Vector2(0f, 0f);
            ((RectTransform)newGame.transform).anchoredPosition = new Vector2(0f, -100f);
            var online = UIFactory.Button(column, "PlayOnline", 0, 200, 440, 84, Accent);
            UIFactory.ButtonLabel(online, 38).text = "Play online";
            online.gameObject.AddComponent<ButtonSound>();
            if (optionsBtn != null) ((RectTransform)optionsBtn.transform).anchoredPosition = new Vector2(0f, -300f);
            ((RectTransform)quit.transform).anchoredPosition = new Vector2(0f, -400f);
            menuSo.FindProperty("onlineButton").objectReferenceValue = online;
            menuSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Panneau "Play online"
            var panelRoot = UIFactory.Stretch(canvas, "Online");
            const float w = 860f, h = PanelHeight;
            var panel = UIFactory.Rect(panelRoot, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            UIFactory.AddImage(panel, UIFactory.PanelColor);
            UIFactory.AddImage(UIFactory.TopLeft(panel, "Bar", 0, 0, w, 8), Accent);
            UIFactory.Text(panel, "Title", 0, 28, w, 70, 60, TextAlignmentOptions.Top).text = "PLAY ONLINE";

            UIFactory.Text(panel, "SteamLabel", 50, 110, w - 100, 40, 28).text = "With friends (Steam)";
            var host = UIFactory.Button(panel, "Host", 50, 155, w - 100, 70, Accent);
            UIFactory.ButtonLabel(host, 32).text = "Host a room";
            var input = InputField(panel, "Code", 50, 245, 440, 70, "Room code...");
            var join = UIFactory.Button(panel, "Join", 510, 245, w - 560, 70, Accent);
            UIFactory.ButtonLabel(join, 32).text = "Join";

            var status = UIFactory.Text(panel, "Status", 50, 340, w - 100, 90, 24, TextAlignmentOptions.Top, wrap: true);
            var back = UIFactory.Button(panel, "Back", w / 2f - 150f, 450, 300, 64, Accent);
            UIFactory.ButtonLabel(back, 30).text = "Back";
            foreach (var s in new Selectable[] { host, join, back, input }) s.gameObject.AddComponent<ButtonSound>();
            panelRoot.gameObject.SetActive(false);

            // ---- Code de la room affiché en haut du lobby
            var roomCode = UIFactory.Text(UIFactory.Rect(lobbyPanel.transform, "RoomCode", new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1600f, 50f)),
                34, TextAlignmentOptions.Center);
            roomCode.text = "ROOM CODE: ABC123";

            // ---- Composant
            var om = menu.gameObject.AddComponent<OnlineMenu>();
            var so = new SerializedObject(om);
            so.FindProperty("mainMenu").objectReferenceValue = menu;
            so.FindProperty("menuPanel").objectReferenceValue = menuPanel;
            so.FindProperty("onlineButton").objectReferenceValue = online;
            so.FindProperty("onlinePanel").objectReferenceValue = panelRoot.gameObject;
            so.FindProperty("hostButton").objectReferenceValue = host;
            so.FindProperty("codeInput").objectReferenceValue = input;
            so.FindProperty("joinButton").objectReferenceValue = join;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("status").objectReferenceValue = status;
            so.FindProperty("lobbyPanel").objectReferenceValue = lobbyPanel;
            var srcSlots = menuSo.FindProperty("slots");
            var dstSlots = so.FindProperty("slots");
            dstSlots.arraySize = srcSlots.arraySize;
            for (int i = 0; i < srcSlots.arraySize; i++)
                dstSlots.GetArrayElementAtIndex(i).objectReferenceValue = srcSlots.GetArrayElementAtIndex(i).objectReferenceValue;
            so.FindProperty("footer").objectReferenceValue = menuSo.FindProperty("footer").objectReferenceValue;
            so.FindProperty("roomCode").objectReferenceValue = roomCode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Champ de saisie TextMeshPro (fond, zone de texte masquée, texte d'aide).</summary>
        static TMP_InputField InputField(Transform parent, string name, float x, float y, float width, float height, string placeholder)
        {
            var rt = UIFactory.TopLeft(parent, name, x, y, width, height);
            var bg = UIFactory.AddImage(rt, Color.white, raycast: true);

            var area = UIFactory.Stretch(rt, "Text Area");
            area.offsetMin = new Vector2(16f, 6f);
            area.offsetMax = new Vector2(-16f, -6f);
            area.gameObject.AddComponent<RectMask2D>();

            var hint = UIFactory.Text(UIFactory.Stretch(area, "Placeholder"), height * 0.45f, TextAlignmentOptions.Left);
            hint.text = placeholder;
            hint.color = new Color(1f, 1f, 1f, 0.35f);
            var text = UIFactory.Text(UIFactory.Stretch(area, "Text"), height * 0.45f, TextAlignmentOptions.Left);
            text.text = "";

            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = bg;
            field.characterLimit = 40;
            // Navigation clavier / manette : on se place sur le champ sans écrire ; Entrée / A pour taper
            field.shouldActivateOnSelect = false;
            UIFactory.SetAccent(field, Accent);
            return field;
        }
    }
}
