using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Resting spots for embers in the sieve, spread evenly over a disc so they never sit on top of each other.
    /// </summary>
    public static class SieveSpotLayout
    {
        // The golden angle (~137.5°) gives the most even spiral packing in a disc (Vogel's model).
        private static readonly float GoldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

        /// <summary>Average distance between neighboring spots; jitter is measured against this.</summary>
        public static float NominalSpacing(int count, float radius)
        {
            return radius * Mathf.Sqrt(Mathf.PI / count);
        }

        /// <param name="jitter">Random offset as a fraction of <see cref="NominalSpacing"/>.
        /// Use <see cref="MaxSafeJitter"/> to keep spots a minimum distance apart.</param>
        public static Vector2[] CreateSpots(int count, float radius, float jitter, System.Random random)
        {
            Vector2[] spots = CreateEvenSpots(count, radius);
            float maxOffset = NominalSpacing(count, radius) * jitter * 0.5f;
            if (maxOffset <= 0f)
            {
                return spots;
            }

            for (int i = 0; i < count; i++)
            {
                float offsetAngle = (float)random.NextDouble() * Mathf.PI * 2f;
                float offsetDistance = maxOffset * (float)random.NextDouble();
                spots[i] += new Vector2(Mathf.Cos(offsetAngle), Mathf.Sin(offsetAngle)) * offsetDistance;
            }

            return spots;
        }

        /// <summary>
        /// Largest jitter that still keeps every pair of spots at least <paramref name="minDistance"/> apart.
        /// Each spot moves at most half the jitter distance, so a pair closes by at most the full jitter distance.
        /// Negative means even an unjittered layout is too tight.
        /// </summary>
        public static float MaxSafeJitter(int count, float radius, float minDistance)
        {
            float evenMinimum = MinimumDistance(CreateEvenSpots(count, radius));
            return (evenMinimum - minDistance) / NominalSpacing(count, radius);
        }

        public static float MinimumDistance(Vector2[] spots)
        {
            float minimum = float.MaxValue;
            for (int i = 0; i < spots.Length; i++)
            {
                for (int j = i + 1; j < spots.Length; j++)
                {
                    minimum = Mathf.Min(minimum, Vector2.Distance(spots[i], spots[j]));
                }
            }

            return minimum;
        }

        private static Vector2[] CreateEvenSpots(int count, float radius)
        {
            Vector2[] spots = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float distance = radius * Mathf.Sqrt((i + 0.5f) / count);
                float angle = i * GoldenAngle;
                spots[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            }

            return spots;
        }
    }
}
