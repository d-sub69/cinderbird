using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Thin adapter: feeds input into <see cref="SieveShaker"/> and removes the resulting ash from the sieve.
    /// Views read <see cref="Energy"/> and listen to <see cref="AshShakenOut"/>.
    /// </summary>
    public sealed class SieveShakeController : MonoBehaviour
    {
        [SerializeField] private Sieve _sieve;
        [SerializeField] private ShakeSettings _settings;
        [SerializeField] private InputActionReference _shakeAction;
        [Tooltip("Mouse delta; its speed while shaking drives the shake energy.")]
        [SerializeField] private InputActionReference _lookAction;

        private SieveShaker _shaker;

        /// <summary>Fired with the cubic meters of ash shaken out this frame.</summary>
        public event Action<float> AshShakenOut;

        public float Energy => _shaker?.Energy ?? 0f;

        private void Awake()
        {
            if (_sieve == null || _settings == null || _shakeAction == null || _lookAction == null)
            {
                Debug.LogError($"{nameof(SieveShakeController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _shaker = new SieveShaker(_settings.ToParameters());
        }

        private void OnEnable()
        {
            if (_shaker == null)
            {
                return;
            }

            // Not disabled in OnDisable: Look and Shake are shared with FirstPersonController,
            // and disabling them here would cut its input too.
            _shakeAction.action.Enable();
            _lookAction.action.Enable();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            // While the cursor is free, right-click belongs to the Editor, not the sieve.
            bool held = Cursor.lockState == CursorLockMode.Locked && _shakeAction.action.IsPressed();
            float wiggleSpeed = held ? _lookAction.action.ReadValue<Vector2>().magnitude / deltaTime : 0f;

            _shaker.Tick(held, wiggleSpeed, deltaTime);
            ShakeOutAsh(deltaTime);
        }

        private void ShakeOutAsh(float deltaTime)
        {
            float toRemove = _shaker.AshToRemove(deltaTime);
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
