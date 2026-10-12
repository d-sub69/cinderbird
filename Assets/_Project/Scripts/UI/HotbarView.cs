using Cinderbird.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// The row of tool slots at the bottom of the screen: slot number and tool name, active slot highlighted.
    /// Redraws only when the inventory changes.
    /// </summary>
    public sealed class HotbarView : MonoBehaviour
    {
        [SerializeField] private ToolBelt _toolBelt;
        [Tooltip("One background per slot, in slot order.")]
        [SerializeField] private Image[] _slotBackgrounds;
        [Tooltip("One label per slot, in slot order.")]
        [SerializeField] private TMP_Text[] _slotLabels;
        [SerializeField] private Color _slotColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color _activeSlotColor = new Color(1f, 1f, 1f, 0.35f);

        private void Awake()
        {
            if (_toolBelt == null || _slotBackgrounds == null || _slotLabels == null)
            {
                Debug.LogError($"{nameof(HotbarView)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_toolBelt == null)
            {
                return;
            }

            _toolBelt.Inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_toolBelt == null)
            {
                return;
            }

            _toolBelt.Inventory.Changed -= Refresh;
        }

        private void Refresh()
        {
            Inventory<ToolDefinition> inventory = _toolBelt.Inventory;
            for (int slot = 0; slot < _slotBackgrounds.Length; slot++)
            {
                bool exists = slot < inventory.Capacity;
                _slotBackgrounds[slot].gameObject.SetActive(exists);
                if (!exists)
                {
                    continue;
                }

                _slotBackgrounds[slot].color = slot == inventory.ActiveSlot ? _activeSlotColor : _slotColor;
                if (slot < _slotLabels.Length && _slotLabels[slot] != null)
                {
                    ToolDefinition tool = inventory[slot];
                    // Allocates a small string, but only when the inventory changes.
                    _slotLabels[slot].text = tool != null ? $"{slot + 1}\n{tool.DisplayName}" : $"{slot + 1}";
                }
            }
        }
    }
}
