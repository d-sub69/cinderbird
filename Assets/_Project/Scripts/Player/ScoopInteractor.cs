using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Player
{
    /// <summary>
    /// Digs into the voxel pile the camera is looking at and puts the ash and embers into the sieve.
    /// </summary>
    public sealed class ScoopInteractor : MonoBehaviour
    {
        [SerializeField] private ScoopSettings _settings;
        [SerializeField] private Transform _camera;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private InputActionReference _scoopAction;
        [Tooltip("Layers the scoop ray can hit.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private readonly List<Ember> _extractedEmbers = new List<Ember>();
        private VoxelChunk _targetChunk;
        private Vector3 _targetCenter;

        /// <summary>True when the view ray hits diggable ash within reach this frame.</summary>
        public bool HasTarget => _targetChunk != null;

        private void Awake()
        {
            if (_settings == null || _camera == null || _sieve == null || _scoopAction == null)
            {
                Debug.LogError($"{nameof(ScoopInteractor)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_scoopAction != null)
            {
                _scoopAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_scoopAction != null)
            {
                _scoopAction.action.Disable();
            }
        }

        private void Update()
        {
            // Every frame, not just on click, so the center dot can show whether a scoop would land.
            UpdateTarget();

            // While the cursor is free, the click is for re-locking it, not for digging.
            if (Cursor.lockState != CursorLockMode.Locked || !_scoopAction.action.WasPressedThisFrame())
            {
                return;
            }

            TryScoop();
        }

        private void UpdateTarget()
        {
            _targetChunk = null;
            Ray ray = new Ray(_camera.position, _camera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _settings.Reach, _hitMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider.TryGetComponent(out VoxelChunk chunk))
            {
                _targetChunk = chunk;
                // Pushing the sphere into the surface along the view ray takes a real bite instead of a shallow dent.
                _targetCenter = hit.point + ray.direction * (_settings.Radius * _settings.Penetration);
            }
        }

        private void TryScoop()
        {
            // Checked before digging so a refused scoop doesn't destroy ash or embers.
            if (_targetChunk == null || !_sieve.Contents.CanScoop)
            {
                return;
            }

            _extractedEmbers.Clear();
            DigResult result = _targetChunk.Dig(_targetCenter, _settings.Radius, _extractedEmbers);
            if (!result.RemovedAnyAsh && result.EmberCount == 0)
            {
                return;
            }

            _sieve.Contents.TryAdd(result.RemovedVolume, _extractedEmbers);
        }
    }
}
