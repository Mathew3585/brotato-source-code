using System;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Progression des défis sur ce PC (PlayerPrefs) : défis réussis, meilleur score de chacun,
    /// et objets encore verrouillés dans la boutique. Vérifié à chaque changement d'état de la partie.
    /// </summary>
    public static class Challenges
    {
        const string DoneKey = "ch_done_";
        const string BestKey = "ch_best_";

        /// <summary>Un défi vient d'être réussi (le HUD affiche une notification).</summary>
        public static event Action<ChallengeSet.Challenge> Completed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Completed = null;

        public static bool IsDone(ChallengeSet.Challenge c) => PlayerPrefs.GetInt(DoneKey + c.id, 0) == 1;
        public static int Best(ChallengeSet.Challenge c) => PlayerPrefs.GetInt(BestKey + c.id, 0);

        public static int DoneCount
        {
            get
            {
                int count = 0;
                var set = ChallengeSet.Instance;
                if (set != null)
                    foreach (var c in set.challenges)
                        if (IsDone(c)) count++;
                return count;
            }
        }

        /// <summary>Vrai si l'objet est la récompense d'un défi pas encore réussi (une arme verrouille aussi ses tiers supérieurs).</summary>
        public static bool IsLocked(ShopItemData item)
        {
            var set = ChallengeSet.Instance;
            if (set == null || item == null) return false;
            foreach (var c in set.challenges)
            {
                if (c.reward == null || IsDone(c)) continue;
                if (c.reward == item) return true;
                if (c.reward is WeaponData w)
                    for (var t = w.nextTier; t != null; t = t.nextTier)
                        if (t == item) return true;
            }
            return false;
        }

        /// <summary>Appelé par GameManager à chaque changement d'état (hôte, solo et clients).</summary>
        public static void OnStateChanged(GameManager gm, GameState state)
        {
            if (state is not (GameState.Wave or GameState.Intermission or GameState.Shop or GameState.Victory or GameState.GameOver)) return;
            var set = ChallengeSet.Instance;
            if (set == null) return;

            bool victory = state == GameState.Victory;
            foreach (var c in set.challenges)
            {
                if (IsDone(c)) continue;
                int value = Measure(c, gm, victory);
                if (c.HasProgress && value > Best(c)) PlayerPrefs.SetInt(BestKey + c.id, value);
                if (value >= c.target) Complete(c);
            }
            PlayerPrefs.Save();
        }

        /// <summary>Valeur actuelle du défi (comparée à target). Seuls les joueurs de ce PC comptent.</summary>
        static int Measure(ChallengeSet.Challenge c, GameManager gm, bool victory)
        {
            switch (c.type)
            {
                case ChallengeType.ReachWave:
                    return gm.State == GameState.Wave || victory ? gm.WaveIndex + 1 : gm.WaveIndex;
                case ChallengeType.WinRun:
                    return victory ? c.target : 0;
                case ChallengeType.WinOnDanger:
                    return victory && Session.Difficulty >= c.target ? c.target : 0;
            }

            int best = 0;
            foreach (var p in Player.All)
            {
                if (p.IsRemote) continue;
                int v = c.type switch
                {
                    ChallengeType.WinWithCharacter => victory && p.Character == c.character ? c.target : 0,
                    ChallengeType.KillEnemies => p.RunStats.kills,
                    ChallengeType.CollectMaterials => p.RunStats.materialsCollected,
                    ChallengeType.ReachLevel => p.Level,
                    ChallengeType.OwnTier4Weapon => HasTier4(p) ? c.target : 0,
                    _ => 0,
                };
                best = Mathf.Max(best, v);
            }
            return best;
        }

        static bool HasTier4(Player p)
        {
            foreach (var w in p.Weapons.Weapons)
                if (w.Data.tier >= 4) return true;
            return false;
        }

        static void Complete(ChallengeSet.Challenge c)
        {
            PlayerPrefs.SetInt(DoneKey + c.id, 1);
            Debug.Log($"[BrotatoLike] Challenge complete: {c.displayName}");
            Completed?.Invoke(c);
        }

        /// <summary>Debug : réussit (ou réinitialise) tous les défis.</summary>
        public static void DebugSetAll(bool done)
        {
            var set = ChallengeSet.Instance;
            if (set == null) return;
            foreach (var c in set.challenges)
            {
                PlayerPrefs.SetInt(DoneKey + c.id, done ? 1 : 0);
                if (!done) PlayerPrefs.SetInt(BestKey + c.id, 0);
            }
            PlayerPrefs.Save();
        }
    }
}
