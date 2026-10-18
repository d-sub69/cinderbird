using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Terrain;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Shows small glowing dots on the mountain where buried embers are close to the surface.
    /// Candidates are kept per chunk and recalculated only around a change (a dig, a slide), a slice per frame within a
    /// small time budget, so even a huge change never stalls a frame. The dots go to the candidates nearest the
    /// viewer, re-picked a couple of times a second as the player moves.
    /// </summary>
    [RequireComponent(typeof(VoxelTerrain))]
    public sealed class EmberGlimmerView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly ProfilerMarker RefreshMarker = new ProfilerMarker("Cinderbird.Glimmers");
        // Embers this far beyond a dig may have been taken or uncovered by it; their glimmers go at once.
        private const float TakenMargin = 0.3f;

        [SerializeField] private GlimmerSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Small sphere with an emissive URP Lit material and no collider (Ember_Visual works).")]
        [SerializeField] private Renderer _glimmerPrefab;
        [Tooltip("Glimmers nearest to this (usually the camera) are shown.")]
        [SerializeField] private Transform _viewer;

        private VoxelTerrain _terrain;
        private Renderer[] _visuals;
        private Color[] _emissionColors;
        private float[] _phases;
        private GlimmerSelection _selection;
        private List<GlimmerPoint>[] _chunkCandidates;
        private readonly List<int> _chunkScratch = new List<int>();
        private MaterialPropertyBlock _propertyBlock;
        private float _nextReselectTime;
        private readonly List<GlimmerRefreshJob> _jobs = new List<GlimmerRefreshJob>();
        private readonly Stack<GlimmerRefreshJob> _jobPool = new Stack<GlimmerRefreshJob>();

        private void Awake()
        {
            _terrain = GetComponent<VoxelTerrain>();
            if (_settings == null || _tierCatalog == null || _glimmerPrefab == null || _viewer == null)
            {
                Debug.LogError($"{nameof(EmberGlimmerView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            CreatePool();
        }

        private void OnEnable()
        {
            if (_visuals != null)
            {
                _terrain.Dug += OnDug;
                _terrain.DigStarted += OnDigStarted;
                _terrain.SettleFinished += OnSettled;
                _terrain.Built += OnTerrainBuilt;
                _terrain.Restored += OnTerrainBuilt;
                _terrain.LayerUnlocked += OnLayerUnlocked;
                if (_terrain.IsBuilt)
                {
                    OnTerrainBuilt();
                }
            }
        }

        private void OnDisable()
        {
            if (_visuals != null)
            {
                _terrain.Dug -= OnDug;
                _terrain.DigStarted -= OnDigStarted;
                _terrain.SettleFinished -= OnSettled;
                _terrain.Built -= OnTerrainBuilt;
                _terrain.Restored -= OnTerrainBuilt;
                _terrain.LayerUnlocked -= OnLayerUnlocked;
            }
        }

        // The opened layer's embers near the surface start to glimmer: part of the layer melting open. Rare, so a full rescan.
        private void OnLayerUnlocked(int layer)
        {
            OnTerrainBuilt();
        }

        // Also after a save is restored: whole chunks of ash and embers changed at once.
        private void OnTerrainBuilt()
        {
            AshTerrain terrain = _terrain.Terrain;
            if (terrain == null)
            {
                return;
            }

            ClearJobs();
            _chunkCandidates = new List<GlimmerPoint>[terrain.World.ChunkCount];
            for (int chunk = 0; chunk < _chunkCandidates.Length; chunk++)
            {
                _chunkCandidates[chunk] = new List<GlimmerPoint>();
                CollectCandidates(terrain, chunk);
            }

            Reselect();
        }

        private void Update()
        {
            if (_chunkCandidates == null)
            {
                return;
            }

            ProcessRefreshes();
            if (Time.time >= _nextReselectTime)
            {
                Reselect();
            }

            GlideToTargets();

            if (_settings.TwinkleSpeed <= 0f)
            {
                return;
            }

            float time = Time.time;
            for (int i = 0; i < _selection.Count; i++)
            {
                float dim = GlimmerTwinkle.Dim(time, _phases[i], _settings.TwinkleSpeed, _settings.TwinkleAmount);
                ApplyColor(i, _emissionColors[i] * dim);
            }
        }

        // The embers a dig takes leave at once, so their glimmers go at once; the rest around it is worked out again over
        // the next frames.
        private void OnDigStarted(DigResult result, Vector3 center, float radius)
        {
            RemoveCandidatesNear(center, radius + TakenMargin);
            RefreshAround(center, radius);
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            RefreshAround(center, radius);
        }

        // A slide changed a box of the surface; everything within reach of it may glimmer differently now. Done once
        // per slide, not every settling update: re-probing a wide area a dozen times a second cost milliseconds.
        private void OnSettled(Bounds changed)
        {
            RefreshAround(changed.center, changed.extents.magnitude);
        }

        // An ember's glimmer comes from probing up to MaxDepth around it, and the change touched samples only within its
        // radius (plus one cell of blending). Embers farther away than both can't have changed, so only the ones within
        // reach are probed again, chunk by chunk over the next frames.
        private void RefreshAround(Vector3 center, float radius)
        {
            if (_chunkCandidates == null)
            {
                return;
            }

            AshTerrain terrain = _terrain.Terrain;
            float reach = radius + _settings.MaxDepth + terrain.World.CellSize;
            Vector3 extent = Vector3.one * reach;
            terrain.World.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            foreach (int chunk in _chunkScratch)
            {
                GlimmerRefreshJob job = FindJob(chunk);
                if (job != null)
                {
                    job.Widen(center, reach);
                    continue;
                }

                job = _jobPool.Count > 0 ? _jobPool.Pop() : new GlimmerRefreshJob();
                job.Start(chunk, center, reach);
                _jobs.Add(job);
            }
        }

        private GlimmerRefreshJob FindJob(int chunk)
        {
            foreach (GlimmerRefreshJob job in _jobs)
            {
                if (job.Chunk == chunk)
                {
                    return job;
                }
            }

            return null;
        }

        // Probes embers until the frame's budget is spent; a chunk's new glimmers replace its old ones once it's done.
        private void ProcessRefreshes()
        {
            if (_jobs.Count == 0)
            {
                return;
            }

            using (RefreshMarker.Auto())
            {
                AshTerrain terrain = _terrain.Terrain;
                long started = TimeMeasure.Now();
                float budget = _settings.RefreshBudgetMilliseconds;
                bool applied = false;
                bool outOfTime = false;
                while (_jobs.Count > 0 && !outOfTime)
                {
                    GlimmerRefreshJob job = _jobs[0];
                    EmberField embers = terrain.EmbersIn(job.Chunk);
                    float reachSquared = job.ReachSquared;
                    while (job.Cursor < embers.Count)
                    {
                        Ember ember = embers[job.Cursor++];
                        if ((ember.Position - job.Center).sqrMagnitude <= reachSquared)
                        {
                            TryAddCandidate(terrain, ember, job.Found);
                        }

                        if ((job.Cursor & 15) == 0 && TimeMeasure.MillisecondsSince(started) >= budget)
                        {
                            outOfTime = true;
                            break;
                        }
                    }

                    if (job.Cursor < embers.Count)
                    {
                        continue;
                    }

                    ApplyJob(job);
                    _jobs.RemoveAt(0);
                    _jobPool.Push(job);
                    applied = true;
                    outOfTime |= TimeMeasure.MillisecondsSince(started) >= budget;
                }

                if (applied)
                {
                    Reselect();
                }
            }
        }

        private void ApplyJob(GlimmerRefreshJob job)
        {
            RemoveFrom(_chunkCandidates[job.Chunk], job.Center, job.ReachSquared);
            _chunkCandidates[job.Chunk].AddRange(job.Found);
            job.Found.Clear();
        }

        private void RemoveCandidatesNear(Vector3 center, float radius)
        {
            if (_chunkCandidates == null)
            {
                return;
            }

            Vector3 extent = Vector3.one * radius;
            _terrain.Terrain.World.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            foreach (int chunk in _chunkScratch)
            {
                RemoveFrom(_chunkCandidates[chunk], center, radius * radius);
            }

            Reselect();
        }

        private static void RemoveFrom(List<GlimmerPoint> candidates, Vector3 center, float reachSquared)
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if ((candidates[i].EmberPosition - center).sqrMagnitude <= reachSquared)
                {
                    int last = candidates.Count - 1;
                    candidates[i] = candidates[last];
                    candidates.RemoveAt(last);
                }
            }
        }

        private void ClearJobs()
        {
            foreach (GlimmerRefreshJob job in _jobs)
            {
                _jobPool.Push(job);
            }

            _jobs.Clear();
        }

        private void CreatePool()
        {
            int count = _settings.MaxGlimmers;
            _visuals = new Renderer[count];
            _emissionColors = new Color[count];
            _phases = new float[count];
            _selection = new GlimmerSelection(count);

            for (int i = 0; i < count; i++)
            {
                // Children of the terrain, so terrain-local surface points can be used as local positions directly.
                Renderer visual = Instantiate(_glimmerPrefab, transform);
                visual.name = $"Glimmer {i}";
                visual.gameObject.SetActive(false);
                _visuals[i] = visual;
                _phases[i] = GlimmerTwinkle.PhaseFor(i);
            }
        }

        private void CollectCandidates(AshTerrain terrain, int chunk)
        {
            List<GlimmerPoint> candidates = _chunkCandidates[chunk];
            candidates.Clear();
            EmberField embers = terrain.EmbersIn(chunk);

            for (int i = 0; i < embers.Count; i++)
            {
                TryAddCandidate(terrain, embers[i], candidates);
            }
        }

        private void TryAddCandidate(AshTerrain terrain, Ember ember, List<GlimmerPoint> candidates)
        {
            if (ember.TierIndex < 0 || ember.TierIndex >= _tierCatalog.Count || _tierCatalog[ember.TierIndex] == null)
            {
                return;
            }

            // A sealed layer's embers can't be scooped yet, so showing them would only tease; they appear when it opens.
            if (!terrain.Locks.IsUnlocked(ember.Layer))
            {
                return;
            }

            if (EmberGlimmer.TryFindSurface(terrain.World, ember.Position, _settings.MaxDepth, _terrain.LocalFloorHeight,
                    out Vector3 surfacePoint, out float depth))
            {
                candidates.Add(new GlimmerPoint(surfacePoint, depth, ember.TierIndex, ember.Position));
            }
        }

        // Shows the glimmers nearest the viewer; ones that moved a little glide there (see <see cref="GlimmerSelection"/>).
        private void Reselect()
        {
            _nextReselectTime = Time.time + _settings.ReselectInterval;
            _selection.Pick(_chunkCandidates, transform.InverseTransformPoint(_viewer.position), _settings.FollowDistance);
            int count = _selection.Count;
            for (int i = 0; i < _visuals.Length; i++)
            {
                bool active = i < count;
                if (_visuals[i].gameObject.activeSelf != active)
                {
                    _visuals[i].gameObject.SetActive(active);
                }

                if (active)
                {
                    ShowGlimmer(i, _selection[i]);
                }
            }
        }

        private void GlideToTargets()
        {
            float follow = 1f - Mathf.Exp(-_settings.FollowRate * Time.deltaTime);
            for (int i = 0; i < _selection.Count; i++)
            {
                if (_selection.Glide(i, follow))
                {
                    _visuals[i].transform.localPosition = _selection.ShownAt(i);
                }
            }
        }

        private void ShowGlimmer(int slot, GlimmerPoint glimmer)
        {
            EmberTier tier = _tierCatalog[glimmer.TierIndex];
            float closeness = 1f - Mathf.Clamp01(glimmer.Depth / _settings.MaxDepth);
            float brightness = _settings.Brightness * Mathf.Lerp(_settings.DeepBrightness, 1f, closeness);

            Transform visualTransform = _visuals[slot].transform;
            // Centered on the surface point, so about half the dot pokes out of the ash.
            visualTransform.localPosition = _selection.ShownAt(slot);
            visualTransform.localScale = Vector3.one * (tier.VisualScale * _settings.SizeScale);

            _emissionColors[slot] = tier.EmissionColor * brightness;
            ApplyColor(slot, _emissionColors[slot]);
        }

        private void ApplyColor(int slot, Color emission)
        {
            // Black base so only the emission shows: it reads as light through the ash, not a bead sitting on it.
            _propertyBlock.SetColor(BaseColorId, Color.black);
            _propertyBlock.SetColor(EmissionColorId, emission);
            _visuals[slot].SetPropertyBlock(_propertyBlock);
        }
    }
}
