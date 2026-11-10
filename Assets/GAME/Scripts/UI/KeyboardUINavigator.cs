using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Navigation au clavier dans les menus d'un joueur (boutique, pause, options) : lit directement le clavier
    /// (W/A/S/D ou ZQSD selon la disposition, flèches, Entrée / Espace) et pilote SON système d'événements.
    /// Utilisé pour le joueur clavier en coop locale, à la place de la navigation du module d'entrée.
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public class KeyboardUINavigator : MonoBehaviour
    {
        const float RepeatDelay = 0.4f;
        const float RepeatRate = 0.1f;

        EventSystem eventSystem;
        Vector2 lastDirection;
        float repeatTimer;

        void Awake() => eventSystem = GetComponent<EventSystem>();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            // Déplacement (touches physiques : W = Z sur un clavier AZERTY)
            Vector2 direction = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) direction = Vector2.up;
            else if (kb.sKey.isPressed || kb.downArrowKey.isPressed) direction = Vector2.down;
            else if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) direction = Vector2.left;
            else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) direction = Vector2.right;
            bool submit = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;

            // Rien de sélectionné : la première touche sélectionne le premier bouton du menu (sans bouger ni valider)
            bool hasMenu = !(eventSystem is MultiplayerEventSystem mes) || mes.playerRoot != null;
            if ((direction != lastDirection && direction != Vector2.zero) || submit)
            {
                if (hasMenu && UISelection.EnsureSelected(eventSystem))
                {
                    lastDirection = direction;
                    repeatTimer = RepeatDelay;
                    return;
                }
            }

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy)
            {
                lastDirection = direction;
                return;
            }

            if (direction != lastDirection)
            {
                lastDirection = direction;
                repeatTimer = RepeatDelay;
                if (direction != Vector2.zero) Move(selected, direction);
            }
            else if (direction != Vector2.zero)
            {
                repeatTimer -= Time.unscaledDeltaTime;
                if (repeatTimer <= 0f)
                {
                    repeatTimer = RepeatRate;
                    Move(selected, direction);
                }
            }

            // Validation
            if (submit)
                ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        }

        void Move(GameObject selected, Vector2 direction)
        {
            var data = new AxisEventData(eventSystem)
            {
                moveVector = direction,
                moveDir = direction.y > 0 ? MoveDirection.Up
                    : direction.y < 0 ? MoveDirection.Down
                    : direction.x < 0 ? MoveDirection.Left
                    : MoveDirection.Right,
            };
            ExecuteEvents.Execute(selected, data, ExecuteEvents.moveHandler);
        }
    }
}
