using System.Collections.Generic;
using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Makes the ember clusters glow through the ash. A few times a second, when the mountain changed: each open-layer
    /// cluster's depth under the surface is measured again where the ash changed near it, and every chunk with clusters
    /// near gets its brightest few handed to an additive glow overlay (a second renderer on the chunk's mesh). A cluster
    /// dims and shrinks as it's emptied, moves when its embers slide, and a sealed layer's clusters stay dark.
    /// </summary>
    public sealed class EmberClusterGlow : MonoBehaviour
    {
        // Must match MAX_CLUSTERS in the ClusterGlow shader.
        private const int MaxPerChunk = 8;

        private static readonly int CenterId = Shader.PropertyToID("_ClusterCenter");
        private static readonly int GlowId = Shader.PropertyToID("_ClusterGlow");
        private static readonly int ColorId = Shader.PropertyToID("_ClusterColor");
        private static readonly int CountId = Shader.PropertyToID("_ClusterCount");

        [SerializeField] private VoxelTerrain _terrain;
        [SerializeField] private EmberTierCatalog _tiers;
        [SerializeField] private ClusterGlowSettings _settings;
        [Tooltip("The ClusterGlow shader's material.")]
        [SerializeField] private Material _glowMaterial;

        private MaterialPropertyBlock _block;
        private Vector4[] _centers;
        private Vector4[] _glows;
        private Vector4[] _colors;
        private List<int> _chunkScratch;
        private float[] _depth;
        private float[] _intensity;
        private float[] _patch;
        private int[] _seenChunkVersions;
        private bool[] _chunkChanged;
        private List<ushort>[] _candidates;
        private int[] _shownHash;
        private MeshRenderer[] _overlays;
        private int _seenClusterVersion = -1;
        private float _nextRefresh;
        // Cached: a lambda here would allocate a delegate on every refresh.
        private System.Comparison<ushort> _brighterFirst;

        private void Awake()
        {
            if (_terrain == null || _tiers == null || _settings == null || _glowMaterial == null)
            {
                Debug.LogError($"{nameof(EmberClusterGlow)} on '{name}' has unassigned references.", this);
                enabled = false;
            }

            _brighterFirst = (a, b) => _intensity[b].CompareTo(_intensity[a]);
        }

        private void Update()
        {
            EmberClusters clusters = _terrain.Clusters;
            if (clusters == null || Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + _settings.RefreshSeconds;
            EnsureBuffers(clusters);
            if (clusters.Version == _seenClusterVersion)
            {
                return;
            }

            _seenClusterVersion = clusters.Version;
            FindChangedChunks();
            MeasureDepths(clusters);
            AssignToChunks(clusters);
        }

        private void EnsureBuffers(EmberClusters clusters)
        {
            // Created here rather than by field initializers: a script reload during play can leave such fields empty.
            _block ??= new MaterialPropertyBlock();
            _centers ??= new Vector4[MaxPerChunk];
            _glows ??= new Vector4[MaxPerChunk];
            _colors ??= new Vector4[MaxPerChunk];
            _chunkScratch ??= new List<int>();
            _brighterFirst ??= (a, b) => _intensity[b].CompareTo(_intensity[a]);

            int chunks = _terrain.Terrain.World.ChunkCount;
            if (_depth != null && _depth.Length == clusters.Capacity && _overlays != null && _overlays.Length == chunks)
            {
                return;
            }

            _depth = new float[clusters.Capacity];
            _intensity = new float[clusters.Capacity];
            _patch = new float[clusters.Capacity];
            for (int i = 0; i < _depth.Length; i++)
            {
                _depth[i] = float.MaxValue;
            }

            _seenChunkVersions = new int[chunks];
            _chunkChanged = new bool[chunks];
            _candidates = new List<ushort>[chunks];
            _shownHash = new int[chunks];
            _overlays = new MeshRenderer[chunks];
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                _seenChunkVersions[chunk] = -1;
                _candidates[chunk] = new List<ushort>(MaxPerChunk * 2);
            }

            _seenClusterVersion = -1;
        }

        private void FindChangedChunks()
        {
            AshTerrain terrain = _terrain.Terrain;
            for (int chunk = 0; chunk < _seenChunkVersions.Length; chunk++)
            {
                int version = terrain.ChangeVersionOf(chunk);
                _chunkChanged[chunk] = version != _seenChunkVersions[chunk];
                _seenChunkVersions[chunk] = version;
            }
        }

        // Only where the ash or embers changed near a cluster can its depth have changed.
        private void MeasureDepths(EmberClusters clusters)
        {
            AshTerrain terrain = _terrain.Terrain;
            float reach = _settings.Reach;
            for (int id = 1; id < clusters.Capacity; id++)
            {
                ClusterState state = clusters[id];
                if (state.IsEmpty || !terrain.Locks.IsUnlocked(state.Layer))
                {
                    _intensity[id] = 0f;
                    continue;
                }

                if (_depth[id] == float.MaxValue || ChangedNear(terrain.World, state.Center, reach))
                {
                    _depth[id] = ClusterDepth.Probe(terrain.World, state.Center, reach, out _);
                }

                _intensity[id] = ClusterGlowMath.Intensity(state.Weight, _depth[id], reach, _settings.IntensityPerWorth,
                    _settings.WorthExponent, _settings.MaxIntensity);
                _patch[id] = ClusterGlowMath.PatchRadius(state.Radius, _depth[id], reach, _settings.DeepPatchScale,
                    _settings.MinPatchRadius);
            }
        }

        private bool ChangedNear(VoxelWorld world, Vector3 center, float reach)
        {
            Vector3 extent = Vector3.one * reach;
            world.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            foreach (int chunk in _chunkScratch)
            {
                if (_chunkChanged[chunk])
                {
                    return true;
                }
            }

            return false;
        }

        private void AssignToChunks(EmberClusters clusters)
        {
            VoxelWorld world = _terrain.Terrain.World;
            foreach (List<ushort> list in _candidates)
            {
                list.Clear();
            }

            for (int id = 1; id < clusters.Capacity; id++)
            {
                if (_intensity[id] <= 0f)
                {
                    continue;
                }

                ClusterState state = clusters[id];
                Vector3 extent = Vector3.one * (_depth[id] + _patch[id]);
                world.ChunksOverlapping(state.Center - extent, state.Center + extent, _chunkScratch);
                foreach (int chunk in _chunkScratch)
                {
                    _candidates[chunk].Add((ushort)id);
                }
            }

            for (int chunk = 0; chunk < _candidates.Length; chunk++)
            {
                Show(chunk, clusters);
            }
        }

        private void Show(int chunk, EmberClusters clusters)
        {
            List<ushort> list = _candidates[chunk];
            // The brightest first: a chunk shows at most MaxPerChunk.
            list.Sort(_brighterFirst);
            int count = Mathf.Min(MaxPerChunk, list.Count);
            int hash = count;
            for (int i = 0; i < count; i++)
            {
                ushort id = list[i];
                hash = hash * 31 + id;
                hash = hash * 31 + Mathf.RoundToInt(_intensity[id] * 50f);
                hash = hash * 31 + Mathf.RoundToInt(_patch[id] * 50f);
                hash = hash * 31 + Mathf.RoundToInt(_depth[id] * 50f);
                hash = hash * 31 + clusters[id].Center.GetHashCode();
            }

            if (hash == _shownHash[chunk] && (count > 0) == (_overlays[chunk] != null && _overlays[chunk].enabled))
            {
                return;
            }

            _shownHash[chunk] = hash;
            MeshRenderer overlay = count > 0 ? OverlayOf(chunk) : _overlays[chunk];
            if (overlay == null)
            {
                return;
            }

            overlay.enabled = count > 0;
            if (count == 0)
            {
                return;
            }

            Transform pile = _terrain.transform;
            for (int i = 0; i < MaxPerChunk; i++)
            {
                if (i >= count)
                {
                    _centers[i] = Vector4.zero;
                    _glows[i] = Vector4.zero;
                    _colors[i] = Vector4.zero;
                    continue;
                }

                ushort id = list[i];
                ClusterState state = clusters[id];
                Vector3 center = pile.TransformPoint(state.Center);
                _centers[i] = new Vector4(center.x, center.y, center.z, id * 0.618f);
                _glows[i] = new Vector4(_patch[id], _intensity[id], _depth[id], _settings.Pulse);
                _colors[i] = TierColor(state.BestTier);
            }

            overlay.GetPropertyBlock(_block);
            _block.SetVectorArray(CenterId, _centers);
            _block.SetVectorArray(GlowId, _glows);
            _block.SetVectorArray(ColorId, _colors);
            _block.SetFloat(CountId, count);
            overlay.SetPropertyBlock(_block);
        }

        private Vector4 TierColor(int tier)
        {
            Color color = tier >= 0 && tier < _tiers.Count && _tiers[tier] != null ? _tiers[tier].Color : new Color(1f, 0.5f, 0.2f);
            Color linear = color.linear;
            return new Vector4(linear.r, linear.g, linear.b, 1f);
        }

        // A second renderer on the chunk's own mesh, so the glow follows every re-mesh for free.
        private MeshRenderer OverlayOf(int chunk)
        {
            if (_overlays[chunk] != null)
            {
                return _overlays[chunk];
            }

            VoxelChunkView view = _terrain.ChunkView(chunk);
            if (view == null)
            {
                return null;
            }

            GameObject overlay = new GameObject("Cluster Glow");
            overlay.layer = view.gameObject.layer;
            overlay.transform.SetParent(view.transform, false);
            overlay.AddComponent<MeshFilter>().sharedMesh = view.GetComponent<MeshFilter>().sharedMesh;
            MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _glowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _overlays[chunk] = renderer;
            return renderer;
        }
    }
}
