using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Player
{
    /// <summary>
    /// Digs into the voxel pile the camera is looking at. For now it just logs the removed volume;
    /// in step 3 the scooped ash and embers go into the sieve.
    /// </summary>
    public sealed class ScoopInteractor : MonoBehaviour
    {
        [SerializeField] private ScoopSettings _settings;
        [SerializeField] private Transform _camera;
        [SerializeField] private InputActionReference _scoopAction;
        [Tooltip("Layers the scoop ray can hit.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private void Awake()
        {
            if (_settings == null || _camera == null || _scoopAction == null)
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
            // While the cursor is free, the click is for re-locking it, not for digging.
            if (Cursor.lockState != CursorLockMode.Locked || !_scoopAction.action.WasPressedThisFrame())
            {
                return;
            }

            TryScoop();
        }

        private void TryScoop()
        {
            Ray ray = new Ray(_camera.position, _camera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _settings.Reach, _hitMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (!hit.collider.TryGetComponent(out VoxelChunk chunk))
            {
                return;
            }

            // Pushing the sphere into the surface along the view ray takes a real bite instead of a shallow dent.
            Vector3 center = hit.point + ray.direction * (_settings.Radius * _settings.Penetration);
            DigResult result = chunk.Dig(center, _settings.Radius);

            Debug.Log($"Scooped {result.RemovedVolume * 1000f:F1} L of ash. Pile now {chunk.TotalAshVolume:F2} m³.", this);
        }
    }
}
