using UnityEngine;
using UnityEngine.Rendering;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Renders and collides one chunk of the terrain. Created at runtime by <see cref="VoxelTerrain"/>;
    /// vertices are in the terrain's local space, so the view sits at the terrain's origin.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class VoxelChunkView : MonoBehaviour
    {
        private Mesh _mesh;
        private MeshCollider _collider;

        public void Initialize(Material material)
        {
            // 32-bit indices: a busy 32³ chunk can exceed the 65k-vertex limit of 16-bit meshes.
            _mesh = new Mesh { name = $"{name} Mesh", indexFormat = IndexFormat.UInt32 };
            // Hint to the GPU driver that this mesh is rewritten often.
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<MeshRenderer>().sharedMaterial = material;
            _collider = GetComponent<MeshCollider>();
        }

        public void Apply(MeshBuffers buffers)
        {
            _mesh.Clear();
            _mesh.SetVertices(buffers.Vertices);
            _mesh.SetNormals(buffers.Normals);
            _mesh.SetTriangles(buffers.Indices, 0);

            // The collider caches the mesh it was given; reassigning forces it to rebuild from the new data.
            _collider.sharedMesh = null;
            if (buffers.Indices.Count > 0)
            {
                _collider.sharedMesh = _mesh;
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}
