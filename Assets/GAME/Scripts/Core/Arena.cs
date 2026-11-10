using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Zone de jeu rectangulaire : les joueurs et les ennemis restent dedans.</summary>
    public class Arena : MonoBehaviour
    {
        public static Arena Instance { get; private set; }

        public Vector2 size = new(40, 26);

        public Rect Bounds => new((Vector2)transform.position - size * 0.5f, size);

        void Awake() => Instance = this;

        public Vector2 Clamp(Vector2 p, float margin = 0.5f)
        {
            var b = Bounds;
            p.x = Mathf.Clamp(p.x, b.xMin + margin, b.xMax - margin);
            p.y = Mathf.Clamp(p.y, b.yMin + margin, b.yMax - margin);
            return p;
        }

        public bool Contains(Vector2 p, float margin = 0f)
        {
            var b = Bounds;
            return p.x > b.xMin - margin && p.x < b.xMax + margin && p.y > b.yMin - margin && p.y < b.yMax + margin;
        }

        public Vector2 RandomPoint(float margin = 1f)
        {
            var b = Bounds;
            return new Vector2(Random.Range(b.xMin + margin, b.xMax - margin), Random.Range(b.yMin + margin, b.yMax - margin));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
