using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Shake "energy" (0..1) from input: holding gives a base level, wiggling the mouse adds up to full.
    /// Energy eases toward its target instead of jumping, which gives the shake some weight.
    /// </summary>
    public sealed class SieveShaker
    {
        private readonly ShakeParameters _parameters;
        private float _smoothedWiggle;

        public SieveShaker(ShakeParameters parameters)
        {
            _parameters = parameters;
        }

        public float Energy { get; private set; }

        /// <param name="wiggleSpeed">Mouse speed in pixels per second.</param>
        public void Tick(bool held, float wiggleSpeed, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            float target = held ? TargetEnergy(wiggleSpeed, deltaTime) : 0f;
            if (!held)
            {
                // Start fresh on the next press rather than carrying old mouse speed over.
                _smoothedWiggle = 0f;
            }

            float transitionTime = target > Energy ? _parameters.RiseTime : _parameters.FallTime;
            float maxStep = transitionTime > 0f ? deltaTime / transitionTime : 1f;
            Energy = Mathf.MoveTowards(Energy, target, maxStep);
        }

        /// <summary>Ash to shake out over <paramref name="deltaTime"/> at the current energy, in cubic meters.</summary>
        public float AshToRemove(float deltaTime)
        {
            return Energy * _parameters.MaxRemovalRate * Mathf.Max(0f, deltaTime);
        }

        private float TargetEnergy(float wiggleSpeed, float deltaTime)
        {
            float smoothing = _parameters.WiggleSmoothingTime;
            float blend = smoothing > 0f ? 1f - Mathf.Exp(-deltaTime / smoothing) : 1f;
            _smoothedWiggle += (Mathf.Max(0f, wiggleSpeed) - _smoothedWiggle) * blend;

            float wiggle = _parameters.FullEnergyWiggleSpeed > 0f
                ? Mathf.Clamp01(_smoothedWiggle / _parameters.FullEnergyWiggleSpeed)
                : 0f;
            float hold = Mathf.Clamp01(_parameters.HoldEnergy);
            return hold + (1f - hold) * wiggle;
        }
    }
}
