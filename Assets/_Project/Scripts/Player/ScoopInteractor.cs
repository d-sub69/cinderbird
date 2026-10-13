using System.Collections.Generic;
using Cinderbird.Core;
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
        [Tooltip("Optional: pours the scooped ash into the sieve over a moment instead of instantly.")]
        [SerializeField] private SieveFiller _filler;
        [SerializeField] private InputActionReference _scoopAction;
        [Tooltip("Layers the scoop ray can hit.")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [Tooltip("Optional: test mode can make the scoop bigger and drop its ash on the way.")]
        [SerializeField] private TestModeSettings _testMode;

        private readonly List<Ember> _extractedEmbers = new List<Ember>();
        private float[] _removedByLayer = System.Array.Empty<float>();
        private VoxelTerrain _targetTerrain;
        private Vector3 _targetCenter;
        private Vector3 _targetSurfacePoint;

        /// <summary>Fired when a click on the pile is refused, with the reason (for player feedback).</summary>
        public event System.Action<ScoopRefusal> ScoopRefused;

        /// <summary>True when the view ray hits diggable ash within reach this frame.</summary>
        public bool HasTarget => _targetTerrain != null;

        private float ScoopRadius => _settings.Radius * (_testMode != null ? _testMode.ScoopRadiusMultiplier : 1f);

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
            _targetTerrain = null;
            // Scooping needs the sieve in hand; a stowed sieve is inactive.
            if (!_sieve.isActiveAndEnabled)
            {
                return;
            }

            Ray ray = new Ray(_camera.position, _camera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _settings.Reach, _hitMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            VoxelTerrain terrain = hit.collider.GetComponentInParent<VoxelTerrain>();
            if (terrain != null)
            {
                _targetTerrain = terrain;
                _targetSurfacePoint = hit.point;
                // Pushing the sphere into the surface along the view ray takes a real bite instead of a shallow dent.
                _targetCenter = hit.point + ray.direction * (ScoopRadius * _settings.Penetration);
            }
        }

        private void TryScoop()
        {
            bool pouring = _filler != null && _filler.IsFilling;
            if (_targetTerrain == null || pouring)
            {
                return;
            }

            // Checked before digging so a refused scoop doesn't destroy ash or embers.
            SieveContents contents = _sieve.Contents;
            if (contents.HasAsh)
            {
                ScoopRefused?.Invoke(ScoopRefusal.AshInSieve);
                return;
            }

            if (contents.IsFullOfEmbers)
            {
                ScoopRefused?.Invoke(ScoopRefusal.SieveFullOfEmbers);
                return;
            }

            _extractedEmbers.Clear();
            float[] removedByLayer = LayerBuffer(_targetTerrain.LayerCount);
            // The hole opens while the ash pours into the sieve, so the pile empties as the sieve fills.
            bool pours = _filler != null && _filler.isActiveAndEnabled;
            float carveSeconds = pours ? _filler.PourDuration : 0f;
            DigResult result = _targetTerrain.Dig(_targetCenter, ScoopRadius, _extractedEmbers, removedByLayer,
                _settings.EmberCatchMargin, carveSeconds);
            if (!result.RemovedAnyAsh && result.EmberCount == 0)
            {
                return;
            }

            _targetTerrain.MixLayerColors(removedByLayer, out Color ashColor, out Color dustColor);

            // Test mode can drop the ash on the way: the pile still loses it, but the sieve gets only the embers.
            float ashIntoSieve = _testMode != null && _testMode.SkipShaking ? 0f : result.RemovedVolume;

            // The filler copies the embers, so the list can be reused for the next dig.
            if (pours)
            {
                _filler.TryBegin(ashIntoSieve, _extractedEmbers, _targetSurfacePoint, ashColor, dustColor);
                return;
            }

            contents.TryAdd(ashIntoSieve, _extractedEmbers, ashColor, dustColor);
        }

        // Reused between digs; only reallocated if the terrain's layer count changes.
        private float[] LayerBuffer(int layerCount)
        {
            if (_removedByLayer.Length != layerCount)
            {
                _removedByLayer = new float[layerCount];
            }

            System.Array.Clear(_removedByLayer, 0, _removedByLayer.Length);
            return _removedByLayer;
        }
    }
}
