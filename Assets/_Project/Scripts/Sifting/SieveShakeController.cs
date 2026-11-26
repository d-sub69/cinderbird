using System;
using Cinderbird.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Thin adapter: feeds input into <see cref="SieveShaker"/> and removes the resulting ash from the sieve.
    /// Views read <see cref="Energy"/> and listen to <see cref="AshShakenOut"/>. The player's sieve reads the Shake and
    /// Look actions; a helper's sieve has no actions and is driven from code (<see cref="Drive"/>), through the same
    /// shake simulation.
    /// </summary>
    public sealed class SieveShakeController : MonoBehaviour
    {
        [SerializeField] private Sieve _sieve;
        [SerializeField] private ShakeSettings _settings;
        [Tooltip("Leave both actions empty for a sieve driven from code (a helper's).")]
        [SerializeField] private InputActionReference _shakeAction;
        [Tooltip("Mouse delta; its speed while shaking drives the shake energy.")]
        [SerializeField] private InputActionReference _lookAction;
        [Tooltip("Optional: while something else owns the buttons (sending a helper), right-click doesn't shake.")]
        [SerializeField] private InputFocus _focus;

        private SieveShaker _shaker;
        private bool _drivenHeld;
        private float _drivenWiggleSpeed;
        private float _timeScale = 1f;
        private float _rateScale = 1f;

        /// <summary>Fired with the cubic meters of ash shaken out this frame.</summary>
        public event Action<float> AshShakenOut;

        public float Energy => _shaker?.Energy ?? 0f;

        /// <summary>Whether input comes from <see cref="Drive"/> rather than the Shake and Look actions.</summary>
        public bool IsDriven => _shakeAction == null || _lookAction == null;

        /// <summary>A mouse speed (pixels per second) that gives full shake energy.</summary>
        public float FullWiggleSpeed => _settings != null ? _settings.ToParameters().FullEnergyWiggleSpeed : 0f;

        /// <summary>The shake's parameters (removal rate, rise time), e.g. to time a driven shake.</summary>
        public ShakeParameters Parameters => _settings != null ? _settings.ToParameters() : default;

        private void Awake()
        {
            if (_sieve == null || _settings == null)
            {
                Debug.LogError($"{nameof(SieveShakeController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _shaker = new SieveShaker(_settings.ToParameters());
        }

        private void OnEnable()
        {
            if (_shaker == null || IsDriven)
            {
                return;
            }

            // Not disabled in OnDisable: Look and Shake are shared with FirstPersonController,
            // and disabling them here would cut its input too.
            _shakeAction.action.Enable();
            _lookAction.action.Enable();
        }

        /// <summary>For a driven sieve: hold (or let go of) the shake at a mouse speed, until called again.</summary>
        public void Drive(bool held, float wiggleSpeed)
        {
            _drivenHeld = held;
            _drivenWiggleSpeed = held ? Mathf.Max(0f, wiggleSpeed) : 0f;
        }

        /// <summary>Shakes ash out this many times faster: a bigger sieve, so a full one takes as long as the first sieve's.</summary>
        public void SetRateScale(float scale)
        {
            _rateScale = Mathf.Max(0.01f, scale);
        }

        /// <summary>Runs the shake faster (above 1) or slower: a helper's pace upgrade shortens her shaking.</summary>
        public void SetTimeScale(float scale)
        {
            _timeScale = Mathf.Max(0.01f, scale);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime * _timeScale;
            if (deltaTime <= 0f)
            {
                return;
            }

            bool held;
            float wiggleSpeed;
            if (IsDriven)
            {
                held = _drivenHeld;
                wiggleSpeed = _drivenWiggleSpeed;
            }
            else
            {
                // While the cursor is free, right-click belongs to the Editor, not the sieve.
                held = Cursor.lockState == CursorLockMode.Locked && _shakeAction.action.IsPressed() && (_focus == null || !_focus.IsTaken);
                // Mouse delta is per real frame: divided by the real frame time, not the scaled one.
                wiggleSpeed = held ? _lookAction.action.ReadValue<Vector2>().magnitude / Time.deltaTime : 0f;
            }

            _shaker.Tick(held, wiggleSpeed, deltaTime);
            ShakeOutAsh(deltaTime);
        }

        private void ShakeOutAsh(float deltaTime)
        {
            float toRemove = _shaker.AshToRemove(deltaTime) * _rateScale;
            if (toRemove <= 0f)
            {
                return;
            }

            float removed = _sieve.Contents.RemoveAsh(toRemove);
            if (removed > 0f)
            {
                AshShakenOut?.Invoke(removed);
            }
        }
    }
}
