using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// How the phoenix's silhouette looks at a moment: size, flames, color, height and how it moves. Stages blend from one
    /// look to the next, so the shape grows clearer instead of jumping.
    /// </summary>
    public readonly struct PhoenixLook
    {
        public PhoenixLook(float scale, float particlesPerSecond, float brightness, Color color, float flameSize, float lift, float breathe,
            float wingBeat)
        {
            Scale = scale;
            ParticlesPerSecond = particlesPerSecond;
            Brightness = brightness;
            Color = color;
            FlameSize = flameSize;
            Lift = lift;
            Breathe = breathe;
            WingBeat = wingBeat;
        }

        /// <summary>No phoenix at all (before the first stage).</summary>
        public static PhoenixLook None => new PhoenixLook(0f, 0f, 0f, Color.black, 0f, 0f, 0f, 0f);

        public float Scale { get; }
        public float ParticlesPerSecond { get; }
        public float Brightness { get; }
        public Color Color { get; }
        public float FlameSize { get; }
        public float Lift { get; }
        public float Breathe { get; }
        public float WingBeat { get; }

        public bool IsVisible => Scale > 0f && ParticlesPerSecond > 0f && Brightness > 0f;

        public static PhoenixLook Lerp(PhoenixLook from, PhoenixLook to, float t)
        {
            t = Mathf.Clamp01(t);
            // Growing from nothing, it takes the new stage's color at once rather than fading in from black.
            Color fromColor = from.IsVisible ? from.Color : to.Color;
            return new PhoenixLook(
                Mathf.Lerp(from.Scale, to.Scale, t),
                Mathf.Lerp(from.ParticlesPerSecond, to.ParticlesPerSecond, t),
                Mathf.Lerp(from.Brightness, to.Brightness, t),
                Color.Lerp(fromColor, to.Color, t),
                Mathf.Lerp(from.IsVisible ? from.FlameSize : to.FlameSize, to.FlameSize, t),
                Mathf.Lerp(from.Lift, to.Lift, t),
                Mathf.Lerp(from.Breathe, to.Breathe, t),
                Mathf.Lerp(from.WingBeat, to.WingBeat, t));
        }

        /// <summary>How many stages a Heat level has reached: the last stage whose level it is at or past (0 for none).</summary>
        public static int StageFor(IReadOnlyList<PhoenixStage> stages, int heatLevel)
        {
            int reached = 0;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i].HeatLevel <= heatLevel)
                {
                    reached = i + 1;
                }
            }

            return reached;
        }

        /// <summary>The look of a stage counted from 1; <see cref="None"/> for 0.</summary>
        public static PhoenixLook Of(IReadOnlyList<PhoenixStage> stages, int stage)
        {
            return stage <= 0 || stages.Count == 0 ? None : stages[Mathf.Min(stage, stages.Count) - 1].Look;
        }
    }
}
