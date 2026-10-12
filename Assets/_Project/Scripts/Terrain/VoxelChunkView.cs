using Unity.Collections;
using Unity.Mathematics;
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

        public EntityId MeshId => _mesh.GetEntityId();

        public bool HasTriangles { get; private set; }

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

        /// <summary>Uploads a freshly meshed chunk. Call <see cref="RefreshCollider"/> once its collision is baked.</summary>
        public void Apply(NativeArray<float3> vertices, NativeArray<float3> normals, NativeArray<int> indices)
        {
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            HasTriangles = indices.Length > 0;
        }

        public void RefreshCollider()
        {
            // The collider caches the mesh it was given; reassigning makes it pick up the new (pre-baked) data.
            _collider.sharedMesh = null;
            if (HasTriangles)
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
