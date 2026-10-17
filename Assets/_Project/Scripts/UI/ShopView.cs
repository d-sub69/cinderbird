using System;
using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// The shop panel: one row per upgrade on sale, the Glow to spend, Buy buttons. Opening it frees the cursor but
    /// doesn't stop the game (<see cref="GamePhase.InMenu"/>). Closed with its button, the use key (E) or Esc.
    /// </summary>
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private PauseController _pause;
        [SerializeField] private Upgrades _upgrades;
        [Tooltip("The whole shop UI, shown while open.")]
        [SerializeField] private GameObject _menu;
        [Tooltip("Rows are created under this, one per upgrade.")]
        [SerializeField] private Transform _rowParent;
        [Tooltip("An inactive row used as the template for every upgrade.")]
        [SerializeField] private ShopRowView _rowTemplate;
        [SerializeField] private TMP_Text _glowLabel;
        [SerializeField] private Button _closeButton;
        [Tooltip("Optional: the tab with the tools (sieves, mesh, basket, helpers). Without tabs every upgrade is listed.")]
        [SerializeField] private Button _toolsTab;
        [Tooltip("Optional: the tab with the powers; shown once a power has come.")]
        [SerializeField] private Button _powersTab;
        [SerializeField] private Color _tabColor = new Color(0.55f, 0.5f, 0.45f, 1f);
        [SerializeField] private Color _selectedTabColor = new Color(0.93f, 0.9f, 0.86f, 1f);
        [Tooltip("The use key (E): closes the shop again.")]
        [SerializeField] private InputActionReference _closeAction;

        private readonly List<ShopRowView> _rows = new List<ShopRowView>();
        private int _openedFrame = -1;
        private int _closedFrame = -1;
        private UpgradeCategory _tab = UpgradeCategory.Tools;

        /// <summary>Fired after a purchase with the upgrade and its new level (for player feedback).</summary>
        public event Action<UpgradeDefinition, int> Purchased;

        public bool IsOpen => _menu != null && _menu.activeSelf;

        private void Awake()
        {
            if (_pause == null || _upgrades == null || _menu == null || _rowParent == null || _rowTemplate == null ||
                _glowLabel == null || _closeButton == null)
            {
                Debug.LogError($"{nameof(ShopView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _menu.SetActive(false);
            _rowTemplate.gameObject.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            if (_toolsTab != null && _powersTab != null)
            {
                _toolsTab.onClick.AddListener(() => ShowTab(UpgradeCategory.Tools));
                _powersTab.onClick.AddListener(() => ShowTab(UpgradeCategory.Powers));
            }
            foreach (UpgradeDefinition upgrade in _upgrades.Catalog)
            {
                if (upgrade == null)
                {
                    continue;
                }

                ShopRowView row = Instantiate(_rowTemplate, _rowParent);
                row.name = upgrade.DisplayName;
                row.gameObject.SetActive(true);
                row.Bind(upgrade, _upgrades, Buy);
                _rows.Add(row);
            }
        }

        private void OnEnable()
        {
            _pause.PhaseChanged += OnPhaseChanged;
            _pause.BackPressed += OnBackPressed;
            _upgrades.Levels.Changed += OnLevelChanged;
            _upgrades.Wallet.Currencies.Changed += Refresh;
            if (_upgrades.Shop != null)
            {
                _upgrades.Shop.AvailabilityChanged += Refresh;
            }
            if (_closeAction != null)
            {
                _closeAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            _pause.PhaseChanged -= OnPhaseChanged;
            _pause.BackPressed -= OnBackPressed;
            _upgrades.Levels.Changed -= OnLevelChanged;
            _upgrades.Wallet.Currencies.Changed -= Refresh;
            if (_upgrades.Shop != null)
            {
                _upgrades.Shop.AvailabilityChanged -= Refresh;
            }
        }

        /// <summary>Opens the shop if the game is being played (not paused, not in another menu).</summary>
        public void Open()
        {
            // The key press that closed it (E, while still looking at the stall) must not open it again this same frame.
            if (IsOpen || Time.frameCount == _closedFrame || !_pause.OpenMenu())
            {
                return;
            }

            _menu.SetActive(true);
            // The key press that opened it must not close it again this same frame.
            _openedFrame = Time.frameCount;
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _menu.SetActive(false);
            _closedFrame = Time.frameCount;
            _pause.CloseMenu();
        }

        private void Update()
        {
            if (IsOpen && _closeAction != null && Time.frameCount != _openedFrame && _closeAction.action.WasPressedThisFrame())
            {
                Close();
            }
        }

        private void Buy(UpgradeDefinition upgrade)
        {
            if (_upgrades.Shop.TryBuy(upgrade) == Purchase.Bought)
            {
                Purchased?.Invoke(upgrade, _upgrades.LevelOf(upgrade));
            }

            Refresh();
        }

        private void OnLevelChanged(string id)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            _glowLabel.text = $"Glow {_upgrades.Wallet.Currencies.Glow:N0}";
            Shop shop = _upgrades.Shop;
            bool tabs = _toolsTab != null && _powersTab != null;
            if (tabs)
            {
                RefreshTabs(shop);
            }

            foreach (ShopRowView row in _rows)
            {
                // Only what is on sale (a helper's upgrades once she is hired, a power's once it has come), on this tab.
                bool shown = shop.IsAvailable(row.Upgrade) && (!tabs || row.Upgrade.Category == _tab);
                if (row.gameObject.activeSelf != shown)
                {
                    row.gameObject.SetActive(shown);
                }

                if (shown)
                {
                    row.Refresh();
                }
            }
        }

        private void ShowTab(UpgradeCategory tab)
        {
            _tab = tab;
            Refresh();
        }

        // The Powers tab shows once a power has come (any of its upgrades is on sale); the open tab is highlighted.
        private void RefreshTabs(Shop shop)
        {
            bool anyPower = false;
            foreach (ShopRowView row in _rows)
            {
                anyPower |= row.Upgrade.Category == UpgradeCategory.Powers && shop.IsAvailable(row.Upgrade);
            }

            _powersTab.gameObject.SetActive(anyPower);
            if (!anyPower)
            {
                _tab = UpgradeCategory.Tools;
            }

            _toolsTab.image.color = _tab == UpgradeCategory.Tools ? _selectedTabColor : _tabColor;
            _powersTab.image.color = _tab == UpgradeCategory.Powers ? _selectedTabColor : _tabColor;
        }

        // Pausing (Esc is handled below, focus loss here) closes the shop; it reopens from the stall.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.InMenu && IsOpen)
            {
                _menu.SetActive(false);
            }
        }

        private void OnBackPressed()
        {
            if (IsOpen)
            {
                Close();
            }
        }
    }
}
