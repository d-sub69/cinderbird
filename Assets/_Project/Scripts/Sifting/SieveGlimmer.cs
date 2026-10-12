using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// How strongly an ember buried in the sieve's ash shows through: faint under a thick layer,
    /// brighter as shaking thins it, and gone once the ash surface reaches the ember (it's visible itself then).
    /// </summary>
    public static class SieveGlimmer
    {
        /// <param name="ashHeight">Height of the ash surface above the sieve floor.</param>
        /// <param name="emberTop">Height of the top of the ember above the sieve floor.</param>
        /// <param name="maxDepth">Depth at which the glimmer is at its faintest.</param>
        /// <param name="deepBrightness">Brightness at <paramref name="maxDepth"/> or deeper, 0..1.</param>
        /// <returns>0 when uncovered, otherwise between <paramref name="deepBrightness"/> and 1.</returns>
        public static float Brightness(float ashHeight, float emberTop, float maxDepth, float deepBrightness)
        {
            float depth = ashHeight - emberTop;
            if (depth <= 0f)
            {
                return 0f;
            }

            float closeness = maxDepth > 0f ? 1f - Mathf.Clamp01(depth / maxDepth) : 1f;
            return Mathf.Lerp(Mathf.Clamp01(deepBrightness), 1f, closeness);
        }
    }
}
