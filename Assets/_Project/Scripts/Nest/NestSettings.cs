using UnityEngine;

namespace Cinderbird.Nest
{
    [CreateAssetMenu(fileName = "NestSettings", menuName = "Cinderbird/Nest Settings")]
    public sealed class NestSettings : ScriptableObject
    {
        [Header("Interaction")]
        [Tooltip("How close (meters, ignoring height) the player must be to feed the nest.")]
        [SerializeField, Min(0.1f)] private float _pourRadius = 2.5f;

        [Header("Flare")]
        [Tooltip("Flare added per point of Heat in a landing ember.")]
        [SerializeField, Min(0f)] private float _flarePerHeat = 0.15f;
        [Tooltip("Seconds for a flare to fade to half.")]
        [SerializeField, Min(0.01f)] private float _flareHalfLife = 0.4f;
        [SerializeField, Min(0f)] private float _maxFlare = 1.5f;

        [Header("Fire particles")]
        [Tooltip("Flames per second at 0% and 100% progress.")]
        [SerializeField, Min(0f)] private float _minEmissionRate = 8f;
        [SerializeField, Min(0f)] private float _maxEmissionRate = 60f;
        [Tooltip("Extra flames per second per 1.0 of flare.")]
        [SerializeField, Min(0f)] private float _flareEmissionBoost = 80f;
        [Tooltip("Flame size multiplier at 0% and 100% progress.")]
        [SerializeField, Min(0f)] private float _minSizeMultiplier = 0.6f;
        [SerializeField, Min(0f)] private float _maxSizeMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float _flareSizeBoost = 0.5f;

        [Header("Fire light")]
        [SerializeField, Min(0f)] private float _minLightIntensity = 1f;
        [SerializeField, Min(0f)] private float _maxLightIntensity = 5f;
        [SerializeField, Min(0f)] private float _flareLightBoost = 4f;

        [Header("Ember flights")]
        [SerializeField, Min(0.05f)] private float _flightDuration = 0.6f;
        [Tooltip("Seconds between consecutive embers leaving the basket.")]
        [SerializeField, Min(0f)] private float _flightStagger = 0.06f;
        [Tooltip("Peak height of the arc above the straight line, in meters.")]
        [SerializeField, Min(0f)] private float _flightArcHeight = 0.8f;
        [Tooltip("Flying ember objects created up front; extras land instantly.")]
        [SerializeField, Min(1)] private int _maxFlights = 30;
        [Tooltip("Flying embers are drawn this much bigger than in the basket so they read at a distance.")]
        [SerializeField, Min(0.1f)] private float _flightScale = 1.5f;

        public float PourRadius => _pourRadius;
        public float FlarePerHeat => _flarePerHeat;
        public float FlareHalfLife => _flareHalfLife;
        public float MaxFlare => _maxFlare;
        public float MinEmissionRate => _minEmissionRate;
        public float MaxEmissionRate => _maxEmissionRate;
        public float FlareEmissionBoost => _flareEmissionBoost;
        public float MinSizeMultiplier => _minSizeMultiplier;
        public float MaxSizeMultiplier => _maxSizeMultiplier;
        public float FlareSizeBoost => _flareSizeBoost;
        public float MinLightIntensity => _minLightIntensity;
        public float MaxLightIntensity => _maxLightIntensity;
        public float FlareLightBoost => _flareLightBoost;
        public float FlightDuration => _flightDuration;
        public float FlightStagger => _flightStagger;
        public float FlightArcHeight => _flightArcHeight;
        public int MaxFlights => _maxFlights;
        public float FlightScale => _flightScale;
    }
}
