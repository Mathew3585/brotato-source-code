using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>Petit son quand un bouton est sélectionné (manette / clavier) ou survolé (souris).</summary>
    [RequireComponent(typeof(Selectable))]
    public class ButtonSound : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnSelect(BaseEventData eventData) => PlayIfInteractable();

        public void OnPointerEnter(PointerEventData eventData) => PlayIfInteractable();

        void PlayIfInteractable()
        {
            if (selectable != null && selectable.IsInteractable()) AudioManager.Play(SoundId.UiSelect);
        }
    }
}
