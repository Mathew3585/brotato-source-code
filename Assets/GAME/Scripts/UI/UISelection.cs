using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>
    /// Sans souris, un menu sans bouton sélectionné ne réagit à rien : dès qu'un menu est ouvert sans sélection
    /// (au démarrage, après un changement de panneau…), on sélectionne son premier bouton visible.
    /// </summary>
    public class UISelection : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("UISelection");
            DontDestroyOnLoad(go);
            go.AddComponent<UISelection>();
        }

        void LateUpdate()
        {
            // Systèmes des joueurs (boutique, pause, options en jeu) : seulement si un menu leur est attribué
            foreach (var p in Player.All)
                if (p.EventSystem != null && p.EventSystem.playerRoot != null) EnsureSelected(p.EventSystem);

            // Système de la scène (menu principal), seulement hors partie
            var current = EventSystem.current;
            if (current != null && !(current is MultiplayerEventSystem) && Player.All.Count == 0) EnsureSelected(current);
        }

        /// <summary>
        /// Sélectionne le premier bouton visible (le plus haut, puis le plus à gauche) si rien de valide n'est sélectionné.
        /// Renvoie vrai si une sélection a été faite.
        /// </summary>
        public static bool EnsureSelected(EventSystem eventSystem)
        {
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy
                && selected.TryGetComponent<Selectable>(out var s) && s.IsInteractable())
                return false;

            Transform root = eventSystem is MultiplayerEventSystem mes && mes.playerRoot != null ? mes.playerRoot.transform : null;
            Selectable best = null;
            foreach (var candidate in Selectable.allSelectablesArray)
            {
                // Jamais un champ de texte : il capterait le clavier tout seul
                if (!candidate.IsInteractable() || candidate.navigation.mode == Navigation.Mode.None
                    || candidate is TMPro.TMP_InputField) continue;
                if (root != null && !candidate.transform.IsChildOf(root)) continue;
                // Seulement les boutons de la scène : pas ceux de la console de debug (invisible mais active, en DontDestroyOnLoad)
                if (candidate.gameObject.scene != eventSystem.gameObject.scene) continue;
                if (best == null || IsBefore(candidate.transform.position, best.transform.position)) best = candidate;
            }
            if (best == null) return false;
            eventSystem.SetSelectedGameObject(best.gameObject);
            return true;
        }

        static bool IsBefore(Vector3 a, Vector3 b)
        {
            if (Mathf.Abs(a.y - b.y) > 1f) return a.y > b.y;
            return a.x < b.x;
        }
    }
}
