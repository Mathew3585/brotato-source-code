using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Jusqu'à 6 armes réparties en cercle autour du personnage, comme dans Brotato.</summary>
    public class WeaponHolder : MonoBehaviour
    {
        public const int MaxWeapons = 6;

        [SerializeField] Weapon weaponPrefab;
        [SerializeField] float radius = 0.7f;
        [SerializeField] Vector2 centerOffset = new(0f, -0.1f);

        readonly List<Weapon> weapons = new();
        Player owner;

        public IReadOnlyList<Weapon> Weapons => weapons;
        public bool IsFull => weapons.Count >= MaxWeapons;

        public void Init(Player player) => owner = player;

        public bool TryAdd(WeaponData data)
        {
            if (IsFull || data == null) return false;
            var weapon = Instantiate(weaponPrefab, transform);
            weapon.Init(owner, data);
            ApplyPassives(data, +1f);
            weapons.Add(weapon);
            Layout();
            owner.MarkNetDirty();
            return true;
        }

        /// <summary>Peut-on obtenir cette arme ? (place libre, ou fusion possible avec une arme identique)</summary>
        public bool CanAcquire(WeaponData data) => !IsFull || FindByData(data, requireNextTier: true) != null;

        /// <summary>Inventaire plein et arme identique possédée : l'achat la fera passer au tier suivant.</summary>
        public bool WouldMerge(WeaponData data) => IsFull && FindByData(data, requireNextTier: true) != null;

        /// <summary>Ajoute l'arme ; si l'inventaire est plein, la fusionne avec une arme identique.</summary>
        public bool Acquire(WeaponData data)
        {
            if (TryAdd(data)) return true;
            var same = FindByData(data, requireNextTier: true);
            if (same == null) return false;
            ChangeData(same, data.nextTier);
            return true;
        }

        public Weapon FindMergePartner(Weapon weapon)
        {
            if (weapon == null || weapon.Data.nextTier == null) return null;
            foreach (var w in weapons)
                if (w != weapon && w.Data == weapon.Data) return w;
            return null;
        }

        /// <summary>Fusionne 2 armes identiques en une arme de tier supérieur.</summary>
        public bool Merge(Weapon weapon)
        {
            var partner = FindMergePartner(weapon);
            if (partner == null) return false;
            ChangeData(weapon, weapon.Data.nextTier);
            Remove(partner);
            return true;
        }

        /// <summary>Remplace toutes les armes (chargement d'une sauvegarde). Les bonus passifs ne sont pas réappliqués.</summary>
        public void Restore(IEnumerable<WeaponData> saved)
        {
            foreach (var w in weapons) Destroy(w.gameObject);
            weapons.Clear();
            foreach (var data in saved)
            {
                if (data == null || IsFull) continue;
                var weapon = Instantiate(weaponPrefab, transform);
                weapon.Init(owner, data);
                weapons.Add(weapon);
            }
            Layout();
        }

        public void Remove(Weapon weapon)
        {
            if (!weapons.Remove(weapon)) return;
            ApplyPassives(weapon.Data, -1f);
            Destroy(weapon.gameObject);
            Layout();
            owner.MarkNetDirty();
        }

        void ChangeData(Weapon weapon, WeaponData newData)
        {
            ApplyPassives(weapon.Data, -1f);
            weapon.Init(owner, newData);
            ApplyPassives(newData, +1f);
            owner.MarkNetDirty();
        }

        /// <summary>Ajoute (+1) ou retire (-1) les bonus passifs d'une arme.</summary>
        void ApplyPassives(WeaponData data, float sign)
        {
            if (data == null || data.passiveModifiers == null || data.passiveModifiers.Length == 0) return;
            var mods = new StatModifier[data.passiveModifiers.Length];
            for (int i = 0; i < mods.Length; i++)
                mods[i] = new StatModifier(data.passiveModifiers[i].stat, data.passiveModifiers[i].value * sign);
            owner.ApplyStats(mods);
        }

        Weapon FindByData(WeaponData data, bool requireNextTier)
        {
            foreach (var w in weapons)
                if (w.Data == data && (!requireNextTier || data.nextTier != null)) return w;
            return null;
        }

        void Layout()
        {
            int n = weapons.Count;
            for (int i = 0; i < n; i++)
            {
                // Première arme à droite, les suivantes réparties régulièrement
                float angle = (n == 1 ? 0f : 360f * i / n - 15f) * Mathf.Deg2Rad;
                weapons[i].SetRestPosition(centerOffset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }
}
