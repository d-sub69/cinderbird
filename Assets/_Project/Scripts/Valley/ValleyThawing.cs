using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Valley
{
    /// <summary>
    /// How thawed each part of the valley is: every stage reached melts its patch and its stretch of river for good; the
    /// newest one spreads out from its middle as it grows in (<paramref name="growth"/> 0..1). Pure, so it can be tested.
    /// </summary>
    public static class ValleyThawing
    {
        /// <summary>How many stages a Heat level has reached: the last stage whose level it is at or past (0 for none).</summary>
        public static int StageFor(IReadOnlyList<ValleyThawStage> stages, int heatLevel)
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

        /// <summary>How thawed the ground at a world point is (0 snow .. 1 thawed).</summary>
        /// <param name="reached">Stages reached (see <see cref="StageFor"/>); the last of them is the one growing in.</param>
        public static float GroundAt(IReadOnlyList<ValleyThawStage> stages, int reached, float growth, Vector3 point)
        {
            float thawed = 0f;
            for (int i = 0; i < reached && i < stages.Count; i++)
            {
                ValleyThawStage stage = stages[i];
                float radius = stage.Radius * (i == reached - 1 ? Mathf.Clamp01(growth) : 1f);
                if (radius <= 0f)
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(stage.Center.x, stage.Center.z));
                // A soft rim: snow gives way over the outer quarter of the patch.
                thawed = Mathf.Max(thawed, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius * 0.75f, radius, distance)));
            }

            return thawed;
        }

        /// <summary>How thawed the river is at a point along it (0..1 from its start): ice 0 .. running water 1.</summary>
        public static float RiverAt(IReadOnlyList<ValleyThawStage> stages, int reached, float growth, float along)
        {
            const float Soft = 0.015f;
            float thawed = 0f;
            for (int i = 0; i < reached && i < stages.Count; i++)
            {
                Vector2 stretch = stages[i].River;
                float middle = (stretch.x + stretch.y) * 0.5f;
                float half = Mathf.Abs(stretch.y - stretch.x) * 0.5f * (i == reached - 1 ? Mathf.Clamp01(growth) : 1f);
                if (half <= 0f)
                {
                    continue;
                }

                float outside = Mathf.Abs(along - middle) - half;
                thawed = Mathf.Max(thawed, 1f - Mathf.Clamp01(outside / Soft));
            }

            return thawed;
        }

        /// <summary>How lit a window is (0..1): lit for good by an earlier stage, or lighting up with the newest.</summary>
        public static float WindowAt(IReadOnlyList<ValleyThawStage> stages, int reached, float growth, int window)
        {
            float lit = 0f;
            for (int i = 0; i < reached && i < stages.Count; i++)
            {
                if (System.Array.IndexOf(stages[i].Windows, window) >= 0)
                {
                    lit = Mathf.Max(lit, i == reached - 1 ? Mathf.Clamp01(growth) : 1f);
                }
            }

            return lit;
        }
    }
}
