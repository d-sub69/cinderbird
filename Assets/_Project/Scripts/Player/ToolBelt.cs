using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Player
{
    /// <summary>
    /// Thin adapter around the player's tool <see cref="Inventory{T}"/>: number keys and the mouse wheel pick
    /// the active tool, and only that tool is shown in the hands. Switching lowers the hands out of view,
    /// swaps the tool, and raises them again.
    /// </summary>
    public sealed class ToolBelt : MonoBehaviour
    {
        [SerializeField] private ToolBeltSettings _settings;
        [Tooltip("Parent of all held tools under the camera; moved down and up during a swap.")]
        [SerializeField] private Transform _hands;
        [Tooltip("Starting tools, in slot order, with the objects that show them in hand.")]
        [SerializeField] private HeldToolBinding[] _tools;
        [Tooltip("One action per slot (1, 2, 3, …).")]
        [SerializeField] private InputActionReference[] _slotActions;
        [Tooltip("Mouse wheel: up selects the previous tool, down the next.")]
        [SerializeField] private InputActionReference _cycleAction;

        private Inventory<ToolDefinition> _inventory;
        private IEmberHolder[] _holders;
        private ToolDefinition _shownTool;
        private Vector3 _handsRestPosition;
        private float _swapStartTime = -1f;
        private bool _swappedThisSwap;

        /// <summary>Created on first access: other objects' OnEnable can run before our Awake.</summary>
        public Inventory<ToolDefinition> Inventory
        {
            get
            {
                if (_inventory == null)
                {
                    CreateInventory();
                }

                return _inventory;
            }
        }

        /// <summary>The tool in hand for gameplay purposes; null for empty hands. Visuals may lag during a swap.</summary>
        public ToolDefinition ActiveTool => Inventory.Active;

        public GameObject ActiveHeldObject
        {
            get
            {
                int index = BindingIndexOf(ActiveTool);
                return index >= 0 ? _tools[index].HeldObject : null;
            }
        }

        /// <summary>Embers carried by the tool in hand, if it carries any.</summary>
        public IEmberSource ActiveSource
        {
            get
            {
                int index = BindingIndexOf(ActiveTool);
                return index >= 0 && _holders != null && _holders[index] != null ? _holders[index].Source : null;
            }
        }

        /// <summary>Takes a tool out of the inventory, e.g. when it's set down in the world.</summary>
        public bool TryRelease(ToolDefinition tool)
        {
            return Inventory.Remove(tool);
        }

        /// <summary>Puts a tool back into the first free slot and makes it the tool in hand.</summary>
        public bool TryTakeBack(ToolDefinition tool)
        {
            if (!Inventory.TryAdd(tool, out int slot))
            {
                return false;
            }

            Inventory.Select(slot);
            return true;
        }

        /// <summary>Saved name of a tool. Asset names are stable as long as the tool assets aren't renamed.</summary>
        public static string IdOf(ToolDefinition tool)
        {
            return tool != null ? tool.name : string.Empty;
        }

        /// <summary>The starting tool with a saved name, or null if this belt doesn't have it.</summary>
        public ToolDefinition FindTool(string id)
        {
            if (string.IsNullOrEmpty(id) || _tools == null)
            {
                return null;
            }

            foreach (HeldToolBinding binding in _tools)
            {
                if (binding.Tool != null && binding.Tool.name == id)
                {
                    return binding.Tool;
                }
            }

            return null;
        }

        /// <summary>
        /// Replaces the inventory with saved slots and shows the active tool at once (no swap animation).
        /// Starting tools that are neither in the slots nor in <paramref name="outInWorld"/> go back into a free
        /// slot, so a tool added after the save was made is never lost.
        /// </summary>
        public void RestoreSlots(IReadOnlyList<ToolDefinition> slots, int activeSlot, ICollection<ToolDefinition> outInWorld)
        {
            if (_tools == null)
            {
                return;
            }

            Inventory.Restore(slots, activeSlot);
            foreach (HeldToolBinding binding in _tools)
            {
                if (binding.Tool != null && Inventory.IndexOf(binding.Tool) < 0 && !outInWorld.Contains(binding.Tool))
                {
                    Inventory.TryAdd(binding.Tool, out _);
                }
            }

            _swapStartTime = -1f;
            if (_hands != null)
            {
                _hands.localPosition = _handsRestPosition;
            }

            ShowOnly(Inventory.Active);
        }

        private void Awake()
        {
            if (_settings == null || _hands == null || _tools == null || _tools.Length == 0)
            {
                Debug.LogError($"{nameof(ToolBelt)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _handsRestPosition = _hands.localPosition;
            _holders = new IEmberHolder[_tools.Length];
            for (int i = 0; i < _tools.Length; i++)
            {
                GameObject held = _tools[i].HeldObject;
                _holders[i] = held != null ? held.GetComponent<IEmberHolder>() : null;
            }

            _shownTool = Inventory.Active;
            ShowOnly(_shownTool);
            Inventory.Changed += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
            }
        }

        private void OnEnable()
        {
            if (_slotActions != null)
            {
                foreach (InputActionReference action in _slotActions)
                {
                    if (action != null)
                    {
                        action.action.Enable();
                    }
                }
            }

            if (_cycleAction != null)
            {
                _cycleAction.action.Enable();
            }
        }

        private void Update()
        {
            HandleInput();
            AnimateSwap();
        }

        private void CreateInventory()
        {
            int capacity = _settings != null ? _settings.Capacity : 1;
            _inventory = new Inventory<ToolDefinition>(capacity);
            if (_tools == null)
            {
                return;
            }

            foreach (HeldToolBinding binding in _tools)
            {
                if (binding.Tool != null && !_inventory.TryAdd(binding.Tool, out _))
                {
                    Debug.LogWarning($"No free slot for {binding.Tool.DisplayName}.", this);
                }
            }
        }

        private void HandleInput()
        {
            // While the cursor is free, keys and wheel belong to the Editor.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            if (_slotActions != null)
            {
                for (int slot = 0; slot < _slotActions.Length; slot++)
                {
                    InputActionReference action = _slotActions[slot];
                    if (action != null && action.action.WasPressedThisFrame())
                    {
                        Inventory.Select(slot);
                    }
                }
            }

            if (_cycleAction == null)
            {
                return;
            }

            // Only the sign matters: wheel step sizes differ between mice and platforms.
            float scroll = _cycleAction.action.ReadValue<float>();
            if (scroll > 0f)
            {
                Inventory.Cycle(-1);
            }
            else if (scroll < 0f)
            {
                Inventory.Cycle(1);
            }
        }

        private void OnInventoryChanged()
        {
            if (Inventory.Active == _shownTool || _swapStartTime >= 0f)
            {
                return;
            }

            if (_settings.SwapDuration <= 0f)
            {
                ShowOnly(Inventory.Active);
                return;
            }

            _swapStartTime = Time.time;
            _swappedThisSwap = false;
        }

        private void AnimateSwap()
        {
            if (_swapStartTime < 0f)
            {
                return;
            }

            float progress = (Time.time - _swapStartTime) / _settings.SwapDuration;
            float lowered;
            if (progress < 0.5f)
            {
                lowered = Mathf.SmoothStep(0f, 1f, progress * 2f);
            }
            else
            {
                // Swap at the bottom, out of view; shows whatever is active now, even if the player kept switching.
                if (!_swappedThisSwap)
                {
                    ShowOnly(Inventory.Active);
                    _swappedThisSwap = true;
                }

                lowered = Mathf.SmoothStep(1f, 0f, (progress - 0.5f) * 2f);
            }

            if (progress >= 1f)
            {
                lowered = 0f;
                _swapStartTime = -1f;
            }

            _hands.localPosition = _handsRestPosition + Vector3.down * (lowered * _settings.LowerDistance);

            // The player switched again after the swap point: run another swap.
            if (_swapStartTime < 0f && Inventory.Active != _shownTool)
            {
                OnInventoryChanged();
            }
        }

        private void ShowOnly(ToolDefinition tool)
        {
            _shownTool = tool;
            foreach (HeldToolBinding binding in _tools)
            {
                // A tool that isn't in the inventory is out in the world (placed); leave it alone.
                if (binding.HeldObject == null || Inventory.IndexOf(binding.Tool) < 0)
                {
                    continue;
                }

                binding.HeldObject.SetActive(binding.Tool == tool && tool != null);
            }
        }

        private int BindingIndexOf(ToolDefinition tool)
        {
            if (tool == null || _tools == null)
            {
                return -1;
            }

            for (int i = 0; i < _tools.Length; i++)
            {
                if (_tools[i].Tool == tool)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
