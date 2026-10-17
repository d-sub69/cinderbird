using UnityEngine;

namespace Cinderbird.Valley
{
    /// <summary>The valley's landform, as plain numbers the builder works from (taken from <see cref="ValleySettings"/>).</summary>
    public readonly struct ValleyShape
    {
        public ValleyShape(Vector3 center, Vector2 size, int cells, float hillHeight, float rimRise, float noiseScale, int seed,
            Vector3[] river, float riverWidth, float riverDepth, Vector3 village, float villageRadius)
        {
            Center = center;
            Size = size;
            Cells = Mathf.Max(2, cells);
            HillHeight = hillHeight;
            RimRise = rimRise;
            NoiseScale = noiseScale;
            Seed = seed;
            River = river;
            RiverWidth = riverWidth;
            RiverDepth = riverDepth;
            Village = village;
            VillageRadius = villageRadius;
        }

        /// <summary>Middle of the valley floor (world); its y is the floor's height.</summary>
        public Vector3 Center { get; }
        public Vector2 Size { get; }
        public int Cells { get; }
        public float HillHeight { get; }
        /// <summary>How much the ground rises toward the far edges, so the valley reads as a bowl with a horizon.</summary>
        public float RimRise { get; }
        public float NoiseScale { get; }
        public int Seed { get; }
        /// <summary>The river's course (world x/z), from start to end.</summary>
        public Vector3[] River { get; }
        public float RiverWidth { get; }
        public float RiverDepth { get; }
        public Vector3 Village { get; }
        /// <summary>The village sits on flattened ground this far around its middle.</summary>
        public float VillageRadius { get; }
    }
}
