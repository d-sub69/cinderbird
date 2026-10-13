using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Reusable output of <see cref="MarchingCubes"/>. Lists are cleared, not reallocated, between rebuilds.
    /// </summary>
    public sealed class MeshBuffers
    {
        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public List<Vector3> Normals { get; } = new List<Vector3>();

        /// <summary>Per-vertex tint of the ash layer the vertex was cut from.</summary>
        public List<Color> Colors { get; } = new List<Color>();

        public List<int> Indices { get; } = new List<int>();

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Indices.Clear();
        }
    }
}
