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
        public List<int> Indices { get; } = new List<int>();

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Indices.Clear();
        }
    }
}
