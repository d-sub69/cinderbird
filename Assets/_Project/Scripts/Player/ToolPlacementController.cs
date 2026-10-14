using System;
using Cinderbird.Core;
using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Player
{
    /// <summary>
    /// Thin adapter for setting tools down and picking them up: with a placeable tool in hand, shows a ghost
    /// where it would land and places it on Place (G); looking at a placed tool, Pick Up (E) takes it back.
    /// </summary>
    public sealed class ToolPlacementController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private ToolBelt _toolBelt;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private Transform _camera;
        [SerializeField] private ToolBeltSettings _settings;
        [SerializeField] private InputActionReference _placeAction;
        [SerializeField] private InputActionReference _pickUpAction;
        [Tooltip("Layers the placement ray can land on.")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private Color _validGhostColor = new Color(0.3f, 1f, 0.4f, 0.35f);
        [SerializeField] private Color _invalidGhostColor = new Color(1f, 0.3f, 0.3f, 0.35f);

        // Reused buffer for the no-placement-zone overlap query; avoids allocating every frame.
        private readonly Collider[] _zoneHits = new Collider[8];
        private readonly ComponentLookup<VoxelTerrain> _terrains = new ComponentLookup<VoxelTerrain>();
        private readonly ComponentLookup<NoPlacementZone> _zones = new ComponentLookup<NoPlacementZone>();
        private MaterialPropertyBlock _propertyBlock;
        private PlaceableTool _shownGhostOwner;
        private Renderer[] _ghostRenderers = Array.Empty<Renderer>();
        private GameObject _lookedUpHeld;
        private PlaceableTool _lookedUpPlaceable;
        private Vector3 _groundPoint;
        private Quaternion _placeRotation;

        /// <summary>The placeable tool in hand, or null when holding something else.</summary>
        public PlaceableTool Placing { get; private set; }

        /// <summary>Whether the tool in hand could be set down where the player is looking.</summary>
        public PlacementResult Placement { get; private set; } = PlacementResult.NoSurface;

        /// <summary>The off-limits area the look point is in, when <see cref="Placement"/> says so.</summary>
        public NoPlacementZone BlockingZone { get; private set; }

        /// <summary>Fires when <see cref="Placing"/> or <see cref="Placement"/> changes, for prompts.</summary>
        public event Action StateChanged;

        public event Action<PlaceableTool> Placed;
        public event Action<PlaceableTool> PickedUp;

        /// <summary>Fired when a pick-up fails because every inventory slot is taken.</summary>
        public event Action<PlaceableTool> PickUpRefused;

        private void Awake()
        {
            if (_toolBelt == null || _interactor == null || _camera == null || _settings == null ||
                _placeAction == null || _pickUpAction == null)
            {
                Debug.LogError($"{nameof(ToolPlacementController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_placeAction != null)
            {
                _placeAction.action.Enable();
            }

            if (_pickUpAction != null)
            {
                _pickUpAction.action.Enable();
            }
        }

        private void Update()
        {
            UpdatePlacementPreview();

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            if (_placeAction.action.WasPressedThisFrame())
            {
                TryPlace();
            }
            else if (_pickUpAction.action.WasPressedThisFrame())
            {
                TryPickUp();
            }
        }

        private void UpdatePlacementPreview()
        {
            GameObject held = _toolBelt.ActiveHeldObject;
            // Looked up only when the tool in hand changes: in the Editor, a GetComponent that finds nothing
            // allocates, which would show up as per-frame garbage while holding the sieve.
            if (held != _lookedUpHeld)
            {
                _lookedUpHeld = held;
                _lookedUpPlaceable = held != null ? held.GetComponent<PlaceableTool>() : null;
            }

            PlaceableTool placing = _lookedUpPlaceable;
            PlacementResult placement = placing != null ? EvaluateLookPoint() : PlacementResult.NoSurface;

            ShowGhost(placing, placement);
            if (placing == Placing && placement == Placement)
            {
                return;
            }

            Placing = placing;
            Placement = placement;
            StateChanged?.Invoke();
        }

        private PlacementResult EvaluateLookPoint()
        {
            Ray ray = new Ray(_camera.position, _camera.forward);
            bool hasSurface = Physics.Raycast(ray, out RaycastHit hit, _settings.InteractReach, _groundMask, QueryTriggerInteraction.Ignore);
            BlockingZone = null;
            if (!hasSurface)
            {
                return PlacementRules.Evaluate(false, Vector3.up, _settings.MaxPlaceSlope, false, false, false);
            }

            _groundPoint = hit.point;
            // Upright, turned to face the same way as the player.
            _placeRotation = Quaternion.Euler(0f, _camera.eulerAngles.y, 0f);

            bool onAsh = _terrains.Find(hit.collider) != null;
            float clearance = _settings.PlaceClearance;
            // Lifted just off the ground so the ground itself doesn't count as an obstacle.
            Vector3 probe = hit.point + Vector3.up * (clearance + 0.02f);
            bool blocked = Physics.CheckSphere(probe, clearance, _groundMask, QueryTriggerInteraction.Ignore);
            BlockingZone = FindNoPlacementZone(hit.point, clearance);

            return PlacementRules.Evaluate(true, hit.normal, _settings.MaxPlaceSlope, onAsh, BlockingZone != null, blocked);
        }

        // Zones are usually triggers (the nest's look volume), so this query includes them.
        private NoPlacementZone FindNoPlacementZone(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, _zoneHits, Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                NoPlacementZone zone = _zones.Find(_zoneHits[i]);
                if (zone != null)
                {
                    return zone;
                }
            }

            return null;
        }

        private void ShowGhost(PlaceableTool placing, PlacementResult placement)
        {
            // Hide the previous tool's ghost when switching away from it.
            if (_shownGhostOwner != null && _shownGhostOwner != placing && _shownGhostOwner.Ghost != null)
            {
                _shownGhostOwner.Ghost.SetActive(false);
            }

            if (placing != _shownGhostOwner)
            {
                // Looked up once per tool switch, not every frame.
                _ghostRenderers = placing != null && placing.Ghost != null
                    ? placing.Ghost.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
            }

            _shownGhostOwner = placing;
            if (placing == null || placing.Ghost == null)
            {
                return;
            }

            GameObject ghost = placing.Ghost;
            bool visible = placement != PlacementResult.NoSurface;
            ghost.SetActive(visible);
            if (!visible)
            {
                return;
            }

            ghost.transform.SetPositionAndRotation(_groundPoint + Vector3.up * placing.PlacedHeightOffset, _placeRotation);
            _propertyBlock.SetColor(BaseColorId, placement == PlacementResult.Valid ? _validGhostColor : _invalidGhostColor);
            foreach (Renderer ghostRenderer in _ghostRenderers)
            {
                ghostRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void TryPlace()
        {
            PlaceableTool tool = Placing;
            if (tool == null || Placement != PlacementResult.Valid || !_toolBelt.TryRelease(tool.Tool))
            {
                return;
            }

            if (tool.Ghost != null)
            {
                tool.Ghost.SetActive(false);
            }

            tool.PlaceAt(_groundPoint, _placeRotation);
            Placed?.Invoke(tool);
        }

        private void TryPickUp()
        {
            PlaceableTool tool = _interactor.PickUpTarget;
            if (tool == null)
            {
                return;
            }

            if (!_toolBelt.Inventory.HasFreeSlot)
            {
                PickUpRefused?.Invoke(tool);
                return;
            }

            tool.ReturnToHands();
            _toolBelt.TryTakeBack(tool.Tool);
            PickedUp?.Invoke(tool);
        }
    }
}
