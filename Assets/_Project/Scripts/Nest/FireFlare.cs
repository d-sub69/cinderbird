using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// How strongly the nest fire burns: a base level that follows overall progress, plus short flares
    /// from fed embers that fade out by halving every <c>halfLife</c> seconds.
    /// </summary>
    public sealed class FireFlare
    {
        // Below this a flare is invisible; snapping to zero lets the view stop updating.
        private const float FlareCutoff = 0.0001f;

        private readonly float _halfLife;
        private readonly float _maxFlare;

        public FireFlare(float halfLife, float maxFlare)
        {
            _halfLife = Mathf.Max(0.0001f, halfLife);
            _maxFlare = Mathf.Max(0f, maxFlare);
        }

        /// <summary>0 = campfire, 1 = roaring; follows Heat fed ÷ Heat goal.</summary>
        public float BaseLevel { get; private set; }

        /// <summary>Extra intensity on top of the base, 0..maxFlare.</summary>
        public float Flare { get; private set; }

        public void SetProgress(float progress)
        {
            BaseLevel = Mathf.Clamp01(progress);
        }

        public void AddFlare(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            Flare = Mathf.Min(_maxFlare, Flare + amount);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || Flare <= 0f)
            {
                return;
            }

            // Exponential decay is frame-rate independent, unlike subtracting a fixed amount per frame.
            Flare *= Mathf.Pow(0.5f, deltaTime / _halfLife);
            if (Flare < FlareCutoff)
            {
                Flare = 0f;
            }
        }
    }
}
