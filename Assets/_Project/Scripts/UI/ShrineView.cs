using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Offerings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// The shrine panel: every offering (picked-up ones with their lore), looking at one closer, placing them on the
    /// shelf, selling and buying back, and the sets: how far each is and which one gives its bonus. Like the shop, opening
    /// it frees the cursor without stopping the game (<see cref="GamePhase.InMenu"/>); closed with its button, the use
    /// key (E) or Esc.
    /// </summary>
    public sealed class ShrineView : MonoBehaviour
    {
        [SerializeField] private PauseController _pause;
        [SerializeField] private Shrine _shrine;
        [Tooltip("Shows an offering up close (Look closer).")]
        [SerializeField] private OfferingInspectView _inspect;
        [Tooltip("The whole shrine UI, shown while open.")]
        [SerializeField] private GameObject _menu;
        [Tooltip("Rows are created under this, one per offering.")]
        [SerializeField] private Transform _rowParent;
        [Tooltip("An inactive row used as the template for every offering.")]
        [SerializeField] private ShrineRowView _rowTemplate;
        [Tooltip("Set rows are created under this, one per set.")]
        [SerializeField] private Transform _setParent;
        [Tooltip("An inactive row used as the template for every set.")]
        [SerializeField] private ShrineSetRowView _setTemplate;
        [Tooltip("Says how many sets can give their bonus at once.")]
        [SerializeField] private TMP_Text _setsHeading;
        [SerializeField] private TMP_Text _glowLabel;
        [SerializeField] private Button _closeButton;
        [Tooltip("The use key (E): closes the panel again.")]
        [SerializeField] private InputActionReference _closeAction;

        private readonly List<ShrineRowView> _rows = new List<ShrineRowView>();
        private readonly List<ShrineSetRowView> _setRows = new List<ShrineSetRowView>();
        private int _openedFrame = -1;
        private int _closedFrame = -1;
        // While an offering is looked at closer, the panel is hidden but still open (the game stays in the menu).
        private bool _inspecting;
        private int _resumedFrame = -1;

        public bool IsOpen => _menu != null && (_menu.activeSelf || _inspecting);

        private void Awake()
        {
            if (_pause == null || _shrine == null || _inspect == null || _menu == null || _rowParent == null || _rowTemplate == null ||
                _setParent == null || _setTemplate == null || _setsHeading == null || _glowLabel == null || _closeButton == null)
            {
                Debug.LogError($"{nameof(ShrineView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _menu.SetActive(false);
            _rowTemplate.gameObject.SetActive(false);
            _setTemplate.gameObject.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            for (int i = 0; i < _shrine.Catalog.Count; i++)
            {
                ShrineRowView row = Instantiate(_rowTemplate, _rowParent);
                row.name = $"Offering {i + 1}";
                row.gameObject.SetActive(true);
                row.Bind(_shrine, i, Refresh, LookCloser);
                _rows.Add(row);
            }

            foreach (OfferingSet set in _shrine.Catalog.Sets)
            {
                if (set == null)
                {
                    continue;
                }

                ShrineSetRowView row = Instantiate(_setTemplate, _setParent);
                row.name = set.DisplayName;
                row.gameObject.SetActive(true);
                row.Bind(_shrine, set);
                _setRows.Add(row);
            }
        }

        private void OnEnable()
        {
            _pause.PhaseChanged += OnPhaseChanged;
            _pause.BackPressed += OnBackPressed;
            _shrine.Collection.Changed += OnCollectionChanged;
            _shrine.SetsChanged += Refresh;
            _shrine.Wallet.Currencies.Changed += Refresh;
            if (_closeAction != null)
            {
                _closeAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            _pause.PhaseChanged -= OnPhaseChanged;
            _pause.BackPressed -= OnBackPressed;
            _shrine.Collection.Changed -= OnCollectionChanged;
            _shrine.SetsChanged -= Refresh;
            _shrine.Wallet.Currencies.Changed -= Refresh;
        }

        public void Open()
        {
            // The key press that closed it (E, while still looking at the shelf) must not open it again this frame.
            if (IsOpen || Time.frameCount == _closedFrame || !_pause.OpenMenu())
            {
                return;
            }

            _menu.SetActive(true);
            _openedFrame = Time.frameCount;
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen || _inspecting)
            {
                return;
            }

            _menu.SetActive(false);
            _closedFrame = Time.frameCount;
            _pause.CloseMenu();
        }

        private void Update()
        {
            if (IsOpen && !_inspecting && _closeAction != null && Time.frameCount != _openedFrame &&
                Time.frameCount != _resumedFrame && _closeAction.action.WasPressedThisFrame())
            {
                Close();
            }
        }

        private void LookCloser(int index)
        {
            if (_inspecting || _inspect.IsOpen)
            {
                return;
            }

            _inspecting = true;
            _menu.SetActive(false);
            _inspect.InspectOwned(index, OnInspectClosed);
        }

        private void OnInspectClosed()
        {
            _inspecting = false;
            // The Esc or button that closed the closer look mustn't also close the panel this frame.
            _resumedFrame = Time.frameCount;
            if (_pause.Phase == GamePhase.InMenu)
            {
                _menu.SetActive(true);
                Refresh();
            }
        }

        private void OnCollectionChanged(int index)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!_menu.activeSelf)
            {
                return;
            }

            _glowLabel.text = $"Glow {_shrine.Wallet.Currencies.Glow:N0}";
            foreach (ShrineRowView row in _rows)
            {
                row.Refresh();
            }

            int slots = _shrine.ActiveSetSlots;
            _setsHeading.text = slots == 1
                ? "Sets on the shelf (one set gives its bonus at a time)"
                : $"Sets on the shelf ({slots} sets give their bonus at a time)";
            foreach (ShrineSetRowView row in _setRows)
            {
                row.Refresh();
            }
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.InMenu && _menu.activeSelf)
            {
                _menu.SetActive(false);
            }
        }

        private void OnBackPressed()
        {
            if (IsOpen && !_inspecting && Time.frameCount != _resumedFrame)
            {
                Close();
            }
        }
    }
}
