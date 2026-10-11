using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Thin adapter: owns one <see cref="VoxelGrid"/>, turns it into a rendered mesh and collider,
    /// and re-meshes after each dig. Keep the transform's scale at 1 so dig radii stay in meters.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class VoxelChunk : MonoBehaviour
    {
        [SerializeField] private VoxelTerrainSettings _settings;

        private VoxelGrid _grid;
        private readonly MarchingCubes _mesher = new MarchingCubes();
        private readonly MeshBuffers _buffers = new MeshBuffers();
        private Mesh _mesh;
        private MeshCollider _collider;

        public event Action<DigResult> Dug;

        /// <summary>Walks the whole grid; fine for debug display after a dig, not every frame.</summary>
        public float TotalAshVolume => _grid?.TotalAshVolume() ?? 0f;

        private void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError($"{nameof(VoxelChunk)} on '{name}' has no settings assigned.", this);
                enabled = false;
                return;
            }

            _grid = new VoxelGrid(_settings.CellsX, _settings.CellsY, _settings.CellsZ, _settings.CellSize, _settings.GridOrigin);
            AshPileGenerator.Generate(_grid, _settings.PileShape);

            // 32-bit indices: a 32x16x32 chunk can exceed the 65k-vertex limit of 16-bit meshes.
            _mesh = new Mesh { name = $"{name} Voxels", indexFormat = IndexFormat.UInt32 };
            // Hint to the GPU driver that this mesh is rewritten often.
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _collider = GetComponent<MeshCollider>();

            Rebuild();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        /// <summary>Carves a sphere at a world position and returns how much ash came out.</summary>
        public DigResult Dig(Vector3 worldCenter, float radius)
        {
            if (_grid == null)
            {
                return default;
            }

            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            DigResult result = SphereDigBrush.Dig(_grid, localCenter, radius);
            if (!result.RemovedAnyAsh)
            {
                return result;
            }

            Rebuild();
            Dug?.Invoke(result);
            return result;
        }

        private void Rebuild()
        {
            _mesher.Polygonize(_grid, _buffers);

            _mesh.Clear();
            _mesh.SetVertices(_buffers.Vertices);
            _mesh.SetNormals(_buffers.Normals);
            _mesh.SetTriangles(_buffers.Indices, 0);

            // The collider caches the mesh it was given; reassigning forces it to rebuild from the new data.
            _collider.sharedMesh = null;
            if (_buffers.Indices.Count > 0)
            {
                _collider.sharedMesh = _mesh;
            }
        }

        // The mesh only exists in Play mode, so show the chunk's extent in the Scene view.
        private void OnDrawGizmosSelected()
        {
            if (_settings == null)
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(_settings.GridOrigin + _settings.GridSize * 0.5f, _settings.GridSize);
        }
    }
}
