using UnityEngine;
using UnityEngine.EventSystems;

namespace BrotatoLike
{
    /// <summary>Élément qui affiche une infobulle quand la souris le survole (la sélection manette est gérée par la boutique).</summary>
    public class TooltipSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Élément actuellement survolé par la souris (une seule souris : celle du joueur clavier).</summary>
        public static TooltipSource Hovered { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Hovered = null;

        public void OnPointerEnter(PointerEventData eventData) => Hovered = this;

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Hovered == this) Hovered = null;
        }

        void OnDisable()
        {
            if (Hovered == this) Hovered = null;
        }
    }
}
