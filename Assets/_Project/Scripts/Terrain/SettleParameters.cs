using System;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>How loose ash settles after a dig, as plain data so <see cref="AshSettler"/> can be tested.</summary>
    public readonly struct SettleParameters
    {
        public SettleParameters(float reposeAngleDegrees, float reach, float flowRate)
        {
            if (reposeAngleDegrees <= 0f || reposeAngleDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(nameof(reposeAngleDegrees), "The slope must be between 0 and 90 degrees.");
            }

            ReposeAngleDegrees = reposeAngleDegrees;
            Reach = Mathf.Max(0f, reach);
            FlowRate = Mathf.Clamp(flowRate, 0.05f, 1f);
        }

        /// <summary>The steepest slope loose ash rests at (about 35° for dry ash).</summary>
        public float ReposeAngleDegrees { get; }

        /// <summary>How far beyond the dig, in meters, ash may move; keeps one dig from starting a long avalanche.</summary>
        public float Reach { get; }

        /// <summary>Share of the excess a column sheds per step (0..1): lower is smoother but takes more steps.</summary>
        public float FlowRate { get; }

        /// <summary>Highest stable height difference between neighbors one cell apart, in cells.</summary>
        public float MaxRisePerCell => Mathf.Tan(ReposeAngleDegrees * Mathf.Deg2Rad);
    }
}
