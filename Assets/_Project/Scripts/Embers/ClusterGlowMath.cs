using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// How bright a cluster glows and how big its glowing patch on the surface is. Brightness follows what its embers are
    /// worth (not how deep it is); the patch is a small spot when the cluster is deep and its full size once dug into.
    /// </summary>
    public static class ClusterGlowMath
    {
        /// <returns>0 when the cluster is empty or deeper than <paramref name="reach"/>.</returns>
        public static float Intensity(float worth, float depth, float reach, float perWorth, float exponent, float max)
        {
            if (worth <= 0f || depth > reach)
            {
                return 0f;
            }

            return Mathf.Min(max, perWorth * Mathf.Pow(worth, exponent));
        }

        /// <param name="radius">How far the cluster's remaining embers spread.</param>
        /// <param name="depth">Ash between the cluster's middle and the open surface.</param>
        public static float PatchRadius(float radius, float depth, float reach, float deepScale, float minRadius)
        {
            float closeness = 1f - Mathf.Clamp01(depth / Mathf.Max(0.01f, reach));
            return Mathf.Max(minRadius, radius * Mathf.Lerp(deepScale, 1f, closeness));
        }
    }
}
