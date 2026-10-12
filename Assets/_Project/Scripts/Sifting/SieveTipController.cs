using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Thin adapter: on the Tip action, applies <see cref="EmberTransfer"/> and reports what happened.
    /// </summary>
    public sealed class SieveTipController : MonoBehaviour
    {
        [SerializeField] private Sieve _sieve;
        [SerializeField] private Basket _basket;
        [SerializeField] private InputActionReference _tipAction;

        private readonly List<Ember> _transferBuffer = new List<Ember>();

        /// <summary>Fired with the number of embers moved into the basket.</summary>
        public event Action<int> Tipped;

        /// <summary>Fired with the reason when a tip is refused.</summary>
        public event Action<TipOutcome> TipRefused;

        private void Awake()
        {
            if (_sieve == null || _basket == null || _tipAction == null)
            {
                Debug.LogError($"{nameof(SieveTipController)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_tipAction != null)
            {
                _tipAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_tipAction != null)
            {
                _tipAction.action.Disable();
            }
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked || !_tipAction.action.WasPressedThisFrame())
            {
                return;
            }

            TipOutcome outcome = EmberTransfer.Tip(_sieve.Contents, _basket.Contents, _transferBuffer, out int moved);
            if (outcome == TipOutcome.Tipped)
            {
                Tipped?.Invoke(moved);
            }
            else
            {
                TipRefused?.Invoke(outcome);
            }
        }
    }
}
