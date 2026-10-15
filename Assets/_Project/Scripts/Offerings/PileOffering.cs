using Cinderbird.Player;
using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// An uncovered offering lying on the pile, as the player sees it: look at it within reach and press the use key (E)
    /// to pick it up. Added by <see cref="OfferingFinder"/> to the offering's model when it shows it.
    /// </summary>
    public sealed class PileOffering : MonoBehaviour, IInteractable
    {
        private OfferingFinder _finder;
        private int _index;
        private string _prompt;

        public int Index => _index;

        public string Prompt => _prompt;

        public void Initialize(OfferingFinder finder, int index, string displayName)
        {
            _finder = finder;
            _index = index;
            _prompt = $"pick up the {displayName.ToLowerInvariant()}";
        }

        public void Interact()
        {
            if (_finder != null)
            {
                _finder.PickUp(_index);
            }
        }
    }
}
