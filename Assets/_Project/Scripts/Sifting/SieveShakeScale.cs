namespace Cinderbird.Sifting
{
    /// <summary>
    /// How much faster a bigger sieve shakes out its ash: in proportion to how much more it scoops (its radius cubed), so
    /// a full sieve of any size shakes out in the same time and a bigger sieve really digs faster. A half-full one still
    /// takes half the time.
    /// </summary>
    public static class SieveShakeScale
    {
        /// <param name="baseRadius">The first sieve's scoop radius (scale 1).</param>
        /// <param name="radius">This sieve's scoop radius.</param>
        public static float For(float baseRadius, float radius)
        {
            if (baseRadius <= 0f || radius <= 0f)
            {
                return 1f;
            }

            float ratio = radius / baseRadius;
            return ratio * ratio * ratio;
        }
    }
}
