using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Renders and collides one chunk of the terrain. Created at runtime by <see cref="ChunkMeshUpdater"/>;
    /// vertices are in the terrain's local space, so the view sits at the terrain's origin.
    /// The collider has its own copy of the shape, taken now and then (<see cref="CopyShapeToCollider"/>): the drawn
    /// mesh can change every frame while the collider's copy is baked in the background, without either waiting.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class VoxelChunkView : MonoBehaviour
    {
        private Mesh _mesh;
        private Mesh _colliderMesh;
        private MeshCollider _collider;

        /// <summary>The collider's copy of the shape, for baking its collision data.</summary>
        public EntityId ColliderMeshId => _colliderMesh.GetEntityId();

        public bool HasTriangles { get; private set; }

        public bool ColliderHasTriangles { get; private set; }

        public void Initialize(Material material)
        {
            // 32-bit indices: a busy 32³ chunk can exceed the 65k-vertex limit of 16-bit meshes.
            _mesh = new Mesh { name = $"{name} Mesh", indexFormat = IndexFormat.UInt32 };
            // Hint to the GPU driver that this mesh is rewritten often.
            _mesh.MarkDynamic();
            _colliderMesh = new Mesh { name = $"{name} Collider", indexFormat = IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<MeshRenderer>().sharedMaterial = material;
            _collider = GetComponent<MeshCollider>();
        }

        /// <summary>Uploads a freshly meshed chunk for drawing. The collider catches up later (<see cref="CopyShapeToCollider"/>).</summary>
        public void Apply(NativeArray<float3> vertices, NativeArray<float3> normals, NativeArray<float4> colors, NativeArray<int> indices)
        {
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            // Layer tints; the ash material uses a shader that multiplies by vertex color.
            _mesh.SetColors(colors);
            _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            HasTriangles = indices.Length > 0;
        }

        /// <summary>
        /// Copies the drawn shape into the collider's mesh, ready to bake. Never call it while that mesh is being baked.
        /// </summary>
        /// <param name="vertexBuffer">Reused between calls, so copying allocates nothing.</param>
        /// <param name="indexBuffer">Reused between calls, so copying allocates nothing.</param>
        public void CopyShapeToCollider(List<Vector3> vertexBuffer, List<int> indexBuffer)
        {
            _mesh.GetVertices(vertexBuffer);
            _mesh.GetIndices(indexBuffer, 0);
            _colliderMesh.Clear();
            _colliderMesh.SetVertices(vertexBuffer);
            _colliderMesh.SetIndices(indexBuffer, MeshTopology.Triangles, 0);
            ColliderHasTriangles = indexBuffer.Count > 0;
        }

        /// <summary>Swaps in the collider's mesh once its collision data is baked.</summary>
        public void RefreshCollider()
        {
            // The collider caches the mesh it was given; reassigning makes it pick up the new (pre-baked) data.
            _collider.sharedMesh = null;
            if (ColliderHasTriangles)
            {
                _collider.sharedMesh = _colliderMesh;
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_colliderMesh != null)
            {
                Destroy(_colliderMesh);
            }
        }
    }
}
