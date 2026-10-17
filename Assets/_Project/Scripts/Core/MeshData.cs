using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// A mesh as plain arrays, built by pure code (procedural shapes) and turned into a Unity mesh only by whoever shows
    /// it, so the building can be tested without Unity.
    /// </summary>
    public sealed class MeshData
    {
        public MeshData(Vector3[] vertices, int[] triangles, Color[] colors = null, Vector2[] uv = null)
        {
            Vertices = vertices;
            Triangles = triangles;
            Colors = colors;
            Uv = uv;
        }

        public Vector3[] Vertices { get; }

        public int[] Triangles { get; }

        /// <summary>Optional: one color per vertex.</summary>
        public Color[] Colors { get; }

        /// <summary>Optional: one texture coordinate per vertex (used as data, e.g. how far along a river).</summary>
        public Vector2[] Uv { get; }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (Vertices.Length > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = Vertices;
            mesh.triangles = Triangles;
            if (Colors != null)
            {
                mesh.colors = Colors;
            }

            if (Uv != null)
            {
                mesh.uv = Uv;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
