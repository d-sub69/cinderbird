using System.Collections.Generic;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Shows small glowing dots on the mountain where buried embers are close to the surface.
    /// Candidates are kept per chunk and recalculated only for chunks near a dig; the dots go to the
    /// candidates nearest the viewer, re-picked a couple of times a second as the player moves.
    /// </summary>
    [RequireComponent(typeof(VoxelTerrain))]
    public sealed class EmberGlimmerView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

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
                _terrain.DigStarted += OnDug;
                _terrain.SettleFinished += OnSettled;
                _terrain.Built += OnTerrainBuilt;
                _terrain.Restored += OnTerrainBuilt;
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
                _terrain.DigStarted -= OnDug;
                _terrain.SettleFinished -= OnSettled;
                _terrain.Built -= OnTerrainBuilt;
                _terrain.Restored -= OnTerrainBuilt;
            }
        }

        // Also after a save is restored: whole chunks of ash and embers changed at once.
        private void OnTerrainBuilt()
        {
            AshTerrain terrain = _terrain.Terrain;
            if (terrain == null)
            {
                return;
            }

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

        private void RefreshAround(Vector3 center, float radius)
        {
            if (_chunkCandidates == null)
            {
                return;
            }

            // An ember's glimmer comes from probing up to MaxDepth around it, and the change touched samples only
            // within its radius (plus one cell of blending). Embers farther away than both can't have changed, so
            // only the ones within reach are probed again; probing every ember of the surrounding chunks took ms.
            AshTerrain terrain = _terrain.Terrain;
            float reach = radius + _settings.MaxDepth + terrain.World.CellSize;
            Vector3 extent = Vector3.one * reach;
            terrain.World.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            for (int i = 0; i < _chunkScratch.Count; i++)
            {
                RecollectNear(terrain, _chunkScratch[i], center, reach * reach);
            }

            Reselect();
        }

        private void RecollectNear(AshTerrain terrain, int chunk, Vector3 center, float reachSquared)
        {
            // Drop this area's glimmers, including those of embers the dig just took...
            List<GlimmerPoint> candidates = _chunkCandidates[chunk];
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if ((candidates[i].EmberPosition - center).sqrMagnitude <= reachSquared)
                {
                    int last = candidates.Count - 1;
                    candidates[i] = candidates[last];
                    candidates.RemoveAt(last);
                }
            }

            // ...then probe the embers still buried there.
            EmberField embers = terrain.EmbersIn(chunk);
            for (int i = 0; i < embers.Count; i++)
            {
                Ember ember = embers[i];
                if ((ember.Position - center).sqrMagnitude <= reachSquared)
                {
                    TryAddCandidate(terrain, ember, candidates);
                }
            }
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
