using UnityEngine;

namespace Cinderbird.Sifting
{
    [CreateAssetMenu(fileName = "ShakeSettings", menuName = "Cinderbird/Shake Settings")]
    public sealed class ShakeSettings : ScriptableObject
    {
        private const float CubicMetersPerLiter = 0.001f;

        [Header("Ash removal")]
        [Tooltip("Liters of ash shaken out per second at full energy.")]
        [SerializeField, Min(0f)] private float _maxLitersPerSecond = 120f;
        [Tooltip("Energy (0..1) from holding the button without moving the mouse.")]
        [SerializeField, Range(0f, 1f)] private float _holdEnergy = 0.35f;
        [Tooltip("Mouse speed in pixels per second that counts as full energy.")]
        [SerializeField, Min(1f)] private float _fullEnergyWiggleSpeed = 1500f;

        [Header("Feel")]
        [Tooltip("Seconds from no shaking to full energy.")]
        [SerializeField, Min(0f)] private float _riseTime = 0.25f;
        [Tooltip("Seconds from full energy to stopped after letting go.")]
        [SerializeField, Min(0f)] private float _fallTime = 0.4f;
        [Tooltip("Smooths jumpy per-frame mouse speed; higher is calmer but laggier.")]
        [SerializeField, Min(0f)] private float _wiggleSmoothingTime = 0.1f;

        [Header("Sieve jiggle")]
        [Tooltip("Max position wobble in meters at full energy.")]
        [SerializeField, Min(0f)] private float _jigglePosition = 0.015f;
        [Tooltip("Max tilt wobble in degrees at full energy.")]
        [SerializeField, Min(0f)] private float _jiggleRotation = 6f;
        [Tooltip("Wobbles per second.")]
        [SerializeField, Min(0f)] private float _jiggleFrequency = 9f;

        [Header("Dust")]
        [Tooltip("Dust particles emitted per liter of ash shaken out.")]
        [SerializeField, Min(0f)] private float _dustParticlesPerLiter = 2f;

        public float JigglePosition => _jigglePosition;
        public float JiggleRotation => _jiggleRotation;
        public float JiggleFrequency => _jiggleFrequency;
        public float DustParticlesPerCubicMeter => _dustParticlesPerLiter / CubicMetersPerLiter;

        public ShakeParameters ToParameters()
        {
            return new ShakeParameters(
                _maxLitersPerSecond * CubicMetersPerLiter,
                _holdEnergy,
                _fullEnergyWiggleSpeed,
                _riseTime,
                _fallTime,
                _wiggleSmoothingTime);
        }
    }
}
