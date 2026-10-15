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
        [Tooltip("Optional: embers lying loose can be picked up with a click.")]
        [SerializeField] private LooseEmberView _looseEmbers;

        private readonly List<Ember> _extractedEmbers = new List<Ember>();
        private readonly ComponentLookup<VoxelTerrain> _terrains = new ComponentLookup<VoxelTerrain>();
        private float[] _removedByLayer = System.Array.Empty<float>();
        private VoxelTerrain _targetTerrain;
        private Vector3 _targetCenter;
        private Vector3 _targetSurfacePoint;
        private int _aimedLooseId = -1;

        /// <summary>Fired when a click on the pile is refused, with the reason (for player feedback).</summary>
        public event System.Action<ScoopRefusal> ScoopRefused;

        /// <summary>True when the view ray hits diggable ash or a loose ember within reach this frame.</summary>
        public bool HasTarget => _targetTerrain != null || _aimedLooseId >= 0;

        // Set by the sieve upgrade; until then the settings give the radius.
        private float _radiusOverride;

        private float ScoopRadius => (_radiusOverride > 0f ? _radiusOverride : _settings.Radius) *
                                     (_testMode != null ? _testMode.ScoopRadiusMultiplier : 1f);

        /// <summary>The scoop radius of the sieve in hand, in meters (an upgrade sets it).</summary>
        public void SetScoopRadius(float radius)
        {
            _radiusOverride = Mathf.Max(0.05f, radius);
        }

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
            _aimedLooseId = -1;
            // Scooping needs the sieve in hand; a stowed sieve is inactive.
            if (!_sieve.isActiveAndEnabled)
            {
                return;
            }

            Ray ray = new Ray(_camera.position, _camera.forward);
            float hitDistance = float.MaxValue;
            if (Physics.Raycast(ray, out RaycastHit hit, _settings.Reach, _hitMask, QueryTriggerInteraction.Ignore))
            {
                hitDistance = hit.distance;
                VoxelTerrain terrain = _terrains.Find(hit.collider);
                if (terrain != null)
                {
                    _targetTerrain = terrain;
                    _targetSurfacePoint = hit.point;
                    // Pushing the sphere into the surface along the view ray takes a real bite instead of a shallow dent.
                    _targetCenter = hit.point + ray.direction * (ScoopRadius * _settings.Penetration);
                }
            }

            // An ember lying in sight wins over the ash behind it: clicking picks it up on its own.
            if (_looseEmbers != null && _looseEmbers.TryFindAimed(ray, _settings.Reach, out int id, out float distance)
                && distance <= hitDistance + _settings.LooseEmberAimDepth)
            {
                _aimedLooseId = id;
            }
        }

        private void TryScoop()
        {
            bool pouring = _filler != null && _filler.IsFilling;
            if (!HasTarget || pouring)
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
            bool pours = _filler != null && _filler.isActiveAndEnabled;
            if (_aimedLooseId >= 0)
            {
                PickUpLoose(contents, pours);
                return;
            }

            float[] removedByLayer = LayerBuffer(_targetTerrain.LayerCount);
            // The hole opens while the ash pours into the sieve, so the pile empties as the sieve fills.
            float carveSeconds = pours ? _filler.PourDuration : 0f;
            DigResult result = _targetTerrain.Dig(_targetCenter, ScoopRadius, _extractedEmbers, removedByLayer,
                _settings.EmberExposeMargin, carveSeconds);
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

        // A loose ember hops into the sieve on its own, without ash, from wherever it lies.
        private void PickUpLoose(SieveContents contents, bool pours)
        {
            if (!_looseEmbers.TryTake(_aimedLooseId, out Ember ember, out Vector3 position))
            {
                return;
            }

            _extractedEmbers.Add(ember);
            if (pours)
            {
                _filler.TryBegin(0f, _extractedEmbers, position);
                return;
            }

            contents.TryAdd(0f, _extractedEmbers);
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
