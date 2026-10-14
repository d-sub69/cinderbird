using System;
using Cinderbird.Core;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// The look-to-interact raycast: finds what the player is looking at within reach (the nest, a placed
    /// basket) so actions, prompts and the center dot can use it.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform _camera;
        [SerializeField] private ToolBeltSettings _settings;
        [Tooltip("Layers the interaction ray can hit.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private readonly ComponentLookup<IPourTarget> _pourTargets = new ComponentLookup<IPourTarget>();
        private readonly ComponentLookup<PlaceableTool> _placeables = new ComponentLookup<PlaceableTool>();

        /// <summary>What the player could pour into right now, or null.</summary>
        public IPourTarget PourTarget { get; private set; }

        /// <summary>A placed tool the player could pick up right now, or null.</summary>
        public PlaceableTool PickUpTarget { get; private set; }

        /// <summary>The object behind the current target (for looks such as the center dot's accent), or null.</summary>
        public GameObject TargetObject { get; private set; }

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
            IPourTarget pourTarget = null;
            PlaceableTool pickUpTarget = null;
            GameObject targetObject = null;

            Ray ray = new Ray(_camera.position, _camera.forward);
            // Triggers count: interactables use trigger volumes so they can be bigger than their solid parts.
            if (Physics.Raycast(ray, out RaycastHit hit, _settings.InteractReach, _hitMask, QueryTriggerInteraction.Collide))
            {
                pourTarget = _pourTargets.Find(hit.collider);
                pickUpTarget = _placeables.Find(hit.collider);
                if (pickUpTarget != null && !pickUpTarget.IsPlaced)
                {
                    pickUpTarget = null;
                }

                if (pourTarget != null || pickUpTarget != null)
                {
                    targetObject = hit.collider.gameObject;
                }
            }

            if (ReferenceEquals(pourTarget, PourTarget) && pickUpTarget == PickUpTarget && targetObject == TargetObject)
            {
                return;
            }

            PourTarget = pourTarget;
            PickUpTarget = pickUpTarget;
            TargetObject = targetObject;
            TargetChanged?.Invoke();
        }
    }
}
