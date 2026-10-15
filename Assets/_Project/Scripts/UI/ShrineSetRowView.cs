using Cinderbird.Offerings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// One shelf set in the shrine panel: how many of its offerings are on the shelf, its bonus, and whether it's the one
    /// giving it; a complete set that isn't active can be made active (only so many sets give their bonus at once).
    /// </summary>
    public sealed class ShrineSetRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Button _activateButton;
        [SerializeField] private TMP_Text _activateLabel;

        private Shrine _shrine;
        private OfferingSet _set;

        public void Bind(Shrine shrine, OfferingSet set)
        {
            _shrine = shrine;
            _set = set;
            _activateButton.onClick.RemoveAllListeners();
            _activateButton.onClick.AddListener(() => _shrine.Activate(_set));
        }

        public void Refresh()
        {
            if (_set == null)
            {
                return;
            }

            bool complete = _shrine.IsComplete(_set);
            bool active = _shrine.IsActive(_set);
            _label.text = $"{_set.DisplayName}: {_shrine.CountOnShelf(_set)}/{_set.Members.Length} on the shelf. {_set.DescribeBonus()}";
            _activateButton.gameObject.SetActive(complete);
            _activateButton.interactable = !active;
            _activateLabel.text = active ? "Active" : "Activate";
        }
    }
}
