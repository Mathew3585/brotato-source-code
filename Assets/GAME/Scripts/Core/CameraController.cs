using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Caméra partagée : suit le centre des joueurs et dézoome pour les garder tous à l'écran (coop locale).
    /// Reste dans les limites de l'arène.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [SerializeField] float minSize = 6.5f;
        [SerializeField] float maxSize = 11f;
        [SerializeField] float padding = 3f;
        [SerializeField] float followSharpness = 6f;
        [SerializeField] float zoomSharpness = 3f;
        [Tooltip("Distance visible au-delà du bord de l'arène")]
        [SerializeField] float arenaMargin = 1.5f;

        Camera cam;
        Vector2 focus;
        float shake;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            focus = transform.position;
        }

        public void Shake(float amount)
        {
            if (GameSettings.ScreenShake) shake = Mathf.Max(shake, amount);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var arena = Arena.Instance;

            if (TryGetPlayersBounds(out Vector2 min, out Vector2 max))
            {
                Vector2 center = (min + max) * 0.5f;
                float neededSize = Mathf.Max((max.y - min.y) * 0.5f + padding, ((max.x - min.x) * 0.5f + padding) / cam.aspect);
                float targetSize = Mathf.Clamp(neededSize, minSize, maxSize);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, 1f - Mathf.Exp(-zoomSharpness * dt));
                focus = Vector2.Lerp(focus, center, 1f - Mathf.Exp(-followSharpness * dt));
            }
            else if (arena != null)
            {
                focus = Vector2.Lerp(focus, arena.transform.position, 1f - Mathf.Exp(-followSharpness * dt));
            }

            Vector2 pos = arena != null ? ClampToArena(focus, arena.Bounds) : focus;

            if (shake > 0f && Time.timeScale > 0f)
            {
                pos += Random.insideUnitCircle * shake;
                shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            }

            // Caméra alignée sur les pixels de l'écran : évite les lignes noires entre les tuiles et le scintillement
            float pixelWorld = 2f * cam.orthographicSize / Mathf.Max(1, cam.pixelHeight);
            pos.x = Mathf.Round(pos.x / pixelWorld) * pixelWorld;
            pos.y = Mathf.Round(pos.y / pixelWorld) * pixelWorld;
            transform.position = new Vector3(pos.x, pos.y, -10f);
        }

        bool TryGetPlayersBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;

            // On cadre les joueurs vivants et à terre (pour qu'on puisse aller les relever) ; sinon tous les joueurs
            foreach (var p in Player.All)
            {
                if (!p.IsAlive && !p.IsDowned) continue;
                min = Vector2.Min(min, p.Position);
                max = Vector2.Max(max, p.Position);
                any = true;
            }
            if (any) return true;

            foreach (var p in Player.All)
            {
                if (p.Character == null) continue;
                min = Vector2.Min(min, p.Position);
                max = Vector2.Max(max, p.Position);
                any = true;
            }
            return any;
        }

        Vector2 ClampToArena(Vector2 p, Rect bounds)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float xMin = bounds.xMin - arenaMargin + halfW, xMax = bounds.xMax + arenaMargin - halfW;
            float yMin = bounds.yMin - arenaMargin + halfH, yMax = bounds.yMax + arenaMargin - halfH;
            p.x = xMin > xMax ? bounds.center.x : Mathf.Clamp(p.x, xMin, xMax);
            p.y = yMin > yMax ? bounds.center.y : Mathf.Clamp(p.y, yMin, yMax);
            return p;
        }
    }
}
