using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Le jeu se joue entièrement au clavier ou à la manette : le curseur de la souris est caché et verrouillé
    /// dans toutes les scènes (réappliqué quand la fenêtre reprend le focus).
    /// </summary>
    public class CursorLock : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("CursorLock");
            DontDestroyOnLoad(go);
            go.AddComponent<CursorLock>();
            Apply();
        }

        static void Apply()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) Apply();
        }
    }
}
