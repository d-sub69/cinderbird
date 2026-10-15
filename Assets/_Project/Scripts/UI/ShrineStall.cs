using Cinderbird.Player;
using UnityEngine;

namespace Cinderbird.UI
{
    /// <summary>The shrine shelf at the nest: look at it and press the use key (E) to open the shrine panel.</summary>
    public sealed class ShrineStall : MonoBehaviour, IInteractable
    {
        [SerializeField] private ShrineView _view;

        public string Prompt => "visit the shrine";

        public void Interact()
        {
            if (_view != null)
            {
                _view.Open();
            }
        }
    }
}
