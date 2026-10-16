using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Player;
using Cinderbird.Terrain;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// Walks a helper like the player walks: the same <see cref="FirstPersonMotor"/> and movement settings (speed,
    /// acceleration, gravity) on a <see cref="CharacterController"/>, so she is a solid body the player bumps into.
    /// Destinations are reached by A* paths over a <see cref="NavGrid"/> of the yard; when something the grid doesn't
    /// know blocks her (the player, a set-down basket), she marks it, finds a way around, and gives up only after a few
    /// tries. She turns smoothly to face where she goes, or whatever she is told to face. She never teleports.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class HelperMover : MonoBehaviour
    {
        private static readonly ProfilerMarker SearchMarker = new ProfilerMarker("Cinderbird.Helper");

        [SerializeField] private PlayerMovementSettings _movement;
        [SerializeField] private HelperSettings _settings;
        [SerializeField] private VoxelTerrain _pile;

        private readonly List<Vector2Int> _rawPath = new List<Vector2Int>();
        private readonly List<Vector2Int> _path = new List<Vector2Int>();
        private readonly Collider[] _overlaps = new Collider[16];
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly ReachGoal _goal = new ReachGoal();
        // For checking a spot the player points at, without touching the walk in progress.
        private readonly ReachGoal _probeGoal = new ReachGoal();
        private readonly List<Vector2Int> _probePath = new List<Vector2Int>();
        // Start-up only: whether a collider is the ash or a character, asked once per collider rather than once per cell.
        private readonly Dictionary<Collider, bool> _ignoredColliders = new Dictionary<Collider, bool>();
        private CharacterController _body;
        private FirstPersonMotor _motor;
        private NavGrid _grid;
        private GridPathfinder _finder;
        private int _pathIndex;
        private bool _hasGoal;
        private float _yawVelocity;
        private float _targetYaw;
        private bool _turning;
        // Progress along the path: the waypoint she heads for, the closest she has come to it, and when that last improved.
        private int _progressWaypoint;
        private float _closestToWaypoint;
        private float _lastProgressTime;
        private float _walkDeadline;
        private int _repaths;
        private float _nextJumpTime;
        private bool _triedJumpHere;

        /// <summary>Walking toward a destination right now.</summary>
        public bool IsWalking => _hasGoal && !HasArrived && !HasFailed;

        /// <summary>At the last destination given (stays true until a new one is given).</summary>
        public bool HasArrived { get; private set; }

        /// <summary>The last destination can't be reached (no path, or stuck too often).</summary>
        public bool HasFailed { get; private set; }

        /// <summary>Horizontal speed (m/s), for the walk animation.</summary>
        public float Speed { get; private set; }

        public float Radius => _body != null ? _body.radius : 0.3f;

        public NavGrid Grid => _grid;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            if (_movement == null || _settings == null || _pile == null)
            {
                Debug.LogError($"{nameof(HelperMover)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _motor = new FirstPersonMotor(_movement.ToMotorConfig());
            _finder = new GridPathfinder(_settings.MaxSearchCells, _settings.ClimbCost);
            _targetYaw = transform.eulerAngles.y;
            BuildGrid();
        }

        private void OnEnable()
        {
            if (_grid == null)
            {
                return;
            }

            _pile.Built += AttachTerrain;
            _pile.Restored += OnRestored;
            _pile.Settled += OnSettled;
            if (_pile.Terrain != null)
            {
                AttachTerrain();
            }
        }

        private void OnDisable()
        {
            if (_grid == null)
            {
                return;
            }

            _pile.Built -= AttachTerrain;
            _pile.Restored -= OnRestored;
            _pile.Settled -= OnSettled;
        }

        /// <summary>
        /// Walks to somewhere the target is in reach from: within a ring around it and at a height it can be reached from.
        /// Already there: arrives at once. Check <see cref="HasArrived"/> and <see cref="HasFailed"/>.
        /// </summary>
        public void GoTo(Vector3 target, Vector2 distance, float reachUp, float reachDown)
        {
            _goal.Set(_grid, target, distance.x, distance.y, reachUp, reachDown);
            _hasGoal = true;
            _repaths = 0;
            HasArrived = false;
            HasFailed = false;
            if (_goal.Contains(transform.position))
            {
                Arrive();
                return;
            }

            Plan();
        }

        /// <summary>Whether she could walk to somewhere a target is in reach from (the same rules as <see cref="GoTo"/>), from where she is now.</summary>
        public bool CanReach(Vector3 target, Vector2 distance, float reachUp, float reachDown)
        {
            if (_grid == null)
            {
                return false;
            }

            _probeGoal.Set(_grid, target, distance.x, distance.y, reachUp, reachDown);
            if (_probeGoal.Contains(transform.position))
            {
                return true;
            }

            _grid.Now = Time.time;
            _grid.RefreshTerrain();
            return _grid.TryCellOf(transform.position, out Vector2Int start) && _finder.FindPath(_grid, start, _probeGoal, _probePath);
        }

        /// <summary>Stops walking and forgets the destination.</summary>
        public void Stop()
        {
            _hasGoal = false;
            _path.Clear();
            HasArrived = false;
            HasFailed = false;
        }

        /// <summary>Turns smoothly toward a point (while not walking). True once facing it.</summary>
        public bool Face(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            _targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
            _turning = true;
            return Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, _targetYaw)) <= _settings.FaceTolerance;
        }

        /// <summary>Her walking state in one line, for the log while testing (allocates; not for every frame).</summary>
        public string Describe()
        {
            string state = HasFailed ? "failed" : HasArrived ? "arrived" : IsWalking ? "walking" : _hasGoal ? "goal" : "no goal";
            return $"mover {(isActiveAndEnabled ? "on" : "OFF")}, controller {(_body.enabled ? "on" : "OFF")}, {state}, " +
                   $"waypoint {_pathIndex}/{_path.Count}, at {transform.position}, to {_goal.Target}, grounded {_body.isGrounded}";
        }

        /// <summary>Puts her at a saved pose (a load, not a move: the game is only just starting).</summary>
        public void Restore(Vector3 position, float yaw)
        {
            Stop();
            _body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _body.enabled = true;
            _targetYaw = yaw;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            _grid.Now = Time.time;
            Vector3 desired = Vector3.zero;
            if (IsWalking)
            {
                desired = Steer();
            }

            // The player's motor; a shorter wish walks slower, so she keeps the player's acceleration and gravity.
            Vector3 velocity = _motor.Tick(desired * _settings.WalkSpeedFactor, _body.isGrounded, deltaTime);
            CollisionFlags flags = _body.Move(velocity * deltaTime);
            if ((flags & CollisionFlags.Above) != 0)
            {
                _motor.NotifyHitCeiling();
            }

            Vector3 horizontal = new Vector3(_body.velocity.x, 0f, _body.velocity.z);
            Speed = horizontal.magnitude;
            if (IsWalking)
            {
                CheckStuck();
                if (desired.sqrMagnitude > 0.0001f)
                {
                    _targetYaw = Quaternion.LookRotation(desired).eulerAngles.y;
                    _turning = true;
                }
            }

            Turn(deltaTime);
        }

        private Vector3 Steer()
        {
            if (_goal.Contains(transform.position))
            {
                Arrive();
                return Vector3.zero;
            }

            if (_pathIndex >= _path.Count)
            {
                // The path ends on a cell the planner found in reach: that is arriving. (Checking her exact position again
                // here could disagree with the cell by a few centimeters and plan the same path over and over.)
                Arrive();
                return Vector3.zero;
            }

            Vector3 waypoint = _grid.CenterOf(_path[_pathIndex].x, _path[_pathIndex].y);
            Vector3 toWaypoint = waypoint - transform.position;
            toWaypoint.y = 0f;
            // The last waypoint is walked to closely; the ones before only roughly, so she cuts no corners into obstacles.
            bool last = _pathIndex == _path.Count - 1;
            if (toWaypoint.magnitude <= (last ? _settings.WaypointRadius * 0.4f : _settings.WaypointRadius))
            {
                _pathIndex++;
                return Steer();
            }

            Vector3 direction = toWaypoint.normalized;
            JumpIfLedgeAhead(direction);
            return direction;
        }

        // The ground just ahead rises more than a step: jump up onto it (the path planned it as a jump).
        private void JumpIfLedgeAhead(Vector3 direction)
        {
            if (_grid == null || !_body.isGrounded || Time.time < _nextJumpTime)
            {
                return;
            }

            Vector3 ahead = transform.position + direction * (_body.radius + _grid.CellSize * 0.6f);
            if (!_grid.TryCellOf(transform.position, out Vector2Int here) || !_grid.TryCellOf(ahead, out Vector2Int next) || here == next)
            {
                return;
            }

            // Only where the rise is too high to walk: an ordinary ash slope is walked, not hopped.
            if (_grid.NeedsJump(here.x, here.y, next.x, next.y))
            {
                Jump();
            }
        }

        private void Jump()
        {
            _motor.RequestJump();
            _nextJumpTime = Time.time + 0.6f;
        }

        private void Plan()
        {
            using (SearchMarker.Auto())
            {
                PlanPath();
            }
        }

        private void PlanPath()
        {
            _grid.RefreshTerrain();
            _path.Clear();
            _pathIndex = 0;
            if (!_grid.TryCellOf(transform.position, out Vector2Int start) || !_finder.FindPath(_grid, start, _goal, _rawPath))
            {
                HasFailed = true;
                return;
            }

            GridPathfinder.Smooth(_grid, _rawPath, _path);
            // The first cell is where she stands.
            _pathIndex = _path.Count > 1 ? 1 : 0;
            ResetProgress();
            // Twice the time the path takes at her pace, and a few seconds more: past that something is wrong.
            float speed = Mathf.Max(0.1f, _movement.ToMotorConfig().WalkSpeed * _settings.WalkSpeedFactor);
            _walkDeadline = Time.time + PathLength() / speed * 2f + 4f;
        }

        private void Replan()
        {
            if (++_repaths > _settings.MaxRepaths)
            {
                HasFailed = true;
                return;
            }

            Plan();
        }

        private void Arrive()
        {
            HasArrived = true;
            _path.Clear();
        }

        // Barely moving while trying to walk: something the grid doesn't know is in the way. Mark the cell ahead and
        // find a way around it.
        // Stuck means not getting closer to where she goes, however much she moves: pushing against something, or hopping at
        // a ledge she can't clear and sliding back, both count. She tries one jump, then marks the spot ahead blocked and
        // finds another way; too many tries and the destination counts as unreachable.
        private void CheckStuck()
        {
            if (Time.time > _walkDeadline)
            {
                Trace("walked too long without arriving; finding another way");
                Replan();
                return;
            }

            if (_pathIndex < _path.Count)
            {
                float distance = HorizontalDistance(_grid.CenterOf(_path[_pathIndex].x, _path[_pathIndex].y));
                if (_pathIndex != _progressWaypoint || distance < _closestToWaypoint - _settings.StuckDistance)
                {
                    _progressWaypoint = _pathIndex;
                    _closestToWaypoint = distance;
                    _lastProgressTime = Time.time;
                    _triedJumpHere = false;
                    return;
                }
            }

            if (Time.time - _lastProgressTime < _settings.StuckSeconds * 2f)
            {
                return;
            }

            _lastProgressTime = Time.time;
            // Something the grid doesn't know is in the way (a basket set down): try once to jump over it first.
            if (_settings.CanJump && !_triedJumpHere && _body.isGrounded)
            {
                _triedJumpHere = true;
                Trace("is stuck; tries a jump");
                Jump();
                return;
            }

            _triedJumpHere = false;
            Vector3 ahead = transform.position + transform.forward * (_body.radius + _grid.CellSize * 0.5f);
            if (_grid.TryCellOf(ahead, out Vector2Int blocked))
            {
                _grid.BlockUntil(blocked.x, blocked.y, Time.time + _settings.BlockSeconds);
            }

            Trace("is stuck; finding another way");
            Replan();
        }

        private void ResetProgress()
        {
            _progressWaypoint = -1;
            _closestToWaypoint = float.MaxValue;
            _lastProgressTime = Time.time;
            _triedJumpHere = false;
        }

        private float PathLength()
        {
            float length = 0f;
            Vector3 previous = transform.position;
            for (int i = _pathIndex; i < _path.Count; i++)
            {
                Vector3 point = _grid.CenterOf(_path[i].x, _path[i].y);
                length += Vector3.Distance(previous, point);
                previous = point;
            }

            return length;
        }

        private float HorizontalDistance(Vector3 point)
        {
            float dx = point.x - transform.position.x;
            float dz = point.z - transform.position.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // For the log while testing: only when something goes wrong.
        private void Trace(string message)
        {
            if (DevTools.Enabled)
            {
                Debug.Log($"[Sally] {message}", this);
            }
        }

        private void Turn(float deltaTime)
        {
            if (!_turning)
            {
                return;
            }

            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetYaw, ref _yawVelocity, _settings.TurnSmoothTime,
                _settings.MaxTurnSpeed, deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, _targetYaw)) < 0.1f && Mathf.Abs(_yawVelocity) < 1f)
            {
                _turning = false;
            }
        }

        // Settling ash slid into where she stands: lift her on top of it rather than leave her inside.
        private void OnSettled(Bounds changed)
        {
            float lift = 0f;
            float half = _body.height * 0.5f - _body.radius;
            Vector3 feet = transform.TransformPoint(_body.center) + Vector3.down * half;
            while (lift <= 3f && _pile.SampleDensityAt(feet + Vector3.up * lift) > 0.2f)
            {
                lift += 0.05f;
            }

            if (lift > 0f)
            {
                _body.enabled = false;
                transform.position += Vector3.up * lift;
                _body.enabled = true;
            }
        }

        private void AttachTerrain()
        {
            if (_pile.Terrain != null)
            {
                _grid.AttachTerrain(_pile.Terrain.World, _pile.transform.position, _settings.MinAshThickness);
            }
        }

        private void OnRestored()
        {
            _grid.MarkTerrainDirty();
        }

        // The fixed scenery, once: which cells an obstacle stands in, and the ground height everywhere else. The ash,
        // characters and triggers don't count; the ash comes from the voxels.
        private void BuildGrid()
        {
            float cell = _settings.CellSize;
            int width = Mathf.CeilToInt(_settings.GridSize.x / cell);
            int depth = Mathf.CeilToInt(_settings.GridSize.y / cell);
            float maxJump = _settings.CanJump ? _movement.ToMotorConfig().JumpHeight * _settings.JumpReach : 0f;
            _grid = new NavGrid(_settings.GridMin, width, depth, cell, _settings.MaxStep, maxJump, _settings.MaxDrop);
            float clearance = _body.radius * 0.6f + cell * 0.5f;
            float bodyHeight = _body.height;
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 center = _grid.CenterOf(x, z);
                    if (!TryFindGround(center, out float ground))
                    {
                        _grid.SetStatic(x, z, true, 0f);
                        continue;
                    }

                    // From a step's height above the ground to the top of her head: anything there is in the way.
                    float bottom = ground + _body.stepOffset + 0.05f;
                    Vector3 boxCenter = new Vector3(center.x, (bottom + ground + bodyHeight) * 0.5f, center.z);
                    Vector3 halfExtents = new Vector3(clearance, (ground + bodyHeight - bottom) * 0.5f, clearance);
                    _grid.SetStatic(x, z, IsObstructed(boxCenter, halfExtents), ground);
                }
            }

            _ignoredColliders.Clear();
        }

        private bool TryFindGround(Vector3 cellCenter, out float ground)
        {
            ground = 0f;
            Vector3 from = new Vector3(cellCenter.x, 4f, cellCenter.z);
            int count = Physics.RaycastNonAlloc(from, Vector3.down, _hits, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i].collider;
                if (IsIgnored(hit))
                {
                    continue;
                }

                if (!found || _hits[i].point.y > ground)
                {
                    ground = _hits[i].point.y;
                    found = true;
                }
            }

            return found;
        }

        private bool IsObstructed(Vector3 center, Vector3 halfExtents)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlaps, Quaternion.identity, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsIgnored(_overlaps[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // Characters (the player and what they hold, herself) move; the ash is read from the voxels.
        private bool IsIgnored(Collider hit)
        {
            if (!_ignoredColliders.TryGetValue(hit, out bool ignored))
            {
                ignored = hit.GetComponentInParent<CharacterController>() != null || hit.GetComponentInParent<VoxelTerrain>() != null;
                _ignoredColliders[hit] = ignored;
            }

            return ignored;
        }
    }
}
