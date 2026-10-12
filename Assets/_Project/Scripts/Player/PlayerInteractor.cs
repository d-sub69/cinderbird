using System;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// The look-to-interact raycast: finds what the player is looking at within reach (the nest now, a placed
    /// basket in step C2) so actions and prompts can use it.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform _camera;
        [SerializeField] private ToolBeltSettings _settings;
        [Tooltip("Layers the interaction ray can hit.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        /// <summary>What the player could pour into right now, or null.</summary>
        public IPourTarget PourTarget { get; private set; }

        public event Action TargetChanged;

        private void Awake()
        {
            if (_camera == null || _settings == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            IPourTarget found = null;
            Ray ray = new Ray(_camera.position, _camera.forward);
            // Triggers count: interactables use trigger volumes so they can be bigger than their solid parts.
            if (Physics.Raycast(ray, out RaycastHit hit, _settings.InteractReach, _hitMask, QueryTriggerInteraction.Collide))
            {
                found = hit.collider.GetComponentInParent<IPourTarget>();
            }

            if (ReferenceEquals(found, PourTarget))
            {
                return;
            }

            PourTarget = found;
            TargetChanged?.Invoke();
        }
    }
}
