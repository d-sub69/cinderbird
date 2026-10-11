using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Accumulates yaw and pitch from look input, with pitch clamped so the camera can't flip over.
    /// </summary>
    public sealed class LookRotation
    {
        private readonly float _maxPitch;

        public LookRotation(float maxPitch, float initialYaw = 0f)
        {
            _maxPitch = maxPitch;
            Yaw = Mathf.Repeat(initialYaw, 360f);
        }

        /// <summary>Degrees around the up axis, in [0, 360).</summary>
        public float Yaw { get; private set; }

        /// <summary>Degrees around the right axis; negative looks up (Unity's convention).</summary>
        public float Pitch { get; private set; }

        /// <param name="lookDelta">Mouse delta in pixels this frame. Not scaled by deltaTime:
        /// the delta is already "how far the mouse moved", so scaling would make look speed frame-rate dependent.</param>
        /// <param name="sensitivity">Degrees per pixel.</param>
        public void Apply(Vector2 lookDelta, float sensitivity)
        {
            Yaw = Mathf.Repeat(Yaw + lookDelta.x * sensitivity, 360f);
            Pitch = Mathf.Clamp(Pitch - lookDelta.y * sensitivity, -_maxPitch, _maxPitch);
        }
    }
}
