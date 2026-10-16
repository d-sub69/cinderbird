using System;
using Cinderbird.Core;
using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// Choosing where to send a helper: a circle follows the crosshair, green where she may go (open ash, or the ground
    /// beside the pile, and a way for her to walk there), red elsewhere. Left-click sends her, right-click (or pausing)
    /// cancels. Once she is sent, the circle stays on her destination until she gets there. While choosing, the player's
    /// tools leave the buttons alone (<see cref="InputFocus"/>). The rules are <see cref="AssignRules"/>.
    /// </summary>
    public sealed class HelperAssignController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float EvaluateInterval = 0.1f;
        // The way there is searched again only when the aim moves this far, or this often.
        private const float ReachCheckDistance = 0.3f;
        private const float ReachCheckInterval = 1f;

        [SerializeField] private Transform _camera;
        [SerializeField] private InputFocus _focus;
        [SerializeField] private PauseController _pause;
        [Tooltip("Sends her (the Scoop action, left-click).")]
        [SerializeField] private InputActionReference _confirmAction;
        [Tooltip("Cancels (the Shake action, right-click).")]
        [SerializeField] private InputActionReference _cancelAction;
        [Tooltip("The circle: shown where the player points, and on her destination until she gets there.")]
        [SerializeField] private Renderer _marker;
        [Tooltip("Optional: shows her destination after a save is loaded too.")]
        [SerializeField] private Sally _sally;

        private readonly ComponentLookup<VoxelTerrain> _terrains = new ComponentLookup<VoxelTerrain>();
        private MaterialPropertyBlock _propertyBlock;
        private Sally _choosingFor;
        private Vector3 _point;
        private int _layer;
        private float _nextEvaluate;
        private int _beganFrame = -1;
        private bool _hasReachCheck;
        private Vector3 _reachCheckedAt;
        private float _reachCheckedTime;
        private bool _reachable;

        /// <summary>The circle's state changed (shown, valid or not, why), for the prompt.</summary>
        public event Action StateChanged;

        public bool IsActive => _choosingFor != null;

        public AssignCheck Check { get; private set; } = AssignCheck.NoSurface;

        /// <summary>The layer she would work from the circle; <see cref="AssignRules.OutermostLayer"/> on the ground.</summary>
        public int TargetLayer => _layer;

        private void Awake()
        {
            if (_camera == null || _focus == null || _pause == null || _confirmAction == null || _cancelAction == null || _marker == null)
            {
                Debug.LogError($"{nameof(HelperAssignController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            ShowMarker(false);
        }

        private void OnEnable()
        {
            if (_pause != null)
            {
                _pause.PhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (_pause != null)
            {
                _pause.PhaseChanged -= OnPhaseChanged;
            }

            End();
        }

        /// <summary>Starts choosing where to send her; she waits, facing the player, until it ends.</summary>
        public void Begin(Sally sally)
        {
            if (sally == null || sally.Pile.Terrain == null || IsActive)
            {
                sally?.EndTalk();
                return;
            }

            _choosingFor = sally;
            _focus.Take(this);
            _beganFrame = Time.frameCount;
            _confirmAction.action.Enable();
            _cancelAction.action.Enable();
            _nextEvaluate = 0f;
            _hasReachCheck = false;
            Check = AssignCheck.NoSurface;
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (!IsActive)
            {
                ShowDestination();
                return;
            }

            if (Time.unscaledTime >= _nextEvaluate)
            {
                _nextEvaluate = Time.unscaledTime + EvaluateInterval;
                AssignCheck check = Evaluate();
                if (check != Check)
                {
                    Check = check;
                    StateChanged?.Invoke();
                }
            }

            PlaceMarker(_point, Check == AssignCheck.Valid, Check != AssignCheck.NoSurface);

            // The click on the menu that started this must not also send her.
            if (Cursor.lockState != CursorLockMode.Locked || Time.frameCount == _beganFrame)
            {
                return;
            }

            if (_cancelAction.action.WasPressedThisFrame())
            {
                End();
            }
            else if (_confirmAction.action.WasPressedThisFrame() && Check == AssignCheck.Valid)
            {
                Sally sally = _choosingFor;
                End();
                sally.Assign(_point, _layer);
            }
        }

        private AssignCheck Evaluate()
        {
            Ray ray = new Ray(_camera.position, _camera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _choosingFor.Settings.AssignReach, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return AssignCheck.NoSurface;
            }

            _point = hit.point;
            VoxelTerrain pile = _choosingFor.Pile;
            AshTerrain terrain = pile.Terrain;
            HelperSettings settings = _choosingFor.Settings;
            bool onAsh = _terrains.Find(hit.collider) != null;
            Vector3 local = pile.transform.InverseTransformPoint(hit.point);
            AssignCheck check = AssignRules.Check(terrain.World, terrain.Locks, local, onAsh, settings.MaxDistanceFromAsh, settings.SurveyStride,
                out _layer);
            if (check != AssignCheck.Valid)
            {
                return check;
            }

            return IsReachable(hit.point) ? AssignCheck.Valid : AssignCheck.Unreachable;
        }

        // A path search: done again only when the aim has moved, or after a while (she moves, the ash slides).
        private bool IsReachable(Vector3 point)
        {
            if (!_hasReachCheck || (point - _reachCheckedAt).sqrMagnitude > ReachCheckDistance * ReachCheckDistance ||
                Time.unscaledTime - _reachCheckedTime > ReachCheckInterval)
            {
                _hasReachCheck = true;
                _reachCheckedAt = point;
                _reachCheckedTime = Time.unscaledTime;
                _reachable = _choosingFor.CanReach(point);
            }

            return _reachable;
        }

        // After she is sent: the circle marks where she is going until she gets there.
        private void ShowDestination()
        {
            Sally sally = _sally;
            bool visible = sally != null && sally.IsHired && sally.SpotPending;
            if (visible)
            {
                PlaceMarker(sally.Spot, true, true);
            }
            else
            {
                ShowMarker(false);
            }
        }

        private void PlaceMarker(Vector3 point, bool valid, bool visible)
        {
            ShowMarker(visible);
            if (!visible)
            {
                return;
            }

            HelperSettings settings = (_choosingFor != null ? _choosingFor : _sally).Settings;
            _marker.transform.position = point + Vector3.up * 0.02f;
            _propertyBlock.SetColor(BaseColorId, valid ? settings.ValidColor : settings.InvalidColor);
            _marker.SetPropertyBlock(_propertyBlock);
        }

        private void End()
        {
            if (!IsActive)
            {
                return;
            }

            Sally sally = _choosingFor;
            _choosingFor = null;
            _focus.Release(this);
            ShowMarker(false);
            Check = AssignCheck.NoSurface;
            sally.EndTalk();
            StateChanged?.Invoke();
        }

        private void ShowMarker(bool visible)
        {
            if (_marker.gameObject.activeSelf != visible)
            {
                _marker.gameObject.SetActive(visible);
            }
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Playing)
            {
                End();
            }
        }
    }
}
