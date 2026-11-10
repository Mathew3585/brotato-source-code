using System;
using System.Collections.Generic;
using System.Linq;

namespace BrotatoLike
{
    /// <summary>Statistiques d'un joueur pendant une partie (affichées sur l'écran de fin).</summary>
    [Serializable]
    public class RunStats
    {
        public int kills;
        public float damageDealt;
        public float damageTaken;
        public int materialsCollected;
        public List<string> weaponNames = new();
        public List<float> weaponDamage = new();
        /// <summary>Dégâts des structures (tourelles des objets) : un bonus, pas une arme.</summary>
        public float structureDamage;

        public void AddDamage(WeaponData weapon, float amount)
        {
            damageDealt += amount;
            if (weapon == null) return;
            if (weapon.kind == WeaponKind.Turret)
            {
                structureDamage += amount;
                return;
            }
            string name = RootName(weapon);
            int i = weaponNames.IndexOf(name);
            if (i < 0)
            {
                weaponNames.Add(name);
                weaponDamage.Add(0f);
                i = weaponNames.Count - 1;
            }
            weaponDamage[i] += amount;
        }

        /// <summary>Armes classées par dégâts infligés (les plus efficaces d'abord).</summary>
        public IEnumerable<(string name, float damage)> TopWeapons(int count) =>
            weaponNames.Select((n, i) => (n, weaponDamage[i]))
                .Where(x => !IsStructure(x.n))
                .OrderByDescending(x => x.Item2).Take(count);

        /// <summary>Anciennes parties : la tourelle était comptée comme une arme.</summary>
        static bool IsStructure(string name) => name == "Turret" || name == "Tourelle";

        /// <summary>"Fusil III" et "Fusil" comptent pour la même arme.</summary>
        public static string RootName(WeaponData weapon)
        {
            string name = weapon.displayName;
            if (weapon.tier <= 1) return name;
            int space = name.LastIndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }
    }
}
