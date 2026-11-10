using System.Collections.Generic;
using System.IO;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Synchronisation de la partie en ligne (créé par l'hôte dans la scène de jeu).
    /// Hôte : envoie ~15 fois par seconde l'état compact des ennemis, matériaux et joueurs, plus les événements
    /// (état de la partie, chiffres de dégâts, tirs ennemis, marqueurs d'apparition, inventaires).
    /// Clients : crée / met à jour / retire les copies d'affichage (« répliques »).
    /// </summary>
    public class NetGame : NetworkBehaviour
    {
        const float SnapshotInterval = 1f / 15f;
        const float HeavyStateInterval = 2f;
        const int EnemiesPerChunk = 110;
        const int PickupsPerChunk = 120;
        const int MaxPopupsPerTick = 40;
        const float PositionScale = 100f; // positions envoyées en centièmes d'unité (short)

        GameManager gm;
        readonly List<EnemyData> registry = new();
        readonly Dictionary<EnemyData, byte> registryIndex = new();

        // Hôte
        float snapshotTimer;
        float heavyTimer;
        ushort frame;
        readonly List<string> popupTexts = new();
        readonly List<Vector2> popupPositions = new();
        readonly List<Color> popupColors = new();
        readonly List<float> popupScales = new();

        // Clients
        readonly Dictionary<ushort, Enemy> enemyReplicas = new();
        readonly Dictionary<ushort, ushort> enemySeen = new();
        readonly Dictionary<ushort, Pickup> pickupReplicas = new();
        readonly Dictionary<ushort, ushort> pickupSeen = new();
        readonly List<ushort> toRemove = new();

        void Awake()
        {
            gm = GameManager.Instance;
            BuildRegistry();
        }

        /// <summary>Liste des types d'ennemis, construite à l'identique chez l'hôte et chez les clients.</summary>
        void BuildRegistry()
        {
            void Add(EnemyData e)
            {
                if (e == null || registryIndex.ContainsKey(e)) return;
                registryIndex[e] = (byte)registry.Count;
                registry.Add(e);
            }
            foreach (var entry in gm.waveSet.enemies)
            {
                Add(entry.enemy);
                if (entry.enemy != null) Add(entry.enemy.splitInto);
            }
            foreach (var wave in gm.waveSet.waves)
            {
                Add(wave.boss);
                if (wave.boss != null) Add(wave.boss.splitInto);
            }
        }

        // ================================================================ Hôte

        public override void OnStartServer()
        {
            base.OnStartServer();
            gm.StateChanged += OnServerStateChanged;
            DamagePopup.Broadcast = QueuePopup;
            Enemy.VolleyFired += OnVolleyFired;
            EnemySpawner.MarkerShown += OnMarkerShown;
            OnServerStateChanged(gm.State);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (gm != null) gm.StateChanged -= OnServerStateChanged;
            DamagePopup.Broadcast = null;
            Enemy.VolleyFired -= OnVolleyFired;
            EnemySpawner.MarkerShown -= OnMarkerShown;
        }

        void OnServerStateChanged(GameState state) => RpcState((byte)state, gm.WaveIndex, gm.StateTimer);

        void QueuePopup(Vector2 position, string text, Color color, float scale)
        {
            if (popupTexts.Count >= MaxPopupsPerTick) return;
            popupTexts.Add(text);
            popupPositions.Add(position);
            popupColors.Add(color);
            popupScales.Add(scale);
        }

        void OnVolleyFired(EnemyData data, Vector2 position, Vector2 direction, int count, float spread)
        {
            if (registryIndex.TryGetValue(data, out byte type)) RpcVolley(type, position, direction, (byte)count, spread);
        }

        void OnMarkerShown(Vector2 position, float duration, float scale) => RpcMarker(position, duration, scale);

        void Update()
        {
            if (!IsServerInitialized) return;
            float dt = Time.unscaledDeltaTime;

            snapshotTimer -= dt;
            if (snapshotTimer <= 0f)
            {
                snapshotTimer = SnapshotInterval;
                frame++;
                SendEnemies();
                SendPickups();
                SendVitals();
                SendPopups();
            }

            heavyTimer -= dt;
            bool periodic = heavyTimer <= 0f;
            if (periodic) heavyTimer = HeavyStateInterval;
            foreach (var p in Player.All)
            {
                if (p.Character == null || (!periodic && !p.NetDirty)) continue;
                p.ClearNetDirty();
                RpcPlayerState((byte)p.Index, p.ToSave());
            }
        }

        static short Pack(float v) => (short)Mathf.Clamp(Mathf.RoundToInt(v * PositionScale), short.MinValue, short.MaxValue);
        static float Unpack(short v) => v / PositionScale;

        void SendEnemies()
        {
            var list = Enemy.Active;
            int total = list.Count;
            int chunks = Mathf.Max(1, Mathf.CeilToInt(total / (float)EnemiesPerChunk));
            for (int c = 0; c < chunks; c++)
            {
                int start = c * EnemiesPerChunk;
                int count = Mathf.Min(EnemiesPerChunk, total - start);
                using var ms = new MemoryStream(8 + count * 8);
                using var w = new BinaryWriter(ms);
                w.Write(frame);
                w.Write((byte)(c == chunks - 1 ? 1 : 0));
                w.Write((byte)Mathf.Max(0, count));
                for (int i = 0; i < count; i++)
                {
                    var e = list[start + i];
                    w.Write(e.NetId);
                    w.Write(registryIndex.TryGetValue(e.Data, out byte type) ? type : (byte)0);
                    w.Write(Pack(e.Position.x));
                    w.Write(Pack(e.Position.y));
                    w.Write(e.NetFlags);
                }
                RpcEnemies(ms.ToArray(), Channel.Unreliable);
            }
        }

        void SendPickups()
        {
            var list = Pickup.Active;
            int total = list.Count;
            int chunks = Mathf.Max(1, Mathf.CeilToInt(total / (float)PickupsPerChunk));
            for (int c = 0; c < chunks; c++)
            {
                int start = c * PickupsPerChunk;
                int count = Mathf.Min(PickupsPerChunk, total - start);
                using var ms = new MemoryStream(8 + count * 7);
                using var w = new BinaryWriter(ms);
                w.Write(frame);
                w.Write((byte)(c == chunks - 1 ? 1 : 0));
                w.Write((byte)Mathf.Max(0, count));
                for (int i = 0; i < count; i++)
                {
                    var p = list[start + i];
                    w.Write(p.NetId);
                    w.Write(p.NetSprite);
                    w.Write(Pack(p.transform.position.x));
                    w.Write(Pack(p.transform.position.y));
                }
                RpcPickups(ms.ToArray(), Channel.Unreliable);
            }
        }

        void SendVitals()
        {
            using var ms = new MemoryStream(64);
            using var w = new BinaryWriter(ms);
            w.Write(gm.StateTimer);
            var boss = gm.CurrentBoss;
            w.Write(boss != null ? boss.NetId : (ushort)0);
            w.Write(boss != null ? boss.Health : 0f);
            w.Write(boss != null ? boss.MaxHealth : 1f);
            int count = 0;
            foreach (var p in Player.All)
                if (p.Character != null) count++;
            w.Write((byte)count);
            foreach (var p in Player.All)
            {
                if (p.Character == null) continue;
                w.Write((byte)p.Index);
                w.Write(p.Health);
                w.Write(p.IsAlive);
                w.Write(p.IsDowned);
                w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(p.BleedOut01) * 255f));
                w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(p.Revive01) * 255f));
                w.Write(p.Materials);
                w.Write(p.Xp);
                w.Write((short)p.Level);
                w.Write(p.IsReady);
            }
            RpcVitals(ms.ToArray(), Channel.Unreliable);
        }

        void SendPopups()
        {
            if (popupTexts.Count == 0) return;
            RpcPopups(popupTexts.ToArray(), popupPositions.ToArray(), popupColors.ToArray(), popupScales.ToArray(), Channel.Unreliable);
            popupTexts.Clear();
            popupPositions.Clear();
            popupColors.Clear();
            popupScales.Clear();
        }

        // ================================================================ Clients

        bool IsRemoteClient => !IsServerInitialized;

        [ObserversRpc(BufferLast = true)]
        void RpcState(byte state, int wave, float timer)
        {
            if (!IsRemoteClient) return;
            gm.ApplyNetState((GameState)state, wave, timer);
            // L'hôte retire les ennemis entre les vagues : on oublie les répliques détruites localement
            if ((GameState)state != GameState.Wave)
            {
                enemyReplicas.Clear();
                enemySeen.Clear();
            }
        }

        [ObserversRpc]
        void RpcEnemies(byte[] data, Channel channel = Channel.Unreliable)
        {
            if (!IsRemoteClient) return;
            using var r = new BinaryReader(new MemoryStream(data));
            ushort f = r.ReadUInt16();
            bool last = r.ReadByte() == 1;
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                ushort id = r.ReadUInt16();
                byte type = r.ReadByte();
                var pos = new Vector2(Unpack(r.ReadInt16()), Unpack(r.ReadInt16()));
                byte flags = r.ReadByte();
                if (type >= registry.Count) continue;

                if (!enemyReplicas.TryGetValue(id, out var enemy) || !IsValidReplica(enemy, id))
                {
                    // Un ennemi déjà mort à son apparition n'a pas besoin d'être affiché
                    if ((flags & 4) != 0) continue;
                    enemy = gm.EnemyPool.Get(pos);
                    enemy.SpawnReplica(registry[type], id, pos);
                    enemyReplicas[id] = enemy;
                }
                enemy.ApplySnapshot(pos, flags);
                enemySeen[id] = f;
            }
            if (last) RemoveUnseenEnemies(f);
        }

        static bool IsValidReplica(Enemy e, ushort id) => e != null && e.gameObject.activeSelf && e.IsReplica && e.NetId == id;

        void RemoveUnseenEnemies(ushort f)
        {
            toRemove.Clear();
            foreach (var (id, seen) in enemySeen)
                if ((ushort)(f - seen) > 1) toRemove.Add(id); // absent de 2 images de suite
            foreach (var id in toRemove)
            {
                enemySeen.Remove(id);
                if (enemyReplicas.TryGetValue(id, out var e) && IsValidReplica(e, id)) e.Despawn();
                enemyReplicas.Remove(id);
            }
        }

        [ObserversRpc]
        void RpcPickups(byte[] data, Channel channel = Channel.Unreliable)
        {
            if (!IsRemoteClient) return;
            using var r = new BinaryReader(new MemoryStream(data));
            ushort f = r.ReadUInt16();
            bool last = r.ReadByte() == 1;
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                ushort id = r.ReadUInt16();
                byte sprite = r.ReadByte();
                var pos = new Vector2(Unpack(r.ReadInt16()), Unpack(r.ReadInt16()));
                if (!pickupReplicas.TryGetValue(id, out var pickup) || pickup == null || !pickup.gameObject.activeSelf || pickup.NetId != id)
                {
                    var s = sprite == 3 ? gm.crateSprite : gm.materialSprites[Mathf.Min(sprite, gm.materialSprites.Length - 1)];
                    pickup = gm.PickupPool.Get(pos);
                    pickup.SpawnReplica(id, sprite, s, pos);
                    pickupReplicas[id] = pickup;
                }
                pickup.ApplySnapshot(pos);
                pickupSeen[id] = f;
            }
            if (!last) return;

            toRemove.Clear();
            foreach (var (id, seen) in pickupSeen)
                if ((ushort)(f - seen) > 1) toRemove.Add(id);
            foreach (var id in toRemove)
            {
                pickupSeen.Remove(id);
                if (pickupReplicas.TryGetValue(id, out var p) && p != null && p.gameObject.activeSelf && p.NetId == id) p.DespawnReplica();
                pickupReplicas.Remove(id);
            }
        }

        [ObserversRpc]
        void RpcVitals(byte[] data, Channel channel = Channel.Unreliable)
        {
            if (!IsRemoteClient) return;
            using var r = new BinaryReader(new MemoryStream(data));
            gm.ApplyNetTimer(r.ReadSingle());
            ushort bossId = r.ReadUInt16();
            float bossHealth = r.ReadSingle();
            float bossMax = r.ReadSingle();
            if (bossId != 0 && enemyReplicas.TryGetValue(bossId, out var boss) && IsValidReplica(boss, bossId))
                boss.SetReplicaHealth(bossHealth, bossMax);

            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                int index = r.ReadByte();
                float health = r.ReadSingle();
                bool alive = r.ReadBoolean();
                bool downed = r.ReadBoolean();
                float bleedOut01 = r.ReadByte() / 255f;
                float revive01 = r.ReadByte() / 255f;
                int materials = r.ReadInt32();
                float xp = r.ReadSingle();
                int level = r.ReadInt16();
                bool ready = r.ReadBoolean();
                var player = FindPlayer(index);
                if (player != null) player.ApplyNetVitals(health, alive, downed, bleedOut01, revive01, materials, xp, level, ready);
            }
        }

        [ObserversRpc]
        void RpcPlayerState(byte index, PlayerSave state)
        {
            if (!IsRemoteClient) return;
            var player = FindPlayer(index);
            if (player != null) player.ApplyNetState(state, gm.ResolveShopItem);
        }

        [ObserversRpc]
        void RpcPopups(string[] texts, Vector2[] positions, Color[] colors, float[] scales, Channel channel = Channel.Unreliable)
        {
            if (!IsRemoteClient) return;
            for (int i = 0; i < texts.Length; i++)
            {
                DamagePopup.ShowTextLocal(positions[i], texts[i], colors[i], scales[i]);
                // Chiffre de dégâts sur un ennemi : petit son d'impact
                if (texts[i].Length > 0 && char.IsDigit(texts[i][0])) AudioManager.Play(SoundId.EnemyHit);
            }
        }

        [ObserversRpc]
        void RpcVolley(byte type, Vector2 position, Vector2 direction, byte count, float spread)
        {
            if (!IsRemoteClient || type >= registry.Count) return;
            Enemy.FireVolleyVisual(registry[type], position, direction, count, spread);
        }

        [ObserversRpc]
        void RpcMarker(Vector2 position, float duration, float scale)
        {
            if (IsRemoteClient && gm.spawner != null) gm.spawner.ShowRemoteMarker(position, duration, scale);
        }

        static Player FindPlayer(int index)
        {
            foreach (var p in Player.All)
                if (p.Index == index && p.Character != null) return p;
            return null;
        }
    }
}
