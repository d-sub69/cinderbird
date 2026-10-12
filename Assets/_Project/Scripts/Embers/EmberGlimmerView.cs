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

        // How often the nearest glimmers are re-picked as the viewer moves; picking walks every candidate.
        private const float ReselectInterval = 0.5f;

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
        private int _activeCount;
        private List<GlimmerPoint>[] _chunkCandidates;
        private readonly List<int> _chunkScratch = new List<int>();
        private MaterialPropertyBlock _propertyBlock;
        private float _nextReselectTime;

        // Nearest-N selection buffers, reused so picking doesn't allocate.
        private float[] _bestDistances;
        private GlimmerPoint[] _best;

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
            }
        }

        private void OnDisable()
        {
            if (_visuals != null)
            {
                _terrain.Dug -= OnDug;
            }
        }

        // Start, not Awake: the terrain builds the mountain and its embers in its own Awake.
        private void Start()
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

            if (_settings.TwinkleSpeed <= 0f)
            {
                return;
            }

            float time = Time.time;
            for (int i = 0; i < _activeCount; i++)
            {
                float dim = GlimmerTwinkle.Dim(time, _phases[i], _settings.TwinkleSpeed, _settings.TwinkleAmount);
                ApplyColor(i, _emissionColors[i] * dim);
            }
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            if (_chunkCandidates == null)
            {
                return;
            }

            // Any ember within glimmer depth of the new hole may have changed, including in neighboring chunks.
            AshTerrain terrain = _terrain.Terrain;
            Vector3 reach = Vector3.one * (radius + _settings.MaxDepth + terrain.World.CellSize);
            terrain.World.ChunksOverlapping(center - reach, center + reach, _chunkScratch);
            for (int i = 0; i < _chunkScratch.Count; i++)
            {
                CollectCandidates(terrain, _chunkScratch[i]);
            }

            Reselect();
        }

        private void CreatePool()
        {
            int count = _settings.MaxGlimmers;
            _visuals = new Renderer[count];
            _emissionColors = new Color[count];
            _phases = new float[count];
            _bestDistances = new float[count];
            _best = new GlimmerPoint[count];

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
                Ember ember = embers[i];
                if (ember.TierIndex < 0 || ember.TierIndex >= _tierCatalog.Count || _tierCatalog[ember.TierIndex] == null)
                {
                    continue;
                }

                if (EmberGlimmer.TryFindSurface(terrain.World, ember.Position, _settings.MaxDepth, _terrain.LocalFloorHeight,
                        out Vector3 surfacePoint, out float depth))
                {
                    candidates.Add(new GlimmerPoint(surfacePoint, depth, ember.TierIndex));
                }
            }
        }

        // Keeps the N candidates nearest the viewer with a small insertion sort; N is a couple dozen.
        private void Reselect()
        {
            _nextReselectTime = Time.time + ReselectInterval;
            Vector3 eye = transform.InverseTransformPoint(_viewer.position);
            int capacity = _visuals.Length;
            int count = 0;

            for (int chunk = 0; chunk < _chunkCandidates.Length; chunk++)
            {
                List<GlimmerPoint> candidates = _chunkCandidates[chunk];
                for (int i = 0; i < candidates.Count; i++)
                {
                    float distance = (candidates[i].SurfacePosition - eye).sqrMagnitude;
                    if (count == capacity && distance >= _bestDistances[count - 1])
                    {
                        continue;
                    }

                    int slot = count < capacity ? count++ : count - 1;
                    while (slot > 0 && _bestDistances[slot - 1] > distance)
                    {
                        _bestDistances[slot] = _bestDistances[slot - 1];
                        _best[slot] = _best[slot - 1];
                        slot--;
                    }

                    _bestDistances[slot] = distance;
                    _best[slot] = candidates[i];
                }
            }

            _activeCount = count;
            for (int i = 0; i < capacity; i++)
            {
                bool active = i < count;
                if (_visuals[i].gameObject.activeSelf != active)
                {
                    _visuals[i].gameObject.SetActive(active);
                }

                if (active)
                {
                    ShowGlimmer(i, _best[i]);
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
            visualTransform.localPosition = glimmer.SurfacePosition;
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
