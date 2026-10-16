using Cinderbird.Core;
using Cinderbird.Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// What the player can tell Sally when they speak to her (E): work on her own (Auto), work a plot they choose, or
    /// stop and wait by the shop. Opens over the game like the shop (time keeps running, the cursor is free); she
    /// stands facing the player until it closes.
    /// </summary>
    public sealed class HelperMenuView : MonoBehaviour
    {
        [SerializeField] private PauseController _pause;
        [SerializeField] private Sally _sally;
        [SerializeField] private HelperAssignController _assign;
        [Tooltip("The whole menu, shown while open.")]
        [SerializeField] private GameObject _menu;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Button _autoButton;
        [SerializeField] private Button _assignButton;
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _closeButton;
        [Tooltip("The use key (E): closes the menu again.")]
        [SerializeField] private InputActionReference _closeAction;

        private int _openedFrame = -1;

        public bool IsOpen => _menu != null && _menu.activeSelf;

        private void Awake()
        {
            if (_pause == null || _sally == null || _assign == null || _menu == null || _status == null || _autoButton == null ||
                _assignButton == null || _homeButton == null || _closeButton == null)
            {
                Debug.LogError($"{nameof(HelperMenuView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _menu.SetActive(false);
            _autoButton.onClick.AddListener(OnAuto);
            _assignButton.onClick.AddListener(OnAssign);
            _homeButton.onClick.AddListener(OnHome);
            _closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (_sally == null)
            {
                return;
            }

            _sally.TalkRequested += Open;
            _pause.PhaseChanged += OnPhaseChanged;
            _pause.BackPressed += OnBackPressed;
            if (_closeAction != null)
            {
                _closeAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_sally == null)
            {
                return;
            }

            _sally.TalkRequested -= Open;
            _pause.PhaseChanged -= OnPhaseChanged;
            _pause.BackPressed -= OnBackPressed;
        }

        private void Update()
        {
            if (IsOpen && _closeAction != null && Time.frameCount != _openedFrame && _closeAction.action.WasPressedThisFrame())
            {
                Close();
            }
        }

        private void Open(Sally sally)
        {
            if (IsOpen || !_pause.OpenMenu())
            {
                sally.EndTalk();
                return;
            }

            _menu.SetActive(true);
            // The key press that opened it must not close it again this same frame.
            _openedFrame = Time.frameCount;
            _status.text = Describe(sally);
        }

        private void Close()
        {
            if (!Hide())
            {
                return;
            }

            _pause.CloseMenu();
            _sally.EndTalk();
        }

        private bool Hide()
        {
            if (!IsOpen)
            {
                return false;
            }

            _menu.SetActive(false);
            return true;
        }

        private void OnAuto()
        {
            _sally.SetMode(HelperMode.Auto);
            Close();
        }

        private void OnHome()
        {
            _sally.SetMode(HelperMode.Off);
            Close();
        }

        // The menu closes and the player points at where to send her; she keeps waiting until that is done.
        private void OnAssign()
        {
            if (!Hide())
            {
                return;
            }

            _pause.CloseMenu();
            _assign.Begin(_sally);
        }

        private static string Describe(Sally sally)
        {
            string layer = sally.WorkLayer >= 0 ? $" in the {sally.Pile.LayerName(sally.WorkLayer)}" : string.Empty;
            string mode = sally.Mode switch
            {
                HelperMode.Assigned => $"Working from the spot you chose{layer}",
                HelperMode.Off => "Resting by the shop",
                _ => $"Working on her own{layer}",
            };
            string reason = sally.CantWorkReason.Length > 0 && sally.Mode != HelperMode.Off ? $"\nCan't work: {sally.CantWorkReason}" : string.Empty;
            return $"{mode}{reason}\nSieve: {sally.Sieve.Contents.EmberCount} / {sally.Sieve.Contents.EmberCapacity} embers";
        }

        // Pausing (focus loss) closes the menu; she goes back to work.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.InMenu && Hide())
            {
                _sally.EndTalk();
            }
        }

        private void OnBackPressed()
        {
            Close();
        }
    }
}
