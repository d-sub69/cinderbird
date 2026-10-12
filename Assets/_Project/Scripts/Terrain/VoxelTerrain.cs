using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Thin adapter: builds the <see cref="AshTerrain"/> (mountain + embers) at start, creates one
    /// <see cref="VoxelChunkView"/> per chunk, and re-meshes only the chunks a dig touched.
    /// Keep the transform's scale at 1 so dig radii stay in meters.
    /// </summary>
    public sealed class VoxelTerrain : MonoBehaviour
    {
        [SerializeField] private VoxelTerrainSettings _settings;
        [SerializeField] private EmberSpawnSettings _emberSpawnSettings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Material for the ash surface.")]
        [SerializeField] private Material _material;

        [Header("Debug")]
        [Tooltip("In Play mode, draw buried embers near the camera in the Scene view while this object is selected.")]
        [SerializeField] private bool _showEmberGizmos;

        private AshTerrain _terrain;
        private VoxelChunkView[] _views;
        private readonly MarchingCubes _mesher = new MarchingCubes();
        private readonly MeshBuffers _buffers = new MeshBuffers();
        private readonly List<int> _dirtyChunks = new List<int>();

        /// <summary>Fired after a dig with what it removed and the dug sphere (local center, radius).</summary>
        public event Action<DigResult, Vector3, float> Dug;

        /// <summary>The terrain data; null until Awake has run.</summary>
        public AshTerrain Terrain => _terrain;

        public float TotalAshVolume => _terrain?.TotalAshVolume ?? 0f;

        public int BuriedEmberCount => _terrain?.BuriedEmberCount ?? 0;

        /// <summary>Local y of the ground the mountain sits on; <see cref="VoxelTerrainSettings.WorldOrigin"/> puts it at 0.</summary>
        public float LocalFloorHeight => 0f;

        private void Awake()
        {
            if (_settings == null || _material == null)
            {
                Debug.LogError($"{nameof(VoxelTerrain)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            VoxelWorld world = new VoxelWorld(
                _settings.ChunkCells, _settings.ChunksX, _settings.ChunksY, _settings.ChunksZ,
                _settings.CellSize, _settings.WorldOrigin);
            _terrain = new AshTerrain(world);
            _terrain.Generate(_settings.PileShape);
            SpawnEmbers();
            CreateViews();
            RebuildDirtyChunks();
        }

        /// <summary>
        /// Carves a sphere at a world position. Embers inside it are appended to <paramref name="extractedEmbers"/>.
        /// </summary>
        public DigResult Dig(Vector3 worldCenter, float radius, List<Ember> extractedEmbers)
        {
            if (_terrain == null)
            {
                return default;
            }

            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            DigResult result = _terrain.Dig(localCenter, radius, extractedEmbers);
            if (!result.RemovedAnyAsh && result.EmberCount == 0)
            {
                return result;
            }

            RebuildDirtyChunks();
            Dug?.Invoke(result, localCenter, radius);
            return result;
        }

        private void SpawnEmbers()
        {
            if (_emberSpawnSettings == null || _tierCatalog == null)
            {
                Debug.LogWarning($"{nameof(VoxelTerrain)} on '{name}' has no ember settings; the mountain will be empty.", this);
                return;
            }

            // One-off at startup, so the temporary list is fine.
            List<float> weights = new List<float>();
            _tierCatalog.CopySpawnWeights(weights);
            _terrain.SpawnEmbers(_emberSpawnSettings.ToParameters(), weights);
        }

        private void CreateViews()
        {
            VoxelWorld world = _terrain.World;
            _views = new VoxelChunkView[world.ChunkCount];
            for (int i = 0; i < _views.Length; i++)
            {
                Vector3Int coord = world.ChunkCoord(i);
                GameObject chunkObject = new GameObject($"Chunk {coord.x},{coord.y},{coord.z}",
                    typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(VoxelChunkView));
                chunkObject.layer = gameObject.layer;
                chunkObject.transform.SetParent(transform, false);
                VoxelChunkView view = chunkObject.GetComponent<VoxelChunkView>();
                view.Initialize(_material);
                _views[i] = view;
            }
        }

        private void RebuildDirtyChunks()
        {
            _terrain.World.TakeDirtyChunks(_dirtyChunks);
            for (int i = 0; i < _dirtyChunks.Count; i++)
            {
                int chunk = _dirtyChunks[i];
                _mesher.Polygonize(_terrain.World, chunk, _buffers);
                _views[chunk].Apply(_buffers);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_settings == null)
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(_settings.WorldOrigin + _settings.WorldSize * 0.5f, _settings.WorldSize);

            if (!_showEmberGizmos || _terrain == null || _tierCatalog == null)
            {
                return;
            }

            DrawEmberGizmos();
        }

        // Only chunks near the Scene camera: drawing tens of thousands of gizmos would freeze the editor.
        private void DrawEmberGizmos()
        {
            Camera sceneCamera = Camera.current;
            if (sceneCamera == null)
            {
                return;
            }

            const float range = 12f;
            Vector3 eye = transform.InverseTransformPoint(sceneCamera.transform.position);
            List<int> chunks = new List<int>();
            _terrain.World.ChunksOverlapping(eye - Vector3.one * range, eye + Vector3.one * range, chunks);
            foreach (int chunk in chunks)
            {
                EmberField embers = _terrain.EmbersIn(chunk);
                for (int i = 0; i < embers.Count; i++)
                {
                    Ember ember = embers[i];
                    bool validTier = ember.TierIndex < _tierCatalog.Count && _tierCatalog[ember.TierIndex] != null;
                    Gizmos.color = validTier ? _tierCatalog[ember.TierIndex].Color : Color.magenta;
                    Gizmos.DrawSphere(ember.Position, 0.06f);
                }
            }
        }
    }
}
