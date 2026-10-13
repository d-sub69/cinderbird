using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cinderbird.Embers;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        [Tooltip("Optional: ash layers (depth bands, ember tables, tints). Without it the mountain is one layer.")]
        [SerializeField] private LayerCatalog _layerCatalog;
        [Tooltip("Material for the ash surface; it should show vertex colors so the layer tints appear.")]
        [SerializeField] private Material _material;

        [Header("Debug")]
        [Tooltip("In Play mode, draw buried embers near the camera in the Scene view while this object is selected.")]
        [SerializeField] private bool _showEmberGizmos;

        // Show up by name in Unity's Profiler, so slow frames can be traced to digging or meshing.
        private static readonly ProfilerMarker DigMarker = new ProfilerMarker("Cinderbird.Dig");
        private static readonly ProfilerMarker MeshMarker = new ProfilerMarker("Cinderbird.Mesh");

        private AshTerrain _terrain;
        private VoxelChunkView[] _views;
        private MarchingCubes _mesher;
        private readonly List<int> _dirtyChunks = new List<int>();
        private readonly List<int> _bakedChunks = new List<int>();
        private readonly List<int> _batchChunks = new List<int>();

        // Used when there's no layer catalog: plain grey ash, light dust.
        private static readonly Color DefaultAshColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color DefaultDustColor = new Color(0.78f, 0.76f, 0.74f, 0.8f);

        private Color[] _ashColors;
        private Color[] _dustColors;

        // Enough to keep all worker threads busy; a dig touches at most 8 chunks, so only start-up is split.
        private const int MeshBatchSize = 32;

        /// <summary>Fired after a dig with what it removed and the dug sphere (local center, radius).</summary>
        public event Action<DigResult, Vector3, float> Dug;

        /// <summary>Fired when a dig removed nothing because it hit a locked layer (the layer index).</summary>
        public event Action<int> DigBlocked;

        /// <summary>Fired when a layer opens for digging (the layer index).</summary>
        public event Action<int> LayerUnlocked;

        /// <summary>The deepest ash layer that can be dug right now.</summary>
        public int DeepestUnlockedLayer => _terrain?.Locks.DeepestUnlocked ?? 0;

        /// <summary>Milliseconds the last dig took in total, including re-meshing; for the performance overlay.</summary>
        public float LastDigMilliseconds { get; private set; }

        /// <summary>Milliseconds the last re-mesh took (meshing plus collider baking).</summary>
        public float LastMeshMilliseconds { get; private set; }

        public int LastMeshedChunkCount { get; private set; }

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
            int layerCount = _layerCatalog != null ? Math.Max(1, _layerCatalog.Count) : 1;
            _terrain = new AshTerrain(world, layerCount);
            _terrain.Generate(_settings.PileShape, _layerCatalog != null ? _layerCatalog.CreateLayout() : null);
            SpawnEmbers();
            _mesher = new MarchingCubes();
            if (_layerCatalog != null)
            {
                _ashColors = _layerCatalog.CreateTints();
                _dustColors = _layerCatalog.CreateDustColors();
                _mesher.SetLayerTints(_ashColors);
            }

            CreateViews();
            RebuildDirtyChunks();
        }

        private void OnDestroy()
        {
            _mesher?.Dispose();
        }

        /// <summary>
        /// Carves a sphere at a world position. Embers inside it are appended to <paramref name="extractedEmbers"/>.
        /// </summary>
        /// <param name="removedByLayer">If given (length <see cref="LayerCount"/>), cubic meters taken per layer are added to it.</param>
        public DigResult Dig(Vector3 worldCenter, float radius, List<Ember> extractedEmbers, float[] removedByLayer = null)
        {
            if (_terrain == null)
            {
                return default;
            }

            using (DigMarker.Auto())
            {
                long started = Stopwatch.GetTimestamp();
                Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
                DigResult result = _terrain.Dig(localCenter, radius, extractedEmbers, removedByLayer);
                if (!result.RemovedAnyAsh && result.EmberCount == 0)
                {
                    if (result.WasBlocked)
                    {
                        DigBlocked?.Invoke(result.BlockedLayer);
                    }

                    return result;
                }

                RebuildDirtyChunks();
                LastDigMilliseconds = MillisecondsSince(started);
                Dug?.Invoke(result, localCenter, radius);
                return result;
            }
        }

        /// <summary>Opens the next ash layer for digging (debug key for now; Heat levels in the full game).</summary>
        /// <returns>The layer that opened, or -1 if all are open.</returns>
        public int UnlockNextLayer()
        {
            int layer = _terrain != null ? _terrain.Locks.UnlockNext() : -1;
            if (layer >= 0)
            {
                LayerUnlocked?.Invoke(layer);
            }

            return layer;
        }

        public int LayerCount => _terrain?.Locks.Count ?? 1;

        /// <summary>The ash and dust colors of a dug load, blended by how much came from each layer.</summary>
        public void MixLayerColors(float[] removedByLayer, out Color ashColor, out Color dustColor)
        {
            if (_ashColors == null || removedByLayer == null)
            {
                ashColor = DefaultAshColor;
                dustColor = DefaultDustColor;
                return;
            }

            ashColor = LayerColorMix.Blend(removedByLayer, _ashColors, DefaultAshColor);
            dustColor = LayerColorMix.Blend(removedByLayer, _dustColors, DefaultDustColor);
        }

        public string LayerName(int layer)
        {
            return _layerCatalog != null ? _layerCatalog.NameOf(layer) : $"Layer {layer + 1}";
        }

        private void SpawnEmbers()
        {
            if (_emberSpawnSettings == null || _tierCatalog == null)
            {
                Debug.LogWarning($"{nameof(VoxelTerrain)} on '{name}' has no ember settings; the mountain will be empty.", this);
                return;
            }

            if (_layerCatalog != null)
            {
                EmberSpawnParameters parameters = _emberSpawnSettings.ToParameters();
                _terrain.SpawnLayeredEmbers(_layerCatalog.CreateSpawnTable(_tierCatalog.Count), parameters.Seed, parameters.MinDensity);
                // Exactly one Heart Ember, at the center of the innermost pocket.
                if (_layerCatalog.Count > 1)
                {
                    _terrain.PlaceEmber(new Ember(_layerCatalog.PocketCenter, _layerCatalog.HeartTierIndex, (byte)(_layerCatalog.Count - 1)));
                }

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

        // Meshes every changed chunk in parallel on worker threads, uploads the meshes, then bakes their colliders
        // in parallel too. All of it finishes this frame, so the hole appears the moment you dig.
        private void RebuildDirtyChunks()
        {
            _terrain.World.TakeDirtyChunks(_dirtyChunks);
            if (_dirtyChunks.Count == 0)
            {
                return;
            }

            using (MeshMarker.Auto())
            {
                long started = Stopwatch.GetTimestamp();
                // In batches: meshing all chunks at once (start-up) would hold tens of MB of job memory at the same time.
                for (int first = 0; first < _dirtyChunks.Count; first += MeshBatchSize)
                {
                    _batchChunks.Clear();
                    int end = Math.Min(first + MeshBatchSize, _dirtyChunks.Count);
                    for (int i = first; i < end; i++)
                    {
                        _batchChunks.Add(_dirtyChunks[i]);
                    }

                    _mesher.PolygonizeChunks(_terrain.World, _batchChunks);
                    for (int i = 0; i < _batchChunks.Count; i++)
                    {
                        _views[_batchChunks[i]].Apply(_mesher.ResultVertices(i), _mesher.ResultNormals(i), _mesher.ResultColors(i), _mesher.ResultIndices(i));
                    }

                    _mesher.ReleaseResults();
                }

                BakeColliders();
                LastMeshMilliseconds = MillisecondsSince(started);
                LastMeshedChunkCount = _dirtyChunks.Count;
            }
        }

        private void BakeColliders()
        {
            _bakedChunks.Clear();
            foreach (int chunk in _dirtyChunks)
            {
                if (_views[chunk].HasTriangles)
                {
                    _bakedChunks.Add(chunk);
                }
            }

            if (_bakedChunks.Count > 0)
            {
                NativeArray<EntityId> meshIds = new NativeArray<EntityId>(_bakedChunks.Count, Allocator.TempJob);
                for (int i = 0; i < _bakedChunks.Count; i++)
                {
                    meshIds[i] = _views[_bakedChunks[i]].MeshId;
                }

                new BakeMeshJob { MeshIds = meshIds }.Schedule(meshIds.Length, 1).Complete();
                meshIds.Dispose();
            }

            foreach (int chunk in _dirtyChunks)
            {
                _views[chunk].RefreshCollider();
            }
        }

        private static float MillisecondsSince(long startTimestamp)
        {
            return (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
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
