namespace Cinderbird.Pacing
{
    /// <summary>
    /// How a player (and the helper) actually plays, as the pacing simulator models it. These are the numbers a
    /// real-speed playtest calibrates: the rest of the model comes straight from the game's data.
    /// </summary>
    public readonly struct PaceBehavior
    {
        public PaceBehavior(float scoopOverheadSeconds, float shakeEnergy, float activeShare, float tripSeconds,
            float shopSeconds, float windUse, float windCoverage, float helperTripSeconds, float outerShare,
            float slippedPickedUp, float maxHours, float findBonus = 1f)
        {
            ScoopOverheadSeconds = scoopOverheadSeconds;
            ShakeEnergy = shakeEnergy;
            ActiveShare = activeShare;
            TripSeconds = tripSeconds;
            ShopSeconds = shopSeconds;
            WindUse = windUse;
            WindCoverage = windCoverage;
            HelperTripSeconds = helperTripSeconds;
            OuterShare = outerShare;
            SlippedPickedUp = slippedPickedUp;
            MaxHours = maxHours;
            FindBonus = findBonus;
        }

        /// <summary>Seconds per scoop besides shaking: aiming, the scoop itself, the ash pouring in.</summary>
        public float ScoopOverheadSeconds { get; }

        /// <summary>Average shake energy (0..1): the share of the sieve's top shaking speed a player keeps up.</summary>
        public float ShakeEnergy { get; }

        /// <summary>Share of play spent sifting, besides walking to the nest and shopping (looking around, reading).</summary>
        public float ActiveShare { get; }

        /// <summary>Seconds to carry a full basket to the nest and come back.</summary>
        public float TripSeconds { get; }

        /// <summary>Seconds a shop visit takes (walking there, choosing).</summary>
        public float ShopSeconds { get; }

        /// <summary>Share of the Warm Wind's ready time that is used (0..1).</summary>
        public float WindUse { get; }

        /// <summary>Share of a gust's circle that lands on ash.</summary>
        public float WindCoverage { get; }

        /// <summary>Seconds the helper takes to walk to the nest and back.</summary>
        public float HelperTripSeconds { get; }

        /// <summary>Share of digging that goes into the leftovers of outer layers rather than the deepest open one.</summary>
        public float OuterShare { get; }

        /// <summary>Share of sparks that slipped through the mesh which are picked up again later.</summary>
        public float SlippedPickedUp { get; }

        /// <summary>The simulation gives up after this many hours.</summary>
        public float MaxHours { get; }

        /// <summary>
        /// Embers found per cubic meter dug, against the layer's average: aiming at glimmers finds more than digging
        /// blind (1 = blind). Only while the layer still has that many to find.
        /// </summary>
        public float FindBonus { get; }

        /// <summary>
        /// Calibrated on Art's first real-speed playtest (20 min, hand sieve, Cold Crust): a scoop every 3.0 s while
        /// sifting almost nonstop, and twice the layer's embers per cubic meter from aiming at glimmers.
        /// </summary>
        public static PaceBehavior Default => new PaceBehavior(1.0f, 0.85f, 0.95f, 20f, 20f, 0.6f, 0.85f, 25f, 0.15f, 0.5f, 15f, 2f);
    }
}
