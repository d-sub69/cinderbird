using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// One chunk's glimmers being worked out again around a change, a slice per frame: the embers within reach of the
    /// change are probed from <see cref="Cursor"/> on, and what they show is collected in <see cref="Found"/> until the
    /// chunk is done and its glimmers are swapped in at once (so none blink out meanwhile).
    /// </summary>
    public sealed class GlimmerRefreshJob
    {
        public int Chunk { get; private set; }
        public Vector3 Center { get; private set; }
        public float Reach { get; private set; }
        public float ReachSquared => Reach * Reach;
        public int Cursor { get; set; }
        public List<GlimmerPoint> Found { get; } = new List<GlimmerPoint>();

        public void Start(int chunk, Vector3 center, float reach)
        {
            Chunk = chunk;
            Center = center;
            Reach = reach;
            Cursor = 0;
            Found.Clear();
        }

        /// <summary>Another change near the same chunk: one sphere covering both, and the chunk starts over.</summary>
        public void Widen(Vector3 center, float reach)
        {
            float between = Vector3.Distance(Center, center);
            if (between + reach <= Reach)
            {
                // Already inside.
            }
            else if (between + Reach <= reach)
            {
                Center = center;
                Reach = reach;
            }
            else
            {
                float combined = (between + Reach + reach) * 0.5f;
                Center += (center - Center) * ((combined - Reach) / between);
                Reach = combined;
            }

            Cursor = 0;
            Found.Clear();
        }
    }
}
