using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// The shared twinkle of glimmer dots, so the pile and the sieve shimmer the same way.
    /// </summary>
    public static class GlimmerTwinkle
    {
        /// <summary>Golden-angle spacing spreads phases evenly so neighboring dots don't twinkle in sync.</summary>
        public static float PhaseFor(int index)
        {
            return index * 2.39996f;
        }

        /// <param name="speed">Twinkles per second; 0 means a steady glow.</param>
        /// <param name="amount">How far the twinkle dims the glow, 0..1.</param>
        /// <returns>Brightness multiplier between 1 - amount and 1.</returns>
        public static float Dim(float time, float phase, float speed, float amount)
        {
            if (speed <= 0f || amount <= 0f)
            {
                return 1f;
            }

            float wave = Mathf.Sin(time * speed * Mathf.PI * 2f + phase) * 0.5f + 0.5f;
            return 1f - Mathf.Clamp01(amount) * wave;
        }
    }
}
