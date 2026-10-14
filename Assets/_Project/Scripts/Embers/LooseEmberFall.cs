using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// A loose ember's drop: gravity, landing on whatever is below (<see cref="IFallSurface"/>), a small bounce
    /// that loses energy each time, and rest once it lands slower than the minimum bounce speed. A scripted fall
    /// rather than a physics body, so hundreds of embers cost almost nothing and can never tunnel through the ash.
    /// </summary>
    public static class LooseEmberFall
    {
        /// <param name="speed">Downward speed in m/s (negative while bouncing up); 0 when it starts falling.</param>
        public static FallStep Advance(Vector3 position, float speed, float radius, float deltaTime,
            in FallParameters parameters, IFallSurface surface)
        {
            speed += parameters.Gravity * deltaTime;
            float drop = speed * deltaTime;
            if (drop > 0f && surface.TryLand(position, drop, radius, out Vector3 landed))
            {
                return speed > parameters.MinBounceSpeed
                    ? new FallStep(landed, -speed * parameters.Bounce, false)
                    : new FallStep(landed, 0f, true);
            }

            return new FallStep(position + Vector3.down * drop, speed, false);
        }
    }
}
