using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrotatoLike
{
    /// <summary>
    /// Joueurs choisis dans l'écran de sélection : leurs périphériques et leur personnage.
    /// Survit au changement de scène (statique) pour recréer les joueurs dans la scène de jeu.
    /// </summary>
    public static class Session
    {
        public class Slot
        {
            public InputDevice[] devices;
            public string controlScheme;
            public CharacterData character;
        }

        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        public static readonly List<Slot> Players = new();

        /// <summary>Partie sauvegardée à reprendre (null = nouvelle partie).</summary>
        public static RunSave Resume;

        /// <summary>Niveau de difficulté choisi (index dans DifficultySet).</summary>
        public static int Difficulty;

        public static readonly Color[] PlayerColors =
        {
            new(0.35f, 0.65f, 1f),
            new(1f, 0.45f, 0.7f),
            new(0.45f, 0.9f, 0.45f),
            new(1f, 0.8f, 0.3f),
        };

        public static Color ColorOf(int playerIndex) => PlayerColors[Mathf.Abs(playerIndex) % PlayerColors.Length];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Players.Clear();
            Resume = null;
            Difficulty = 0;
        }
    }
}
