using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Partie en cours sauvegardée (à l'ouverture de la boutique et après chaque action dedans).</summary>
    [Serializable]
    public class RunSave
    {
        public int waveIndex;
        public int difficulty;
        public List<PlayerSave> players = new();
    }

    [Serializable]
    public class PlayerSave
    {
        public string character;
        public int materials;
        public int level;
        public float xp;
        public int pendingLevelUps;
        public int pendingCrates;
        public float[] stats;
        public List<string> weapons = new();
        public List<string> items = new();
        public List<string> offerItems = new();
        public List<bool> offerLocked = new();
        public List<bool> offerSold = new();
        public int rerollCount;
        public RunStats runStats;
    }

    /// <summary>Lecture / écriture de la partie en cours avec Easy Save 3.</summary>
    public static class SaveSystem
    {
        const string FilePath = "run.es3";
        const string Key = "run";

        public static bool HasSave
        {
            get
            {
                try { return ES3.KeyExists(Key, FilePath); }
                catch (Exception e)
                {
                    Debug.LogWarning("[BrotatoLike] Sauvegarde illisible : " + e.Message);
                    return false;
                }
            }
        }

        public static void Save(RunSave save)
        {
            try { ES3.Save(Key, save, FilePath); }
            catch (Exception e) { Debug.LogWarning("[BrotatoLike] Échec de la sauvegarde : " + e.Message); }
        }

        public static RunSave Load()
        {
            try { return HasSave ? ES3.Load<RunSave>(Key, FilePath) : null; }
            catch (Exception e)
            {
                Debug.LogWarning("[BrotatoLike] Impossible de charger la sauvegarde : " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (ES3.FileExists(FilePath)) ES3.DeleteFile(FilePath);
            }
            catch (Exception e) { Debug.LogWarning("[BrotatoLike] Impossible de supprimer la sauvegarde : " + e.Message); }
        }
    }
}
