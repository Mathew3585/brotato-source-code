using System;
using System.Globalization;
using System.Linq;
using System.Text;
using IngameDebugConsole;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Commandes de test pour la console IngameDebugConsole (tape "help" dans la console pour la liste).
    /// Les noms d'armes / objets / ennemis acceptent le nom affiché, sans accents ni majuscules.
    /// </summary>
    public static class DebugCommands
    {
        static GameManager Gm => GameManager.Instance;

        [ConsoleMethod("materials", "Gives materials to every player", "amount")]
        public static void Materials(int amount)
        {
            if (!InGame()) return;
            foreach (var p in Player.All) p.CollectMaterials(amount, giveXp: false);
            Debug.Log($"+{amount} materials for every player");
        }

        [ConsoleMethod("challenges", "Completes every challenge (0 = reset them all)", "done")]
        public static void SetChallenges(int done)
        {
            Challenges.DebugSetAll(done != 0);
            Debug.Log(done != 0 ? "Every challenge completed" : "Challenges reset");
        }

        [ConsoleMethod("unlockdanger", "Unlocks every danger level (0 = lock them again)", "upTo")]
        public static void UnlockDanger(int upTo)
        {
            PlayerPrefs.SetInt("danger_unlocked", Mathf.Max(0, upTo));
            PlayerPrefs.Save();
            Debug.Log($"Danger levels unlocked up to {DifficultySet.Unlocked}");
        }

        // Le LAN n'a plus de bouton : il reste pour tester seul (build + éditeur sur le même PC)
        [ConsoleMethod("hostlan", "Hosts a LAN room (testing without Steam)")]
        public static void HostLan()
        {
            if (OnlineManager.Instance == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Session.MenuScene) Debug.Log("Only in the main menu");
            else OnlineManager.Instance.HostLan();
        }

        [ConsoleMethod("joinlan", "Joins a LAN room (testing without Steam)", "ip")]
        public static void JoinLan(string ip)
        {
            if (OnlineManager.Instance == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Session.MenuScene) Debug.Log("Only in the main menu");
            else OnlineManager.Instance.JoinLan(ip);
        }

        [ConsoleMethod("levelup","Gives levels to every player", "levels")]
        public static void LevelUp(int levels)
        {
            if (!InGame()) return;
            foreach (var p in Player.All)
                for (int i = 0; i < levels; i++) p.CollectMaterials(Mathf.CeilToInt(p.XpToNextLevel - p.Xp) + 1);
        }

        [ConsoleMethod("skipwave", "Ends the current wave (goes to the shop)")]
        public static void SkipWave()
        {
            if (InGame()) Gm.DebugEndWave();
        }

        [ConsoleMethod("wave", "Opens the shop right before wave N (20 = boss)", "number")]
        public static void GoToWave(int waveNumber)
        {
            if (InGame()) Gm.DebugGoToWave(waveNumber);
        }

        [ConsoleMethod("god", "Toggles invincibility")]
        public static void God()
        {
            Player.DebugInvincible = !Player.DebugInvincible;
            Debug.Log("Invincibility: " + (Player.DebugInvincible ? "ON" : "OFF"));
        }

        [ConsoleMethod("killall", "Kills every enemy (with loot)")]
        public static void KillAll()
        {
            for (int i = Enemy.Active.Count - 1; i >= 0; i--)
                if (i < Enemy.Active.Count && Enemy.Active[i].IsAlive) Enemy.Active[i].Die(true);
        }

        [ConsoleMethod("spawn", "Spawns enemies near player 1", "name", "count")]
        public static void Spawn(string name, int count)
        {
            if (!InGame()) return;
            var data = AllEnemies().FirstOrDefault(e => Matches(e.displayName, name) || Matches(e.name, name));
            if (data == null)
            {
                Debug.LogWarning("Unknown enemy. List: " + string.Join(", ", AllEnemies().Select(e => e.displayName)));
                return;
            }
            var center = Player.All.Count > 0 ? Player.All[0].Position : Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var pos = Arena.Instance.Clamp(center + UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(3f, 5f), 1f);
                Gm.EnemyPool.Get(pos).Spawn(data, Gm.WaveIndex);
            }
        }

        [ConsoleMethod("weapon", "Gives a weapon to every player (tier 1 to 4)", "name", "tier")]
        public static void GiveWeapon(string name, int tier)
        {
            if (!InGame()) return;
            var data = Gm.shopCatalog.weapons.FirstOrDefault(w => w != null && w.tier == Mathf.Clamp(tier, 1, 4)
                && (Matches(w.displayName, name) || Matches(Root(w.displayName), name)));
            if (data == null)
            {
                Debug.LogWarning("Unknown weapon. List: " + string.Join(", ", Gm.shopCatalog.weapons.Where(w => w != null && w.tier == 1).Select(w => w.displayName)));
                return;
            }
            foreach (var p in Player.All)
                if (!p.Weapons.Acquire(data)) Debug.LogWarning($"P{p.Index + 1} has no room for {data.displayName}");
        }

        [ConsoleMethod("item", "Gives an item to every player", "name")]
        public static void GiveItem(string name)
        {
            if (!InGame()) return;
            var data = Gm.shopCatalog.items.FirstOrDefault(i => i != null && (Matches(i.displayName, name) || Matches(i.name, name)));
            if (data == null)
            {
                Debug.LogWarning("Unknown item. List: " + string.Join(", ", Gm.shopCatalog.items.Where(i => i != null).Select(i => i.displayName)));
                return;
            }
            foreach (var p in Player.All) p.AddItem(data);
        }

        [ConsoleMethod("stats", "Prints player stats")]
        public static void Stats()
        {
            foreach (var p in Player.All)
            {
                var sb = new StringBuilder($"P{p.Index + 1} {p.Character?.displayName}: ");
                foreach (StatType s in Enum.GetValues(typeof(StatType))) sb.Append($"{StatInfo.Name(s)} {p.Stats[s]:0.#}  ");
                Debug.Log(sb.ToString());
            }
        }

        // ------------------------------------------------------------

        static bool InGame()
        {
            if (GameManager.Instance != null) return true;
            Debug.LogWarning("This command only works during a run.");
            return false;
        }

        static System.Collections.Generic.IEnumerable<EnemyData> AllEnemies() =>
            Gm.waveSet.enemies.Select(e => e.enemy)
                .Concat(Gm.waveSet.waves.Select(w => w.boss))
                .Concat(Gm.waveSet.enemies.Select(e => e.enemy != null ? e.enemy.splitInto : null))
                .Where(e => e != null).Distinct();

        static string Root(string displayName)
        {
            int space = displayName.LastIndexOf(' ');
            return space > 0 && displayName.Substring(space + 1).All(c => c is 'I' or 'V') ? displayName.Substring(0, space) : displayName;
        }

        /// <summary>Comparaison sans accents, espaces ni majuscules ("epee" = "Épée").</summary>
        static bool Matches(string a, string b) => Normalize(a) == Normalize(b);

        static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
