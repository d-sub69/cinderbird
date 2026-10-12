using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Sifting;

namespace Cinderbird.Nest
{
    /// <summary>
    /// The rules for pouring the basket into the nest: every ember goes in and pays out its Glow and Heat.
    /// </summary>
    public static class NestFeeding
    {
        /// <param name="fedEmbers">Cleared, then filled with the embers that went in (for visuals).</param>
        public static FeedResult Feed(
            BasketContents basket,
            Currencies currencies,
            IReadOnlyList<int> glowByTier,
            IReadOnlyList<int> heatByTier,
            List<Ember> fedEmbers)
        {
            fedEmbers.Clear();
            if (basket.TakeAll(fedEmbers) == 0)
            {
                return default;
            }

            FeedResult result = EmberValuation.Evaluate(fedEmbers, glowByTier, heatByTier);
            currencies.Add(result.Glow, result.Heat);
            return result;
        }
    }
}
