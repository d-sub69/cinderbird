using System.Collections.Generic;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// The design's rule for opening an ash layer: the nest has reached the layer's Heat level, and every layer
    /// above it is cleared to the layer's share (90% for most, 100% for the Heart Hollow). The share rule means
    /// nobody has to hunt for the last spark; the rest is for 100% completion.
    /// </summary>
    public static class LayerUnlockRule
    {
        // Rounding in shares (e.g. 0.8999999 for 90%) must not keep a layer shut.
        private const float ShareTolerance = 1e-4f;

        /// <param name="clearedShares">Per layer, the share of its embers taken out of the pile (0..1).</param>
        public static bool CanOpen(int layer, int heatLevel, int requiredHeatLevel, float requiredClearedShare,
            IReadOnlyList<float> clearedShares)
        {
            if (heatLevel < requiredHeatLevel)
            {
                return false;
            }

            return LeastClearedAbove(layer, clearedShares, out _) + ShareTolerance >= requiredClearedShare;
        }

        /// <summary>The least cleared layer above <paramref name="layer"/>, which is what holds it shut.</summary>
        /// <returns>Its cleared share; 1 when there is no layer above.</returns>
        public static float LeastClearedAbove(int layer, IReadOnlyList<float> clearedShares, out int leastLayer)
        {
            leastLayer = -1;
            float least = 1f;
            for (int above = 0; above < layer && above < clearedShares.Count; above++)
            {
                if (clearedShares[above] < least)
                {
                    least = clearedShares[above];
                    leastLayer = above;
                }
            }

            return least;
        }
    }
}
