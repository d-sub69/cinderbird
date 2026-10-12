using System.Collections.Generic;
using Cinderbird.Embers;

namespace Cinderbird.Nest
{
    /// <summary>
    /// Adds up what embers are worth. Takes per-tier values as plain lists so it doesn't depend on
    /// ScriptableObjects and can be tested directly.
    /// </summary>
    public static class EmberValuation
    {
        /// <param name="glowByTier">Glow per tier index.</param>
        /// <param name="heatByTier">Heat per tier index.</param>
        /// <remarks>Embers with an unknown tier index count, but are worth nothing.</remarks>
        public static FeedResult Evaluate(IReadOnlyList<Ember> embers, IReadOnlyList<int> glowByTier, IReadOnlyList<int> heatByTier)
        {
            int glow = 0;
            int heat = 0;
            for (int i = 0; i < embers.Count; i++)
            {
                int tier = embers[i].TierIndex;
                glow += ValueAt(glowByTier, tier);
                heat += ValueAt(heatByTier, tier);
            }

            return new FeedResult(embers.Count, glow, heat);
        }

        private static int ValueAt(IReadOnlyList<int> values, int index)
        {
            return index >= 0 && index < values.Count ? values[index] : 0;
        }
    }
}
