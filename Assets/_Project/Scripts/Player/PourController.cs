using System;
using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Embers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Player
{
    /// <summary>
    /// Thin adapter: on Pour, empties the tool in hand into whatever the player is looking at, using the shared
    /// <see cref="EmberTransfer"/> rules, and reports what happened.
    /// </summary>
    public sealed class PourController : MonoBehaviour
    {
        [SerializeField] private ToolBelt _toolBelt;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private InputActionReference _pourAction;
        [Tooltip("Optional: while something else owns the buttons (sending a helper), this leaves them alone.")]
        [SerializeField] private InputFocus _focus;

        private readonly List<Ember> _moved = new List<Ember>();

        /// <summary>Fired with the tool, the target and how many embers went in.</summary>
        public event Action<ToolDefinition, IPourTarget, int> Poured;

        /// <summary>Fired with the tool and the reason when a pour is refused.</summary>
        public event Action<ToolDefinition, PourOutcome> PourRefused;

        /// <summary>True when pressing Pour would try to pour the tool in hand into what the player is looking at.</summary>
        public bool HasTarget => enabled && _interactor.PourTarget != null && _toolBelt.ActiveSource != null;

        private void Awake()
        {
            if (_toolBelt == null || _interactor == null || _pourAction == null)
            {
                Debug.LogError($"{nameof(PourController)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_pourAction != null)
            {
                _pourAction.action.Enable();
            }
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked || !_pourAction.action.WasPressedThisFrame() || !HasTarget || (_focus != null && _focus.IsTaken))
            {
                return;
            }

            ToolDefinition tool = _toolBelt.ActiveTool;
            IPourTarget target = _interactor.PourTarget;
            PourOutcome outcome = EmberTransfer.Pour(_toolBelt.ActiveSource, target.Sink, _moved);
            if (outcome != PourOutcome.Poured)
            {
                PourRefused?.Invoke(tool, outcome);
                return;
            }

            target.OnPoured(_moved, _toolBelt.ActiveHeldObject.transform.position);
            Poured?.Invoke(tool, target, _moved.Count);
        }
    }
}
