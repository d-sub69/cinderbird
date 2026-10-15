using Cinderbird.Core;
using Cinderbird.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// The pause menu: shown while paused, with Resume, Options, New game (after a confirmation) and
    /// Save &amp; Quit. Esc closes the confirmation first, then resumes.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private PauseController _pause;
        [SerializeField] private SaveController _save;
        [Tooltip("The menu panel; hidden while playing.")]
        [SerializeField] private GameObject _menu;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _quitButton;

        [Header("New game confirmation")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private Button _confirmYesButton;
        [SerializeField] private Button _confirmNoButton;

        [Header("Options")]
        [Tooltip("Optional: the options panel. Without it the Options button is greyed out.")]
        [SerializeField] private GameObject _optionsPanel;

        private bool _ready;

        private void Awake()
        {
            if (_pause == null || _save == null || _menu == null || _resumeButton == null || _optionsButton == null ||
                _newGameButton == null || _quitButton == null || _confirmPanel == null || _confirmYesButton == null ||
                _confirmNoButton == null)
            {
                Debug.LogError($"{nameof(PauseMenuView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _resumeButton.onClick.AddListener(_pause.Resume);
            _optionsButton.onClick.AddListener(OpenOptions);
            _optionsButton.interactable = _optionsPanel != null;
            _newGameButton.onClick.AddListener(() => ShowConfirm(true));
            _confirmNoButton.onClick.AddListener(() => ShowConfirm(false));
            _confirmYesButton.onClick.AddListener(_save.StartNewGame);
            _quitButton.onClick.AddListener(_save.SaveAndQuit);
            _ready = true;
        }

        private void OnEnable()
        {
            if (!_ready)
            {
                return;
            }

            _pause.PhaseChanged += OnPhaseChanged;
            _pause.BackPressed += OnBackPressed;
            OnPhaseChanged(_pause.Phase);
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            _pause.PhaseChanged -= OnPhaseChanged;
            _pause.BackPressed -= OnBackPressed;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            bool paused = phase == GamePhase.Paused;
            _menu.SetActive(paused);
            ShowConfirm(false);
            ShowOptions(false);
        }

        private void OnBackPressed()
        {
            // Esc in a menu in the world (the shop) is that menu's to handle.
            if (_pause.Phase != GamePhase.Paused)
            {
                return;
            }

            if (_confirmPanel.activeSelf)
            {
                ShowConfirm(false);
            }
            else if (_optionsPanel != null && _optionsPanel.activeSelf)
            {
                ShowOptions(false);
            }
            else
            {
                _pause.Resume();
            }
        }

        private void OpenOptions()
        {
            ShowOptions(true);
        }

        private void ShowConfirm(bool show)
        {
            _confirmPanel.SetActive(show);
        }

        private void ShowOptions(bool show)
        {
            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(show);
            }
        }
    }
}
