using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Thin adapter: owns one <see cref="VoxelGrid"/> and the embers buried in it, turns the grid into a
    /// rendered mesh and collider, and re-meshes after each dig. Keep the transform's scale at 1 so dig
    /// radii stay in meters.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class VoxelChunk : MonoBehaviour
    {
        [SerializeField] private VoxelTerrainSettings _settings;
        [SerializeField] private EmberSpawnSettings _emberSpawnSettings;
        [SerializeField] private EmberTierCatalog _tierCatalog;

        [Header("Debug")]
        [Tooltip("In Play mode, draw buried embers in the Scene view while this object is selected.")]
        [SerializeField] private bool _showEmberGizmos;

        private VoxelGrid _grid;
        private readonly EmberField _embers = new EmberField();
        private readonly MarchingCubes _mesher = new MarchingCubes();
        private readonly MeshBuffers _buffers = new MeshBuffers();
        private Mesh _mesh;
        private MeshCollider _collider;

        public event Action<DigResult> Dug;

        /// <summary>Walks the whole grid; fine for debug display after a dig, not every frame.</summary>
        public float TotalAshVolume => _grid?.TotalAshVolume() ?? 0f;

        public int BuriedEmberCount => _embers.Count;

        /// <summary>Read access for views such as the glimmer; null until Awake has run. Only change it through <see cref="Dig"/>.</summary>
        public VoxelGrid Grid => _grid;

        /// <summary>Read access for views such as the glimmer. Only change it through <see cref="Dig"/>.</summary>
        public EmberField Embers => _embers;

        /// <summary>Local y of the ground the pile sits on; <see cref="VoxelTerrainSettings.GridOrigin"/> puts it at 0.</summary>
        public float LocalFloorHeight => 0f;

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
            SpawnEmbers();

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

        /// <summary>
        /// Carves a sphere at a world position. Embers inside it are appended to <paramref name="extractedEmbers"/>.
        /// </summary>
        public DigResult Dig(Vector3 worldCenter, float radius, List<Ember> extractedEmbers)
        {
            if (_grid == null)
            {
                return default;
            }

            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            DigResult carve = SphereDigBrush.Dig(_grid, localCenter, radius);
            int emberCount = _embers.ExtractWithin(localCenter, radius, extractedEmbers);
            DigResult result = new DigResult(carve.RemovedVolume, emberCount);

            if (!result.RemovedAnyAsh && emberCount == 0)
            {
                return result;
            }

            if (result.RemovedAnyAsh)
            {
                Rebuild();
            }

            Dug?.Invoke(result);
            return result;
        }

        private void SpawnEmbers()
        {
            if (_emberSpawnSettings == null || _tierCatalog == null)
            {
                Debug.LogWarning($"{nameof(VoxelChunk)} on '{name}' has no ember settings; the pile will be empty.", this);
                return;
            }

            // One-off at startup, so the temporary lists are fine.
            List<float> weights = new List<float>();
            _tierCatalog.CopySpawnWeights(weights);
            List<Ember> spawned = new List<Ember>();
            EmberSpawner.Spawn(_grid, _emberSpawnSettings.ToParameters(), weights, spawned);
            _embers.AddRange(spawned);
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

            if (!_showEmberGizmos || _tierCatalog == null)
            {
                return;
            }

            for (int i = 0; i < _embers.Count; i++)
            {
                Ember ember = _embers[i];
                bool validTier = ember.TierIndex < _tierCatalog.Count && _tierCatalog[ember.TierIndex] != null;
                Gizmos.color = validTier ? _tierCatalog[ember.TierIndex].Color : Color.magenta;
                Gizmos.DrawSphere(ember.Position, 0.06f);
            }
        }
    }
}
