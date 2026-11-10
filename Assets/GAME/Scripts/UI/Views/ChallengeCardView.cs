using UnityEngine;
using UnityEngine.UI;

namespace BrotatoLike
{
    /// <summary>Carte d'un défi (prefab ChallengeCard) : icône de la récompense, ou gros cadenas tant que le défi n'est pas réussi.</summary>
    public class ChallengeCardView : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public Image lockIcon;

        public void Show(ChallengeSet.Challenge challenge, bool done)
        {
            icon.sprite = challenge.reward != null ? challenge.reward.Icon : null;
            icon.enabled = done && icon.sprite != null;
            lockIcon.enabled = !done;
        }
    }
}
