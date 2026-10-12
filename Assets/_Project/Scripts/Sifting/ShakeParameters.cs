namespace Cinderbird.Sifting
{
    /// <summary>
    /// Plain-data shake tuning in SI units, so <see cref="SieveShaker"/> can be tested without a ScriptableObject.
    /// </summary>
    public readonly struct ShakeParameters
    {
        public ShakeParameters(
            float maxRemovalRate,
            float holdEnergy,
            float fullEnergyWiggleSpeed,
            float riseTime,
            float fallTime,
            float wiggleSmoothingTime)
        {
            MaxRemovalRate = maxRemovalRate;
            HoldEnergy = holdEnergy;
            FullEnergyWiggleSpeed = fullEnergyWiggleSpeed;
            RiseTime = riseTime;
            FallTime = fallTime;
            WiggleSmoothingTime = wiggleSmoothingTime;
        }

        /// <summary>Cubic meters of ash per second at full energy.</summary>
        public float MaxRemovalRate { get; }

        /// <summary>Energy (0..1) from holding the button without moving the mouse.</summary>
        public float HoldEnergy { get; }

        /// <summary>Mouse speed in pixels per second that counts as full energy.</summary>
        public float FullEnergyWiggleSpeed { get; }

        /// <summary>Seconds to go from 0 to full energy.</summary>
        public float RiseTime { get; }

        /// <summary>Seconds to go from full energy to 0.</summary>
        public float FallTime { get; }

        /// <summary>Time constant for smoothing raw mouse speed, which is jumpy frame to frame.</summary>
        public float WiggleSmoothingTime { get; }
    }
}
