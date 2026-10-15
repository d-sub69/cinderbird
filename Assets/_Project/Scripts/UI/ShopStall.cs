using Cinderbird.Player;
using UnityEngine;

namespace Cinderbird.UI
{
    /// <summary>The shop stall at the nest: look at it and press the use key (E) to open the shop.</summary>
    public sealed class ShopStall : MonoBehaviour, IInteractable
    {
        [SerializeField] private ShopView _view;

        public string Prompt => "open the shop";

        public void Interact()
        {
            if (_view != null)
            {
                _view.Open();
            }
        }
    }
}
