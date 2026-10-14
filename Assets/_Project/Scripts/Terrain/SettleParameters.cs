using System;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>How loose ash settles after a dig, as plain data so <see cref="AshSettler"/> can be tested.</summary>
    public readonly struct SettleParameters
    {
        /// <param name="maxFlowPerStep">Most ash (in samples' worth) a column may shed per step; 0 = no limit.</param>
        /// <param name="minSlideForDust">Ash (in samples' worth) a column must lose between commits to count as a dust spot.</param>
        public SettleParameters(float reposeAngleDegrees, float reach, float flowRate, float maxFlowPerStep = 0f,
            float minSlideForDust = 0.15f)
        {
            if (reposeAngleDegrees <= 0f || reposeAngleDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(nameof(reposeAngleDegrees), "The slope must be between 0 and 90 degrees.");
            }

            ReposeAngleDegrees = reposeAngleDegrees;
            Reach = Mathf.Max(0f, reach);
            FlowRate = Mathf.Clamp(flowRate, 0.05f, 1f);
            MaxFlowPerStep = maxFlowPerStep > 0f ? maxFlowPerStep : float.MaxValue;
            MinSlideForDust = Mathf.Max(0f, minSlideForDust);
        }

        /// <summary>The steepest slope loose ash rests at (about 35° for dry ash).</summary>
        public float ReposeAngleDegrees { get; }

        /// <summary>How far beyond the dig, in meters, ash may move; keeps one dig from starting a long avalanche.</summary>
        public float Reach { get; }

        /// <summary>Share of the excess a column sheds per step (0..1): lower is smoother but takes more steps.</summary>
        public float FlowRate { get; }

        /// <summary>
        /// Most ash a column sheds in one step. Without a cap, a steep wall sheds most of its excess in the first
        /// steps and the slide looks like a snap; capped, ash runs at a steady pace like a real trickle.
        /// </summary>
        public float MaxFlowPerStep { get; }

        /// <summary>How much ash a column must lose between commits to puff dust there.</summary>
        public float MinSlideForDust { get; }

        /// <summary>Highest stable height difference between neighbors one cell apart, in cells.</summary>
        public float MaxRisePerCell => Mathf.Tan(ReposeAngleDegrees * Mathf.Deg2Rad);
    }
}
