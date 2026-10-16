using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// How a helper walks, finds her way, picks her work and where she may be sent. Her walking speed and gravity are the
    /// player's (<see cref="Player.PlayerMovementSettings"/>); her pace and sieve size come from her upgrades.
    /// </summary>
    [CreateAssetMenu(fileName = "HelperSettings", menuName = "Cinderbird/Helper Settings")]
    public sealed class HelperSettings : ScriptableObject
    {
        [Header("Walk grid")]
        [Tooltip("World x/z of the walk grid's corner; the grid must cover the yard and the mountain.")]
        [SerializeField] private Vector2 _gridMin = new Vector2(-26f, 2f);
        [Tooltip("Size of the walk grid in meters (x, z).")]
        [SerializeField] private Vector2 _gridSize = new Vector2(52f, 64f);
        [SerializeField, Min(0.1f)] private float _cellSize = 0.5f;
        [Tooltip("Steepest slope she walks up, in degrees (the player's controller limit is a good match).")]
        [SerializeField, Range(5f, 80f)] private float _maxSlope = 45f;
        [Tooltip("Ash thinner than this over the ground is walked as ground (meters).")]
        [SerializeField, Min(0f)] private float _minAshThickness = 0.05f;
        [Tooltip("Cells one path search may expand before it gives up.")]
        [SerializeField, Min(100)] private int _maxSearchCells = 12000;
        [Tooltip("Extra path cost per meter climbed, so she prefers flat ground.")]
        [SerializeField, Min(0f)] private float _climbCost = 2f;

        [Header("Walking")]
        [Tooltip("She jumps up onto ledges and over low obstacles in her way (the player's jump).")]
        [SerializeField] private bool _canJump = true;
        [Tooltip("Highest ledge she plans to jump onto, as a share of the player's jump height (a margin so she clears it).")]
        [SerializeField, Range(0.3f, 1f)] private float _jumpReach = 0.8f;
        [Tooltip("Deepest drop she steps down off a ledge (meters).")]
        [SerializeField, Min(0f)] private float _maxDrop = 2f;
        [Tooltip("Her walking speed as a share of the player's.")]
        [SerializeField, Range(0.1f, 1f)] private float _walkSpeedFactor = 0.55f;
        [Tooltip("How close to a waypoint counts as reached (meters).")]
        [SerializeField, Min(0.05f)] private float _waypointRadius = 0.35f;
        [Tooltip("Seconds her turn takes to settle; smaller turns snappier.")]
        [SerializeField, Min(0.01f)] private float _turnSmoothTime = 0.15f;
        [SerializeField, Min(10f)] private float _maxTurnSpeed = 420f;
        [Tooltip("Facing within this many degrees counts as facing.")]
        [SerializeField, Range(1f, 30f)] private float _faceTolerance = 6f;
        [Tooltip("Moving less than this (meters) over the stuck time while walking means she is stuck.")]
        [SerializeField, Min(0.01f)] private float _stuckDistance = 0.15f;
        [SerializeField, Min(0.1f)] private float _stuckSeconds = 0.8f;
        [Tooltip("Seconds the spot she bumped into stays out of her paths.")]
        [SerializeField, Min(0.5f)] private float _blockSeconds = 6f;
        [Tooltip("New paths she tries after getting stuck before she gives the destination up.")]
        [SerializeField, Min(0)] private int _maxRepaths = 4;

        [Header("Work")]
        [Tooltip("Seconds one sieve takes at her base pace: the scoop pouring in plus the shake. Her pace upgrade shortens it.")]
        [SerializeField, Min(0.5f)] private float _sieveSeconds = 4f;
        [Tooltip("Meters between the spots she looks at.")]
        [SerializeField, Min(0.1f)] private float _surveyStride = 0.5f;
        [Tooltip("Open ash thinner than this (meters) isn't worth a scoop.")]
        [SerializeField, Min(0f)] private float _minAshDepth = 0.15f;
        [Tooltip("Spots up to this much farther than the nearest one count as near; the lowest of them is dug first (meters).")]
        [SerializeField, Min(0f)] private float _nearBand = 1.5f;
        [Tooltip("She stands this far from the spot she scoops, horizontally (meters).")]
        [SerializeField] private Vector2 _scoopDistance = new Vector2(0.55f, 1.35f);
        [Tooltip("Highest a spot may be above where she stands (meters).")]
        [SerializeField, Min(0f)] private float _reachUp = 1.4f;
        [Tooltip("Lowest a spot may be below where she stands (meters).")]
        [SerializeField, Min(0f)] private float _reachDown = 1.2f;
        [Tooltip("Height of her hands above her feet, where scoops start (meters).")]
        [SerializeField, Min(0f)] private float _handsHeight = 0.9f;
        [Tooltip("Seconds before she tries a spot she couldn't reach again (slides and digging change the way there).")]
        [SerializeField, Min(1f)] private float _retryUnreachableSeconds = 90f;
        [Tooltip("With nothing to do, she looks for work again this often (seconds).")]
        [SerializeField, Min(0.2f)] private float _idleRecheckSeconds = 2f;

        [Header("Nest and home")]
        [Tooltip("She pours standing this far beyond the nest's edge, horizontally (meters).")]
        [SerializeField] private Vector2 _nestDistance = new Vector2(0.4f, 1.6f);
        [Tooltip("How close to her spot by the shop counts as home (meters).")]
        [SerializeField, Min(0.1f)] private float _homeRadius = 0.75f;

        [Header("Sending her")]
        [Tooltip("How far the player can point when choosing where to send her (meters).")]
        [SerializeField, Min(1f)] private float _assignReach = 30f;
        [Tooltip("How close to the spot she was sent to counts as there (meters).")]
        [SerializeField, Min(0.1f)] private float _spotRadius = 0.6f;
        [Tooltip("Sent to the ground, the pile must be this close (meters).")]
        [SerializeField, Min(0.5f)] private float _maxDistanceFromAsh = 3f;
        [SerializeField] private Color _validColor = new Color(0.3f, 1f, 0.4f, 0.6f);
        [SerializeField] private Color _invalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        public Vector2 GridMin => _gridMin;
        public Vector2 GridSize => _gridSize;
        public float CellSize => _cellSize;
        public float MaxSlope => _maxSlope;
        public float MinAshThickness => _minAshThickness;
        public int MaxSearchCells => _maxSearchCells;
        public float ClimbCost => _climbCost;
        public bool CanJump => _canJump;
        public float JumpReach => _jumpReach;
        public float MaxDrop => _maxDrop;
        public float WalkSpeedFactor => _walkSpeedFactor;
        public float SieveSeconds => _sieveSeconds;
        public float WaypointRadius => _waypointRadius;
        public float TurnSmoothTime => _turnSmoothTime;
        public float MaxTurnSpeed => _maxTurnSpeed;
        public float FaceTolerance => _faceTolerance;
        public float StuckDistance => _stuckDistance;
        public float StuckSeconds => _stuckSeconds;
        public float BlockSeconds => _blockSeconds;
        public int MaxRepaths => _maxRepaths;
        public float SurveyStride => _surveyStride;
        public float MinAshDepth => _minAshDepth;
        public float NearBand => _nearBand;
        public Vector2 ScoopDistance => _scoopDistance;
        public float ReachUp => _reachUp;
        public float ReachDown => _reachDown;
        public float HandsHeight => _handsHeight;
        public float RetryUnreachableSeconds => _retryUnreachableSeconds;
        public float IdleRecheckSeconds => _idleRecheckSeconds;
        public Vector2 NestDistance => _nestDistance;
        public float HomeRadius => _homeRadius;
        public float AssignReach => _assignReach;
        public float MaxDistanceFromAsh => _maxDistanceFromAsh;
        public float SpotRadius => _spotRadius;
        public Color ValidColor => _validColor;
        public Color InvalidColor => _invalidColor;

        /// <summary>Highest climb from one cell to the next straight neighbor (meters).</summary>
        public float MaxStep => _cellSize * Mathf.Tan(_maxSlope * Mathf.Deg2Rad);
    }
}
