using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Pool générique d'objets pour éviter Instantiate/Destroy en jeu.</summary>
    public class Pool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly Stack<T> free = new();

        public Pool(T prefab, Transform parent, int prewarm = 0)
        {
            this.prefab = prefab;
            this.parent = parent;
            for (int i = 0; i < prewarm; i++)
            {
                var item = Object.Instantiate(prefab, parent);
                item.gameObject.SetActive(false);
                free.Push(item);
            }
        }

        public T Get(Vector3 position)
        {
            var item = free.Count > 0 ? free.Pop() : Object.Instantiate(prefab, parent);
            item.transform.position = position;
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            free.Push(item);
        }
    }
}
