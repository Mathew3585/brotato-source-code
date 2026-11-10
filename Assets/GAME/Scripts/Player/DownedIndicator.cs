using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Au-dessus d'un joueur à terre : jauge rouge (temps avant la mort), jauge verte (réanimation)
    /// et cercle en pointillés qui montre la zone où un coéquipier doit se placer.
    /// Simples sprites du monde (comme Debris), créés à la première utilisation.
    /// </summary>
    public class DownedIndicator : MonoBehaviour
    {
        const int RingDots = 20;
        const float BarWidth = 1f;
        const float BarHeight = 0.1f;
        // Le carré de Debris mesure 0,125 unité de côté
        const float PixelSize = 0.125f;
        const int Order = 29000;

        SpriteRenderer bleedBack, bleedFill, reviveBack, reviveFill;
        readonly SpriteRenderer[] ring = new SpriteRenderer[RingDots];
        float spin;

        public static DownedIndicator Create(Transform parent)
        {
            var go = new GameObject("DownedIndicator");
            go.transform.SetParent(parent, false);
            var self = go.AddComponent<DownedIndicator>();
            self.Build();
            go.SetActive(false);
            return self;
        }

        void Build()
        {
            bleedBack = Bar("BleedBack", new Color(0f, 0f, 0f, 0.7f), 0.85f, Order);
            bleedFill = Bar("BleedFill", new Color(1f, 0.25f, 0.25f), 0.85f, Order + 1);
            reviveBack = Bar("ReviveBack", new Color(0f, 0f, 0f, 0.7f), 0.72f, Order);
            reviveFill = Bar("ReviveFill", new Color(0.35f, 1f, 0.45f), 0.72f, Order + 1);
            for (int i = 0; i < RingDots; i++)
            {
                ring[i] = MakeSprite("RingDot", Order - 1);
                ring[i].transform.localScale = Vector3.one * 0.6f;
            }
        }

        SpriteRenderer Bar(string barName, Color color, float y, int order)
        {
            var sr = MakeSprite(barName, order);
            sr.color = color;
            sr.transform.localPosition = new Vector3(0f, y, 0f);
            sr.transform.localScale = new Vector3(BarWidth / PixelSize, BarHeight / PixelSize, 1f);
            return sr;
        }

        SpriteRenderer MakeSprite(string objectName, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Debris.Pixel;
            sr.sortingOrder = order;
            return sr;
        }

        /// <param name="bleed01">Temps restant avant la mort (1 = plein).</param>
        /// <param name="revive01">Progression de la réanimation (1 = relevé).</param>
        public void Show(float bleed01, float revive01, float radius, Color playerColor)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            SetFill(bleedFill, bleed01);
            // La jauge rouge clignote quand il reste peu de temps
            bleedFill.enabled = bleed01 > 0.3f || Mathf.Repeat(Time.time, 0.25f) > 0.1f;

            bool reviving = revive01 > 0.001f;
            reviveBack.enabled = reviving;
            reviveFill.enabled = reviving;
            SetFill(reviveFill, revive01);

            // Cercle qui tourne lentement, à la couleur du joueur (vert pendant la réanimation)
            spin += Time.deltaTime * (reviving ? 90f : 30f);
            var dotColor = reviving ? new Color(0.35f, 1f, 0.45f, 0.9f) : new Color(playerColor.r, playerColor.g, playerColor.b, 0.75f);
            for (int i = 0; i < RingDots; i++)
            {
                float a = (spin + i * 360f / RingDots) * Mathf.Deg2Rad;
                ring[i].transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                ring[i].color = dotColor;
            }
        }

        static void SetFill(SpriteRenderer fill, float t)
        {
            t = Mathf.Clamp01(t);
            // Remplissage depuis la gauche
            fill.transform.localScale = new Vector3(BarWidth * t / PixelSize, BarHeight / PixelSize, 1f);
            var p = fill.transform.localPosition;
            fill.transform.localPosition = new Vector3(-BarWidth * (1f - t) / 2f, p.y, p.z);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
