using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// How an ember lying loose sits on the ash. Once found it is never buried again: it keeps a rest height above
    /// the surface under it (<see cref="Embers.LooseEmber.Rest"/>), so ash piling onto it lifts it and ash running
    /// away under it lets it down; the rest only ever grows, so it never sinks deeper than it was found. Read
    /// straight from the density field, so it holds even before colliders catch up with a change.
    /// </summary>
    public static class LooseEmberRules
    {
        // How far under its bottom an ember feels for ash to stand on.
        private const float SupportProbe = 0.03f;
        // Step of the search for the surface; refined between the two samples either side of it.
        private const float SearchStep = 0.05f;

        /// <summary>Whether ash right under (or around) the ember holds it up.</summary>
        public static bool RestsOnAsh(IDensityField field, Vector3 center, float radius)
        {
            return field.SampleDensity(center) >= 0f || field.SampleDensity(center + Vector3.down * (radius + SupportProbe)) >= 0f;
        }

        /// <summary>
        /// The height of the ash surface straight above or below a loose ember: up through ash that came over it, or
        /// down to the ash it lies on (as far as its bottom plus a little).
        /// </summary>
        /// <param name="center">Ember center in the field's local space.</param>
        /// <param name="maxAbove">How far above its center to look for the top of ash covering it.</param>
        /// <returns>False when its center is in the open with no ash right under it (it should fall).</returns>
        public static bool TryFindSurface(IDensityField field, Vector3 center, float radius, float maxAbove, out float surfaceY)
        {
            float density = field.SampleDensity(center);
            float direction = density > 0f ? 1f : -1f;
            float reach = density > 0f ? maxAbove : radius + SupportProbe;
            float y = center.y;
            for (float travelled = 0f; travelled < reach; travelled += SearchStep)
            {
                float nextY = y + direction * SearchStep;
                float next = field.SampleDensity(new Vector3(center.x, nextY, center.z));
                if (next > 0f != density > 0f)
                {
                    surfaceY = Crossing(y, density, nextY, next);
                    return true;
                }

                y = nextY;
                density = next;
            }

            // Covered deeper than the search reaches: the top of the search is as good as it gets this time.
            surfaceY = y;
            return direction > 0f;
        }

        /// <summary>
        /// Where a loose ember sits on a surface: its rest above it, which only ever grows (so it never sinks deeper)
        /// and stays between half sunk (0) and lying on top (its radius). Ash over it lifts it; ash gone from under it
        /// lets it down.
        /// </summary>
        /// <param name="rest">Its rest so far; NaN when not measured yet, then it keeps the height it has now.</param>
        /// <returns>The new height of its center.</returns>
        public static float Settle(float centerY, float surfaceY, float rest, float radius, out float newRest)
        {
            float height = centerY - surfaceY;
            newRest = float.IsNaN(rest) ? height : Mathf.Max(rest, Mathf.Min(height, radius));
            newRest = Mathf.Clamp(newRest, 0f, radius);
            return surfaceY + newRest;
        }

        // Where the density crosses 0 between two heights.
        private static float Crossing(float y0, float d0, float y1, float d1)
        {
            float span = d0 - d1;
            return Mathf.Abs(span) > 1e-6f ? y0 + (y1 - y0) * (d0 / span) : (y0 + y1) * 0.5f;
        }
    }
}
