using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Blends per-layer colors by how much of each layer a dig took, so a scoop across a layer boundary
    /// shows a mix in the sieve.
    /// </summary>
    public static class LayerColorMix
    {
        /// <param name="amountByLayer">How much came from each layer (any unit); index = layer.</param>
        /// <param name="colorByLayer">Color per layer; layers without a color are skipped.</param>
        /// <param name="fallback">Returned when nothing was taken.</param>
        public static Color Blend(IReadOnlyList<float> amountByLayer, IReadOnlyList<Color> colorByLayer, Color fallback)
        {
            float total = 0f;
            Color sum = Color.clear;
            int count = Mathf.Min(amountByLayer.Count, colorByLayer.Count);
            for (int layer = 0; layer < count; layer++)
            {
                float amount = amountByLayer[layer];
                if (amount <= 0f)
                {
                    continue;
                }

                sum += colorByLayer[layer] * amount;
                total += amount;
            }

            return total > 0f ? sum / total : fallback;
        }
    }
}
