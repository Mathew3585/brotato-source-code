using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BrotatoLike.EditorTools
{
    /// <summary>
    /// Passe tout ce qui s'affiche en anglais : noms des armes / objets / ennemis / personnages
    /// et textes déjà présents dans les scènes. Ne remplace que les textes français connus
    /// (un texte que tu as modifié toi-même n'est pas touché).
    /// </summary>
    static class EnglishMigration
    {
        const string DataDir = "Assets/GAME/Data";

        static readonly Dictionary<string, string> Names = new()
        {
            // Armes
            ["Fusil"] = "Rifle", ["Tromblon"] = "Blunderbuss", ["Mousquet"] = "Musket", ["Pelle"] = "Shovel",
            ["Fourche"] = "Pitchfork", ["Gourdin"] = "Club", ["Lance"] = "Spear", ["Hache"] = "Axe", ["Épée"] = "Sword",
            ["Pistolet"] = "Pistol", ["Sniper"] = "Sniper", ["Mitrailleuse"] = "SMG", ["Lance-roquettes"] = "Rocket Launcher",
            ["Bâton magique"] = "Magic Staff", ["Fusil d'assaut"] = "Assault Rifle", ["Fusil à pompe"] = "Shotgun",
            ["Revolver"] = "Revolver", ["Harpon"] = "Harpoon", ["Tourelle"] = "Turret", ["Arrosoir"] = "Watering Can",
            ["Canne à pêche"] = "Fishing Rod", ["Pioche"] = "Pickaxe",
            // Objets
            ["Cœur robuste"] = "Sturdy Heart", ["Gigot"] = "Leg of Lamb", ["Potion verte"] = "Green Potion",
            ["Poisson frais"] = "Fresh Fish", ["Plaque de fer"] = "Iron Plate", ["Chapeau de paille"] = "Straw Hat",
            ["Pelote de laine"] = "Ball of Wool", ["Charbon"] = "Coal", ["Parchemin"] = "Scroll",
            ["Essence magique"] = "Magic Essence", ["Pièce porte-bonheur"] = "Lucky Coin", ["Flamme"] = "Flame",
            ["Diamant"] = "Diamond", ["L'Anneau"] = "The Ring", ["Couronne"] = "Crown", ["Œuf magique"] = "Magic Egg",
            ["Fin du monde"] = "World Ender",
            // Ennemis
            ["Zombie coureur"] = "Runner Zombie", ["Squelette"] = "Skeleton", ["Squelette pirate"] = "Pirate Skeleton",
            ["Pierre tombale"] = "Tombstone", ["Corbeau"] = "Crow", ["Goule"] = "Ghoul", ["Mage noir"] = "Dark Mage",
            ["Tête de feu"] = "Firehead", ["Scarabée"] = "Beetle", ["Larve"] = "Larva", ["Scarabée géant"] = "Giant Beetle",
            ["Canon"] = "Cannon",
            // Personnages
            ["Pierre"] = "Pete", ["Lucie"] = "Lucy", ["Margot"] = "Maggie",
        };

        static readonly Dictionary<string, string> Taglines = new()
        {
            ["Polyvalent"] = "Versatile",
            ["Tireuse d'élite"] = "Sharpshooter",
            ["Costaud (mêlée)"] = "Brute (melee)",
            ["Chanceuse et rapide"] = "Lucky and fast",
        };

        /// <summary>Textes posés dans les scènes par les outils de construction.</summary>
        public static readonly Dictionary<string, string> SceneTexts = new()
        {
            // HUD
            ["VAGUE 1 / 20"] = "WAVE 1 / 20",
            ["Bouger : ZQSD / flèches / stick gauche   •   Visée manuelle : T / clic stick droit   •   Pause : Échap / Start"] =
                "Move: WASD / arrows / left stick   •   Manual aim: T / right stick click   •   Pause: Esc / Start",
            ["Niv. 1"] = "Lv. 1", ["Matériaux : 0"] = "Materials: 0", ["Visée : auto"] = "Aim: auto",
            // Boutique
            ["CAISSE !"] = "CRATE!", ["Nom de l'objet"] = "Item name", ["Prendre"] = "Take", ["Recycler (+5)"] = "Recycle (+5)",
            ["NIVEAU SUPÉRIEUR !"] = "LEVEL UP!", ["+5% Dégâts"] = "+5% Damage", ["Nom de l'offre"] = "Offer name",
            ["VENDU"] = "SOLD", ["Relancer (1)"] = "Reroll (1)", ["PRÊT"] = "READY", ["Armes (1/6)"] = "Weapons (1/6)",
            ["Vendre (+5)"] = "Sell (+5)", ["Fusionner"] = "Merge", ["Objets (0)"] = "Items (0)",
            ["PV max 10\nRégénération 0\nArmure 0"] = "Max HP 10\nHP Regen 0\nArmor 0",
            ["Dégâts 0%\nVitesse d'attaque 0%\nCritique 0%"] = "Damage 0%\nAttack Speed 0%\nCrit Chance 0%",
            ["PRÊT !\n<size=45%>En attente des autres joueurs...</size>"] = "READY!\n<size=45%>Waiting for the other players...</size>",
            ["Annuler"] = "Cancel",
            // Pause / options / fin
            ["Reprendre"] = "Resume", ["Menu principal"] = "Main menu", ["Effets sonores"] = "Sound effects",
            ["Musique"] = "Music", ["Plein écran"] = "Fullscreen", ["Secousses d'écran"] = "Screen shake", ["Retour"] = "Back",
            ["Vague atteinte : 5 / 20"] = "Reached wave 5 / 20",
            ["Ennemis tués : 0\nDégâts infligés : 0"] = "Enemies killed: 0\nDamage dealt: 0",
            ["Entrée / A : recommencer   •   Échap / B : menu principal"] = "Enter / A: restart   •   Esc / B: main menu",
            // Menu principal
            ["LA FERME MAUDITE"] = "CURSED FARM",
            ["Survivez aux vagues de morts-vivants — de 1 à 4 joueurs"] = "Survive the undead waves — 1 to 4 players",
            ["Nouvelle partie"] = "New game", ["Quitter"] = "Quit", ["Continuer"] = "Continue",
            ["Naviguer : flèches / stick   •   Valider : Entrée / A   •   Souris possible"] =
                "Navigate: arrows / stick   •   Confirm: Enter / A   •   Mouse works too",
            ["CHOISISSEZ VOS PERSONNAGES"] = "CHOOSE YOUR CHARACTERS",
            ["Clavier : Entrée pour rejoindre   •   Manette : A pour rejoindre   •   4 joueurs max"] =
                "Keyboard: Enter to join   •   Gamepad: A to join   •   4 players max",
            ["Appuie sur\n<color=#FFD966>Entrée</color> ou <color=#FFD966>A</color>\npour rejoindre"] =
                "Press\n<color=#FFD966>Enter</color> or <color=#FFD966>A</color>\nto join",
            ["Personnage"] = "Character", ["+10 PV max"] = "+10 Max HP", ["Arme : Fusil"] = "Weapon: Rifle",
            ["Entrée : prêt"] = "Enter: ready", ["Q / D ou ← / → : changer   •   Échap : quitter"] = "A / D or ← / →: change   •   Esc: leave",
            ["Échap / B : retour au menu"] = "Esc / B: back to menu",
        };

        static readonly Regex PlayerTag = new(@"^J(\d)( - Personnage)?$");

        public static void MigrateData()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { DataDir }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                bool changed = asset switch
                {
                    ShopItemData item => Translate(ref item.displayName),
                    EnemyData enemy => Translate(ref enemy.displayName),
                    CharacterData character => Translate(ref character.displayName) | TranslateTagline(character),
                    _ => false,
                };
                if (changed) EditorUtility.SetDirty(asset);
            }
        }

        /// <summary>Traduit "Fusil" ou "Fusil III" (le suffixe de tier est conservé).</summary>
        static bool Translate(ref string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            if (Names.TryGetValue(displayName, out var english))
            {
                displayName = english;
                return true;
            }
            int space = displayName.LastIndexOf(' ');
            if (space <= 0) return false;
            string suffix = displayName.Substring(space + 1);
            if (!suffix.All(c => c is 'I' or 'V')) return false;
            if (!Names.TryGetValue(displayName.Substring(0, space), out english)) return false;
            displayName = english + " " + suffix;
            return true;
        }

        static bool TranslateTagline(CharacterData c)
        {
            if (c.tagline == null || !Taglines.TryGetValue(c.tagline, out var english)) return false;
            c.tagline = english;
            return true;
        }

        public static bool MigrateOpenScene()
        {
            bool changed = false;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string current = text.text;
                if (string.IsNullOrEmpty(current)) continue;
                string english = null;
                if (SceneTexts.TryGetValue(current, out var mapped)) english = mapped;
                else
                {
                    var m = PlayerTag.Match(current);
                    if (m.Success) english = "P" + m.Groups[1].Value + (m.Groups[2].Success ? " - Character" : "");
                }
                if (english == null || english == current) continue;
                text.text = english;
                EditorUtility.SetDirty(text);
                changed = true;
            }
            return changed;
        }
    }
}
