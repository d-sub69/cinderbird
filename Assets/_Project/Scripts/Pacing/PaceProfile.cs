using UnityEngine;

namespace Cinderbird.Pacing
{
    /// <summary>
    /// How a real player plays, for the pacing simulator: the numbers a real-speed playtest's pace.log calibrates. The
    /// rest of the simulated run comes from the game's own data (mountain, embers, prices, settings).
    /// </summary>
    [CreateAssetMenu(fileName = "PacingProfile", menuName = "Cinderbird/Pacing Profile")]
    public sealed class PaceProfile : ScriptableObject
    {
        [Header("The player's sifting")]
        [Tooltip("Seconds per scoop besides shaking: aiming, the scoop, the ash pouring in.")]
        [SerializeField, Min(0f)] private float _scoopOverheadSeconds = 1.0f;
        [Tooltip("Average shake energy (0..1) a player keeps up.")]
        [SerializeField, Range(0.05f, 1f)] private float _shakeEnergy = 0.85f;
        [Tooltip("Share of play spent sifting, besides trips and shopping (looking around, reading, wandering).")]
        [SerializeField, Range(0.05f, 1f)] private float _activeShare = 0.95f;
        [Tooltip("Seconds to carry a full basket to the nest and come back.")]
        [SerializeField, Min(0f)] private float _tripSeconds = 20f;
        [Tooltip("Seconds per shop visit.")]
        [SerializeField, Min(0f)] private float _shopSeconds = 20f;

        [Header("Powers and helpers")]
        [Tooltip("Share of the Warm Wind's ready time that is used.")]
        [SerializeField, Range(0.05f, 1f)] private float _windUse = 0.6f;
        [Tooltip("Share of a gust's circle that lands on ash.")]
        [SerializeField, Range(0f, 1f)] private float _windCoverage = 0.85f;
        [Tooltip("Seconds the helper takes to walk to the nest and back.")]
        [SerializeField, Min(0f)] private float _helperTripSeconds = 25f;

        [Header("Digging")]
        [Tooltip("Share of digging that goes into leftovers of outer layers instead of the deepest open one.")]
        [SerializeField, Range(0f, 1f)] private float _outerShare = 0.15f;
        [Tooltip("Share of sparks that slipped through the mesh which are picked up again later.")]
        [SerializeField, Range(0f, 1f)] private float _slippedPickedUp = 0.5f;
        [Tooltip("Embers found per cubic meter dug against the layer's average: aiming at glimmers finds more (1 = digging blind).")]
        [SerializeField, Range(1f, 10f)] private float _findBonus = 2f;

        [Header("Simulation")]
        [Tooltip("The simulation gives up after this many hours.")]
        [SerializeField, Min(0.5f)] private float _maxHours = 15f;

        public PaceBehavior ToBehavior()
        {
            return new PaceBehavior(_scoopOverheadSeconds, _shakeEnergy, _activeShare, _tripSeconds, _shopSeconds, _windUse,
                _windCoverage, _helperTripSeconds, _outerShare, _slippedPickedUp, _maxHours, _findBonus);
        }
    }
}
