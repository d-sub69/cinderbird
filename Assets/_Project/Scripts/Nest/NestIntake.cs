using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;

namespace Cinderbird.Nest
{
    /// <summary>
    /// The nest as a pour target: takes any number of embers and pays out their Glow and Heat, plus any bonuses.
    /// </summary>
    public sealed class NestIntake : IEmberSink
    {
        private readonly Currencies _currencies;
        private readonly IReadOnlyList<int> _glowByTier;
        private readonly IReadOnlyList<int> _heatByTier;
        private readonly FeedBonuses _bonuses;

        /// <param name="bonuses">Optional: extra Glow and Heat on top of the embers' values.</param>
        public NestIntake(Currencies currencies, IReadOnlyList<int> glowByTier, IReadOnlyList<int> heatByTier, FeedBonuses bonuses = null)
        {
            _bonuses = bonuses;
            _currencies = currencies;
            _glowByTier = glowByTier;
            _heatByTier = heatByTier;
        }

        /// <summary>The nest is never full.</summary>
        public int FreeSpace => int.MaxValue;

        /// <summary>What the most recent feeding earned.</summary>
        public FeedResult LastResult { get; private set; }

        public void Add(List<Ember> embers)
        {
            if (embers.Count == 0)
            {
                return;
            }

            FeedResult earned = EmberValuation.Evaluate(embers, _glowByTier, _heatByTier);
            int glow = earned.Glow;
            int heat = earned.Heat;
            _bonuses?.Apply(ref glow, ref heat);
            LastResult = new FeedResult(earned.EmberCount, glow, heat);
            _currencies.Add(LastResult.Glow, LastResult.Heat);
        }
    }
}
