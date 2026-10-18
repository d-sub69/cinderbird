using System;
using System.Collections.Generic;

namespace Cinderbird.Pacing
{
    /// <summary>
    /// Everything the pacing simulator plays a run with: the mountain's layers, what each ember tier is worth, the Heat
    /// levels, the upgrades, the shake, the helper and the wind, and how the player plays (<see cref="PaceBehavior"/>).
    /// Built from the game's real data by the pacing report; built by hand in tests.
    /// </summary>
    public sealed class PaceModel
    {
        public PaceModel(IReadOnlyList<PaceLayer> layers, IReadOnlyList<int> glowByTier, IReadOnlyList<int> heatByTier,
            IReadOnlyList<bool> tierSlips, IReadOnlyList<int> heatThresholds, IReadOnlyList<PaceUpgrade> upgrades,
            PaceBehavior behavior, float shakeLitersPerSecond, float helperSieveSeconds, float helperScoopVolume,
            int windHeatLevel, float windDepth)
        {
            Layers = layers ?? Array.Empty<PaceLayer>();
            GlowByTier = glowByTier ?? Array.Empty<int>();
            HeatByTier = heatByTier ?? Array.Empty<int>();
            TierSlips = tierSlips ?? Array.Empty<bool>();
            HeatThresholds = heatThresholds ?? Array.Empty<int>();
            Upgrades = upgrades ?? Array.Empty<PaceUpgrade>();
            Behavior = behavior;
            ShakeLitersPerSecond = Math.Max(1f, shakeLitersPerSecond);
            HelperSieveSeconds = Math.Max(0.5f, helperSieveSeconds);
            HelperScoopVolume = Math.Max(0f, helperScoopVolume);
            WindHeatLevel = windHeatLevel;
            WindDepth = Math.Max(0f, windDepth);
        }

        public IReadOnlyList<PaceLayer> Layers { get; }

        public IReadOnlyList<int> GlowByTier { get; }

        public IReadOnlyList<int> HeatByTier { get; }

        /// <summary>Per tier: whether it can slip through the mesh (the smallest sparks).</summary>
        public IReadOnlyList<bool> TierSlips { get; }

        /// <summary>Heat for level 1, 2, … in order.</summary>
        public IReadOnlyList<int> HeatThresholds { get; }

        public IReadOnlyList<PaceUpgrade> Upgrades { get; }

        public PaceBehavior Behavior { get; }

        /// <summary>Liters a second the sieve shakes out at full energy.</summary>
        public float ShakeLitersPerSecond { get; }

        /// <summary>Seconds one of the helper's sieves takes (scoop, pour, shake) at her starting pace.</summary>
        public float HelperSieveSeconds { get; }

        /// <summary>Cubic meters her scoop takes.</summary>
        public float HelperScoopVolume { get; }

        /// <summary>Heat level the Warm Wind comes with.</summary>
        public int WindHeatLevel { get; }

        /// <summary>Meters of ash a gust strips.</summary>
        public float WindDepth { get; }
    }
}
