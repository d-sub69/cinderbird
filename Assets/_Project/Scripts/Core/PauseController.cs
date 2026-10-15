using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Core
{
    /// <summary>
    /// Thin adapter around <see cref="PauseState"/>: freezes time while loading or paused, owns the cursor
    /// (locked while playing, free otherwise, also in a menu in the world such as the shop) and reads the pause key. Gameplay input already ignores keys
    /// while the cursor is free, so pausing needs no input-map switching.
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        [Tooltip("Esc: pauses while playing; while paused it's passed to the menu as 'back'.")]
        [SerializeField] private InputActionReference _pauseAction;

        public PauseState State { get; } = new PauseState();

        public GamePhase Phase => State.Phase;

        public bool IsPaused => State.Phase == GamePhase.Paused;

        /// <summary>Fired when the phase changes (loading finished, paused, resumed).</summary>
        public event Action<GamePhase> PhaseChanged;

        /// <summary>The pause key while paused or in a menu: the menu closes a sub-panel, resumes, or closes itself.</summary>
        public event Action BackPressed;

        public void FinishLoading()
        {
            State.FinishLoading();
        }

        public void Pause()
        {
            State.Pause();
        }

        public void Resume()
        {
            State.Resume();
        }

        /// <returns>False unless the game was being played.</returns>
        public bool OpenMenu()
        {
            return State.OpenMenu();
        }

        public void CloseMenu()
        {
            State.CloseMenu();
        }

        private void Awake()
        {
            if (_pauseAction == null)
            {
                Debug.LogError($"{nameof(PauseController)} on '{name}' has no pause action.", this);
            }

            State.Changed += Apply;
            Apply(State.Phase);
        }

        private void OnDestroy()
        {
            State.Changed -= Apply;
            // Time scale is global; a scene reload must not inherit a frozen game.
            Time.timeScale = 1f;
        }

        private void OnEnable()
        {
            if (_pauseAction != null)
            {
                _pauseAction.action.Enable();
            }
        }

        private void Update()
        {
            if (_pauseAction != null && _pauseAction.action.WasPressedThisFrame())
            {
                if (State.Phase == GamePhase.Playing)
                {
                    State.Pause();
                }
                else if (State.Phase == GamePhase.Paused || State.Phase == GamePhase.InMenu)
                {
                    BackPressed?.Invoke();
                }

                return;
            }

            // The Editor frees the cursor when focus moves to another Editor window; a click on the Game view takes it back.
            Mouse mouse = Mouse.current;
            if (State.Phase == GamePhase.Playing && Cursor.lockState != CursorLockMode.Locked &&
                mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                LockCursor(true);
            }
        }

        // Alt-Tab or clicking another window: stop the world rather than let it run unattended.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                State.Pause();
            }
        }

        private void Apply(GamePhase phase)
        {
            bool playing = phase == GamePhase.Playing;
            Time.timeScale = State.IsFrozen ? 0f : 1f;
            LockCursor(playing);
            PhaseChanged?.Invoke(phase);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
