using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Builds one mesh of small markers (octahedra, 6 vertices each), one per ember, colored by tier, for drawing
    /// thousands of embers in a single draw call. Plain C#; the lists are reused between builds.
    /// </summary>
    public sealed class EmberMarkerBuilder
    {
        private static readonly Vector3[] Corners =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        // Eight faces, each from one of +-x, +-y, +-z.
        private static readonly int[] Faces =
        {
            0, 2, 4, 2, 1, 4, 1, 3, 4, 3, 0, 4,
            2, 0, 5, 1, 2, 5, 3, 1, 5, 0, 3, 5,
        };

        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public List<Color> Colors { get; } = new List<Color>();
        public List<int> Indices { get; } = new List<int>();

        public void Clear()
        {
            Vertices.Clear();
            Colors.Clear();
            Indices.Clear();
        }

        /// <param name="size">Distance from the marker's center to each tip.</param>
        public void Add(Vector3 center, float size, Color color)
        {
            int first = Vertices.Count;
            for (int i = 0; i < Corners.Length; i++)
            {
                Vertices.Add(center + Corners[i] * size);
                Colors.Add(color);
            }

            for (int i = 0; i < Faces.Length; i++)
            {
                Indices.Add(first + Faces[i]);
            }
        }
    }
}
