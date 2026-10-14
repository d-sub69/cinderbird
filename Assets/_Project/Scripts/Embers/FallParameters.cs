namespace Cinderbird.Embers
{
    /// <summary>How a loose ember falls and bounces (see <see cref="LooseEmberFall"/>).</summary>
    public readonly struct FallParameters
    {
        /// <param name="gravity">Downward acceleration, m/s².</param>
        /// <param name="bounce">Share of the landing speed kept for the bounce back up (0..1).</param>
        /// <param name="minBounceSpeed">Below this landing speed (m/s) it comes to rest instead of bouncing.</param>
        public FallParameters(float gravity, float bounce, float minBounceSpeed)
        {
            Gravity = gravity;
            Bounce = bounce;
            MinBounceSpeed = minBounceSpeed;
        }

        public float Gravity { get; }
        public float Bounce { get; }
        public float MinBounceSpeed { get; }
    }
}
