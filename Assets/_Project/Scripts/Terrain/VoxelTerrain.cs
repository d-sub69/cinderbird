using System;
using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Embers;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Thin adapter for the diggable mountain: builds the <see cref="AshTerrain"/> (mountain + embers) when the
    /// bootstrap calls Build, keeps its meshes up to date (<see cref="ChunkMeshUpdater"/>), runs digs and tells
    /// listeners what changed. With <see cref="AshSettling"/> on the same object, a scoop drains out of the pile
    /// while the walls slide in; without it, holes are carved at once.
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

        // Shows up by name in Unity's Profiler, so slow frames can be traced to digging.
        private static readonly ProfilerMarker DigMarker = new ProfilerMarker("Cinderbird.Dig");

        private AshTerrain _terrain;
        private ChunkMeshUpdater _meshes;
        private LayerPalette _palette = new LayerPalette(null, null);
        private AshSettling _settling;
        // Reused per dig: what each dig grazed waits in one of these until its hole is open.
        private readonly Stack<DigExposure> _exposures = new Stack<DigExposure>();
        private readonly List<Ember> _melted = new List<Ember>();
        private readonly List<BuriedFind> _meltedFinds = new List<BuriedFind>();
        // Clusters are counted again at most this often; a dig changes one or two chunks, so each count is small.
        private const float ClusterRefreshSeconds = 0.25f;
        private float _nextClusterRefresh;

        /// <summary>Fired once <see cref="Build"/> has generated and meshed the mountain.</summary>
        public event Action Built;

        /// <summary>
        /// Fired as a dig starts, once its embers are taken (before its hole is open), with what it takes and where.
        /// <see cref="Dug"/> follows when the hole is complete.
        /// </summary>
        public event Action<DigResult, Vector3, float> DigStarted;

        /// <summary>Fired after a dig with what it removed and the dug sphere (local center, radius).</summary>
        public event Action<DigResult, Vector3, float> Dug;

        /// <summary>
        /// Fired when a dig has uncovered buried finds (offerings) and its hole is open, with where they lay (valid during the
        /// call). They are no longer in the ash: whoever shows them takes over.
        /// </summary>
        public event Action<IReadOnlyList<BuriedFind>> FindsUncovered;

        /// <summary>Fired when a dig removed nothing because it hit a locked layer (the layer index).</summary>
        public event Action<int> DigBlocked;

        /// <summary>Fired when a layer opens for digging (the layer index).</summary>
        public event Action<int> LayerUnlocked;

        /// <summary>
        /// Fired as a layer opens and the crust above it melts away, with the layer and the embers that came out of the
        /// melted ash (valid during the call); the nest takes them. <see cref="LayerUnlocked"/> follows.
        /// </summary>
        public event Action<int, IReadOnlyList<Ember>> CrustMelted;

        /// <summary>Fired after saved terrain data was applied and re-meshed; totals may have changed.</summary>
        public event Action Restored;

        /// <summary>Fired after settling ash changed the terrain, with the local-space box that changed.</summary>
        public event Action<Bounds> Settled;

        /// <summary>Fired once a slide has come to rest (or was cut short), with the local box it changed overall.</summary>
        public event Action<Bounds> SettleFinished;

        /// <summary>Fired when embers start lying loose (uncovered by a dig) so views can show them.</summary>
        public event Action LooseEmbersChanged;

        /// <summary>Fired when an ember left the mountain without a dig (a loose one picked up), so clearing can be re-checked.</summary>
        public event Action EmbersCollected;

        /// <summary>
        /// Everything that decides how the fresh mountain is generated (<see cref="TerrainFingerprint"/>). A save can
        /// only be applied to a mountain with the same fingerprint.
        /// </summary>
        public string Fingerprint { get; private set; }

        /// <summary>The terrain data; null until <see cref="Build"/> has run.</summary>
        public AshTerrain Terrain => _terrain;

        /// <summary>The object showing a chunk; null until built.</summary>
        public VoxelChunkView ChunkView(int chunk) => _meshes?.ViewOf(chunk);

        /// <summary>The glowing ember clusters as they stand now; null until built.</summary>
        public EmberClusters Clusters { get; private set; }

        public bool IsBuilt => _terrain != null;

        /// <summary>Radius of the generated mountain at its foot, in meters (its middle is at local 0, 0).</summary>
        public float PileRadius => _settings != null ? _settings.PileShape.Radius : 0f;

        /// <summary>Whether ash is moving right now (a scoop draining or a slide running).</summary>
        public bool IsSettling => _settling != null && _settling.IsSettling;

        /// <summary>The deepest ash layer that can be dug right now.</summary>
        public int DeepestUnlockedLayer => _terrain?.Locks.DeepestUnlocked ?? 0;

        public int LayerCount => _terrain?.Locks.Count ?? 1;

        public float TotalAshVolume => _terrain?.TotalAshVolume ?? 0f;

        public int BuriedEmberCount => _terrain?.BuriedEmberCount ?? 0;

        /// <summary>Local y of the ground the mountain sits on; <see cref="VoxelTerrainSettings.WorldOrigin"/> puts it at 0.</summary>
        public float LocalFloorHeight => 0f;

        /// <summary>Main-thread milliseconds of the last dig (for a scoop: taking its embers and starting the drain).</summary>
        public float LastDigMilliseconds { get; private set; }

        /// <summary>Main-thread milliseconds of the last re-mesh (meshing and upload; colliders bake in the background).</summary>
        public float LastMeshMilliseconds => _meshes?.LastMeshMilliseconds ?? 0f;

        public int LastMeshedChunkCount => _meshes?.LastMeshedChunkCount ?? 0;

        /// <summary>Main-thread milliseconds of uploading the last re-mesh of sliding ash (done a frame after it was started).</summary>
        public float LastUploadMilliseconds => _meshes?.LastUploadMilliseconds ?? 0f;

        /// <summary>Counts those uploads, so the performance overlay can tell a new one happened.</summary>
        public int UploadCount => _meshes?.UploadCount ?? 0;

        /// <summary>Main-thread milliseconds of the last collider round (copying shapes and starting their background bake).</summary>
        public float LastColliderMilliseconds => _meshes?.LastColliderMilliseconds ?? 0f;

        /// <summary>Counts collider rounds, so the performance overlay can tell a new one happened.</summary>
        public int ColliderRoundCount => _meshes?.ColliderRoundCount ?? 0;

        /// <summary>Milliseconds the listeners of the last dig took (glimmers, scoop effects, HUD), on top of the dig itself.</summary>
        public float LastDugListenersMilliseconds { get; private set; }

        private bool Settles => _settling != null && _settling.isActiveAndEnabled;

        private void Awake()
        {
            // Optional: lets ash settle and scoops drain out smoothly; without it holes are carved at once.
            TryGetComponent(out _settling);
            if (_settings == null || _material == null)
            {
                Debug.LogError($"{nameof(VoxelTerrain)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// Generates the mountain and its embers and meshes every chunk. Takes a second or two on the full
        /// mountain, so the game's bootstrap calls it behind the loading screen. Runs once; later calls do nothing.
        /// </summary>
        public void Build()
        {
            if (IsBuilt || !enabled)
            {
                return;
            }

            if (_emberSpawnSettings == null || _tierCatalog == null)
            {
                Debug.LogWarning($"{nameof(VoxelTerrain)} on '{name}' has no ember settings; the mountain will be empty.", this);
            }

            _terrain = MountainBuilder.Build(_settings, _layerCatalog, _emberSpawnSettings, _tierCatalog, out IReadOnlyList<EmberCluster> layout);
            VoxelWorld world = _terrain.World;
            Clusters = new EmberClusters(layout, world.ChunkCount, ClusterWeights());
            Clusters.Refresh(_terrain);
            Fingerprint = TerrainFingerprint.Build(_settings, _layerCatalog);
            if (_layerCatalog != null)
            {
                _palette = new LayerPalette(_layerCatalog.CreateTints(), _layerCatalog.CreateDustColors());
            }

            _meshes = new ChunkMeshUpdater(world, transform, _material, _palette.AshTints);
            _meshes.RebuildDirtyChunks(true);
            Built?.Invoke();
        }

        // A cluster's glow follows what its embers are worth: a few Starfire embers outshine many Sparks.
        private float[] ClusterWeights()
        {
            int count = _tierCatalog != null ? _tierCatalog.Count : 0;
            float[] weights = new float[count];
            for (int i = 0; i < count; i++)
            {
                weights[i] = _tierCatalog[i] != null ? _tierCatalog[i].GlowValue : 0f;
            }

            return weights;
        }

        private void Update()
        {
            _meshes?.Update();
            if (Clusters != null && Time.unscaledTime >= _nextClusterRefresh)
            {
                _nextClusterRefresh = Time.unscaledTime + ClusterRefreshSeconds;
                Clusters.Refresh(_terrain);
            }
        }

        private void OnDestroy()
        {
            _meshes?.Dispose();
        }

        /// <summary>
        /// Digs a sphere at a world position. Embers inside it are appended to <paramref name="extractedEmbers"/>.
        /// The result (ash and embers taken) is final at once.
        /// </summary>
        /// <param name="removedByLayer">If given (length <see cref="LayerCount"/>), cubic meters taken per layer are added to it.</param>
        /// <param name="exposeMargin">Embers this far beyond the dug sphere are uncovered and left lying loose in the hole (meters).</param>
        /// <param name="pourSeconds">
        /// For a scoop: how long its ash takes to pour into the sieve. With settling, the hole drains open over that
        /// time and <see cref="Dug"/> fires when it's complete. 0 (or no settling) opens the hole at once.
        /// </param>
        public DigResult Dig(Vector3 worldCenter, float radius, List<Ember> extractedEmbers, float[] removedByLayer = null,
            float exposeMargin = 0f, float pourSeconds = 0f)
        {
            if (_terrain == null)
            {
                return default;
            }

            // Anything still settling where this dig lands (a scoop draining, a slide) writes out first, so a late write can
            // never overwrite the new hole; slides elsewhere carry on. That also lays a finished scoop's grazed embers.
            Vector3 digCenter = transform.InverseTransformPoint(worldCenter);
            if (_settling != null)
            {
                _settling.PrepareForDig(digCenter, radius);
            }

            using (DigMarker.Auto())
            {
                long started = TimeMeasure.Now();
                Vector3 localCenter = digCenter;
                DigExposure exposure = _exposures.Count > 0 ? _exposures.Pop() : new DigExposure();
                DigResult result = _terrain.StartDig(localCenter, radius, extractedEmbers, removedByLayer, exposure.Embers, exposeMargin, exposure.Finds);
                if (!result.RemovedAnyAsh && result.EmberCount == 0)
                {
                    if (result.WasBlocked)
                    {
                        DigBlocked?.Invoke(result.BlockedLayer);
                    }

                    // Nothing is carved, so anything grazed is in the open already.
                    LayExposed(exposure);
                    return result;
                }

                DigStarted?.Invoke(result, localCenter, radius);
                if (pourSeconds > 0f && Settles)
                {
                    LastDigMilliseconds = TimeMeasure.MillisecondsSince(started);
                    _settling.StartScoop(result, localCenter, radius, pourSeconds, exposure);
                    return result;
                }

                _terrain.CarveDig(localCenter, radius);
                _meshes.RebuildDirtyChunks();
                LastDigMilliseconds = TimeMeasure.MillisecondsSince(started);
                CompleteScoop(result, localCenter, radius, exposure);
                if (Settles && result.RemovedAnyAsh)
                {
                    _settling.SettleAfterDig(localCenter, radius);
                }

                return result;
            }
        }

        /// <summary>
        /// Blows the top of the ash off a round area (the Warm Wind): the ash is gone, embers it covered lie loose on the new
        /// surface, offerings there are uncovered, and the edges settle like after a dig. Fires <see cref="DigStarted"/> and
        /// <see cref="Dug"/> as a dig does, so everything that follows digs follows this too.
        /// </summary>
        /// <param name="worldCenter">Middle of the area, on the surface.</param>
        public DigResult Blow(Vector3 worldCenter, float radius, float depth)
        {
            if (_terrain == null)
            {
                return default;
            }

            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            if (_settling != null)
            {
                _settling.PrepareForDig(localCenter, radius);
            }

            using (DigMarker.Auto())
            {
                DigExposure exposure = _exposures.Count > 0 ? _exposures.Pop() : new DigExposure();
                DigResult result = _terrain.BlowSurface(localCenter, radius, depth, exposure.Embers, exposure.Finds);
                if (!result.RemovedAnyAsh)
                {
                    LayExposed(exposure);
                    return result;
                }

                DigStarted?.Invoke(result, localCenter, radius);
                _meshes.RebuildDirtyChunks();
                CompleteScoop(result, localCenter, radius, exposure);
                if (Settles)
                {
                    _settling.SettleAfterDig(localCenter, radius);
                }

                return result;
            }
        }

        /// <summary>Opens a scoop's hole that is still draining all the way at once (e.g. before saving).</summary>
        public void CompleteScoopNow()
        {
            if (_settling != null)
            {
                _settling.CompleteScoopNow();
            }
        }

        /// <summary>
        /// Carves a dig's full sphere at once (local space) without counting anything: for settling to make sure a
        /// scoop's ash is out of the pile if its simulation failed.
        /// </summary>
        public void CarveNow(Vector3 localCenter, float radius)
        {
            _terrain.CarveDig(localCenter, radius);
            _meshes.RebuildDirtyChunks();
        }

        /// <summary>
        /// Called once a dig's hole is open (by settling, when a scoop's ash is all out of the pile): lays the embers
        /// it grazed and fires <see cref="Dug"/>.
        /// </summary>
        public void CompleteScoop(DigResult result, Vector3 localCenter, float radius, DigExposure exposure)
        {
            LayExposed(exposure);
            long listenersStarted = TimeMeasure.Now();
            Dug?.Invoke(result, localCenter, radius);
            LastDugListenersMilliseconds = TimeMeasure.MillisecondsSince(listenersStarted);
        }

        /// <summary>Collects a loose ember (e.g. into the sieve): it leaves the mountain.</summary>
        /// <param name="worldPosition">Where it lay.</param>
        /// <returns>False if no loose ember has this id (any more).</returns>
        public bool TryTakeLoose(int id, out Ember ember, out Vector3 worldPosition)
        {
            ember = default;
            worldPosition = default;
            int index = _terrain != null ? _terrain.IndexOfLoose(id) : -1;
            if (index < 0)
            {
                return false;
            }

            worldPosition = transform.TransformPoint(_terrain.LooseEmbers[index].Ember.Position);
            ember = _terrain.TakeLoose(index);
            EmbersCollected?.Invoke();
            return true;
        }

        /// <summary>
        /// Puts an ember that left the mountain back into the world, lying loose (e.g. one that slipped through the
        /// sieve's mesh); it counts as on the mountain again until collected.
        /// </summary>
        public void DropLoose(Ember ember, Vector3 worldPosition)
        {
            if (_terrain == null)
            {
                return;
            }

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            _terrain.AddDroppedLoose(new Ember(local, ember.TierIndex, ember.Layer));
            LooseEmbersChanged?.Invoke();
        }

        // Embers the dig only grazed lie loose once its hole is open: earlier, the walls still draining in would
        // cover them straight away.
        private void LayExposed(DigExposure exposure)
        {
            List<LooseEmber> embers = exposure.Embers;
            for (int i = 0; i < embers.Count; i++)
            {
                _terrain.AddLoose(embers[i].Ember, embers[i].CameFrom);
            }

            bool any = embers.Count > 0;
            if (any)
            {
                LooseEmbersChanged?.Invoke();
            }

            if (exposure.Finds.Count > 0)
            {
                FindsUncovered?.Invoke(exposure.Finds);
            }

            exposure.Clear();
            _exposures.Push(exposure);
        }

        /// <summary>Opens the next ash layer for digging (Heat levels, or the F4 debug key).</summary>
        /// <returns>The layer that opened, or -1 if all are open.</returns>
        public int UnlockNextLayer()
        {
            int layer = _terrain != null ? _terrain.Locks.UnlockNext() : -1;
            if (layer < 0)
            {
                return layer;
            }

            MeltCrustAbove(layer);
            LayerUnlocked?.Invoke(layer);
            return layer;
        }

        // The nest's heat melts what is left of the layers above the one that opened: no barren ash stands between the
        // player and the new layer, and the mountain visibly sinks. Their last embers come out for the nest (CrustMelted).
        // The re-meshing is spread over the next frames, as for sliding ash, so it reads as the crust crumbling away.
        private void MeltCrustAbove(int layer)
        {
            if (_settling != null)
            {
                _settling.WriteOutNow();
            }

            _melted.Clear();
            _meltedFinds.Clear();
            _terrain.MeltLayersAbove(layer, _melted, _meltedFinds);
            _meshes.ScheduleDirtyChunks();
            CrustMelted?.Invoke(layer, _melted);
            if (_meltedFinds.Count > 0)
            {
                FindsUncovered?.Invoke(_meltedFinds);
            }

            LooseEmbersChanged?.Invoke();
        }

        /// <summary>
        /// Re-meshes what settling ash just changed and tells listeners where. The meshes go up a frame later: sliding ash
        /// changes many times a second, and waiting for the meshing each time would cost the main thread milliseconds.
        /// </summary>
        /// <param name="localBounds">The changed box in this object's local space.</param>
        public void ApplySettledEdits(Bounds localBounds)
        {
            if (_terrain == null)
            {
                return;
            }

            _meshes.ScheduleDirtyChunks();
            Settled?.Invoke(localBounds);
        }

        /// <summary>Tells listeners a slide is over and where it changed things in total.</summary>
        public void FinishSettle(Bounds localBounds)
        {
            SettleFinished?.Invoke(localBounds);
        }

        /// <summary>Re-meshes the chunks a save restored and tells listeners the totals changed.</summary>
        public void CommitRestore()
        {
            if (_terrain == null)
            {
                return;
            }

            _meshes.RebuildDirtyChunks(true);
            Restored?.Invoke();
        }

        /// <summary>Ash density (-1 air .. 1 ash) at a world position; -1 before the mountain is built.</summary>
        public float SampleDensityAt(Vector3 worldPosition)
        {
            return _terrain != null ? _terrain.World.SampleDensity(transform.InverseTransformPoint(worldPosition)) : VoxelGrid.Air;
        }

        /// <summary>The top of the ash straight above or below a world position; false where there is none.</summary>
        public bool TryFindTop(Vector3 worldPosition, out Vector3 top)
        {
            top = worldPosition;
            if (_terrain == null)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            if (!_terrain.World.TryFindTop(local.x, local.z, out float y))
            {
                return false;
            }

            local.y = y;
            top = transform.TransformPoint(local);
            return true;
        }

        /// <summary>
        /// What a dig here would take, without digging: ash that can be removed (sealed layers stay) and the first sealed
        /// layer in the way. Embers are not counted.
        /// </summary>
        public DigResult MeasureDig(Vector3 worldCenter, float radius)
        {
            if (_terrain == null)
            {
                return default;
            }

            return SphereDigBrush.Measure(_terrain.World, transform.InverseTransformPoint(worldCenter), radius, _terrain.Locks, null);
        }

        /// <summary>The dust color of an ash layer, as the sieve shows it.</summary>
        public Color DustColorOfLayer(int layer)
        {
            return _palette.DustOf(layer);
        }

        /// <summary>The ash and dust colors of a dug load, blended by how much came from each layer.</summary>
        public void MixLayerColors(float[] removedByLayer, out Color ashColor, out Color dustColor)
        {
            _palette.Mix(removedByLayer, out ashColor, out dustColor);
        }

        public string LayerName(int layer)
        {
            return _layerCatalog != null ? _layerCatalog.NameOf(layer) : $"Layer {layer + 1}";
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
