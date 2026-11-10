using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Garde ce GameObject entre les changements de scène.
    /// En revenant dans la scène d'origine, le doublon est détruit (un seul exemplaire par Id).
    /// </summary>
    [DisallowMultipleComponent]
    public class DontDestroyOnLoad : MonoBehaviour
    {
        [Tooltip("Identifiant unique de l'objet. Vide = nom du GameObject.")]
        [SerializeField] string id;

        [Tooltip("Détruire les doublons quand on revient dans la scène qui contient cet objet.")]
        [SerializeField] bool destroyDuplicates = true;

        static readonly Dictionary<string, DontDestroyOnLoad> Instances = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instances.Clear();

        string Key => string.IsNullOrEmpty(id) ? gameObject.name : id;

        void Awake()
        {
            if (destroyDuplicates)
            {
                if (Instances.TryGetValue(Key, out var existing) && existing != null && existing != this)
                {
                    Destroy(gameObject);
                    return;
                }
                Instances[Key] = this;
            }

            // DontDestroyOnLoad ne marche que sur un objet racine.
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instances.TryGetValue(Key, out var existing) && existing == this) Instances.Remove(Key);
        }
    }
}
