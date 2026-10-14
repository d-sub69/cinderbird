using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>Where a falling loose ember is after one step of <see cref="LooseEmberFall.Advance"/>.</summary>
    public readonly struct FallStep
    {
        public FallStep(Vector3 position, float speed, bool resting)
        {
            Position = position;
            Speed = speed;
            Resting = resting;
        }

        public Vector3 Position { get; }

        /// <summary>Downward speed in m/s; negative while it bounces up.</summary>
        public float Speed { get; }

        /// <summary>It landed too slowly to bounce and stays put.</summary>
        public bool Resting { get; }
    }
}
