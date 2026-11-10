using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Flèches au bord de l'écran qui pointent vers le boss et les élites quand ils sont hors champ.
    /// Les flèches sont dans la scène (sous le HUD) ; ce script les place et les oriente.
    /// </summary>
    public class OffscreenIndicators : MonoBehaviour
    {
        [SerializeField] RectTransform canvasRect;
        [Tooltip("Flèches disponibles (une par cible suivie, les autres sont masquées)")]
        [SerializeField] Image[] arrows;
        [Tooltip("Distance au bord de l'écran, en pixels de référence (1920x1080)")]
        [SerializeField] float margin = 50f;
        [SerializeField] Color bossColor = new(1f, 0.25f, 0.25f);
        [SerializeField] Color eliteColor = new(1f, 0.7f, 0.2f);

        void LateUpdate()
        {
            var cam = Camera.main;
            var gm = GameManager.Instance;
            int used = 0;

            if (cam != null && gm != null && gm.State == GameState.Wave && canvasRect.gameObject.activeInHierarchy)
            {
                Vector2 size = canvasRect.rect.size;
                foreach (var enemy in Enemy.Active)
                {
                    if (used >= arrows.Length) break;
                    if (!enemy.IsAlive || !(enemy.Data.isBoss || enemy.Data.isElite)) continue;

                    Vector2 vp = cam.WorldToViewportPoint(enemy.Position);
                    if (vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) continue; // visible : pas de flèche

                    // Point sur le bord de l'écran dans la direction de l'ennemi
                    Vector2 fromCenter = (vp - new Vector2(0.5f, 0.5f)) * size;
                    Vector2 half = size * 0.5f - Vector2.one * margin;
                    float k = Mathf.Max(Mathf.Abs(fromCenter.x) / half.x, Mathf.Abs(fromCenter.y) / half.y);
                    Vector2 edge = fromCenter / Mathf.Max(k, 0.0001f);

                    var arrow = arrows[used++];
                    arrow.gameObject.SetActive(true);
                    arrow.rectTransform.anchoredPosition = edge;
                    float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg - 90f; // sprite pointé vers le haut
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 8f);
                    arrow.rectTransform.localScale = Vector3.one * (enemy.Data.isBoss ? 1.4f * pulse : pulse);
                    arrow.color = enemy.Data.isBoss ? bossColor : eliteColor;
                }
            }

            for (int i = used; i < arrows.Length; i++)
                if (arrows[i].gameObject.activeSelf) arrows[i].gameObject.SetActive(false);
        }
    }
}
