using System.Collections.Generic;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Shows small glowing dots on the pile where buried embers are close to the surface.
    /// Positions are recalculated only after a dig; the per-frame work is just the twinkle.
    /// </summary>
    [RequireComponent(typeof(VoxelChunk))]
    public sealed class EmberGlimmerView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly System.Comparison<GlimmerPoint> ByDepth = (a, b) => a.Depth.CompareTo(b.Depth);

        [SerializeField] private GlimmerSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Small sphere with an emissive URP Lit material and no collider (Ember_Visual works).")]
        [SerializeField] private Renderer _glimmerPrefab;

        private VoxelChunk _chunk;
        private Renderer[] _visuals;
        private Color[] _emissionColors;
        private float[] _phases;
        private int _activeCount;
        private readonly List<GlimmerPoint> _candidates = new List<GlimmerPoint>();
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _chunk = GetComponent<VoxelChunk>();
            if (_settings == null || _tierCatalog == null || _glimmerPrefab == null)
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
                _chunk.Dug += OnDug;
            }
        }

        private void OnDisable()
        {
            if (_visuals != null)
            {
                _chunk.Dug -= OnDug;
            }
        }

        // Start, not Awake: the chunk builds its grid and embers in its own Awake, whose order relative to ours isn't guaranteed.
        private void Start()
        {
            Recalculate();
        }

        private void Update()
        {
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

        private void OnDug(DigResult result)
        {
            Recalculate();
        }

        private void CreatePool()
        {
            int count = _settings.MaxGlimmers;
            _visuals = new Renderer[count];
            _emissionColors = new Color[count];
            _phases = new float[count];

            for (int i = 0; i < count; i++)
            {
                // Children of the chunk, so chunk-local surface points can be used as local positions directly.
                Renderer visual = Instantiate(_glimmerPrefab, transform);
                visual.name = $"Glimmer {i}";
                visual.gameObject.SetActive(false);
                _visuals[i] = visual;
                _phases[i] = GlimmerTwinkle.PhaseFor(i);
            }
        }

        private void Recalculate()
        {
            VoxelGrid grid = _chunk.Grid;
            if (grid == null)
            {
                return;
            }

            CollectCandidates(grid);
            _candidates.Sort(ByDepth);

            _activeCount = Mathf.Min(_candidates.Count, _visuals.Length);
            for (int i = 0; i < _visuals.Length; i++)
            {
                bool active = i < _activeCount;
                _visuals[i].gameObject.SetActive(active);
                if (active)
                {
                    ShowGlimmer(i, _candidates[i]);
                }
            }
        }

        private void CollectCandidates(VoxelGrid grid)
        {
            _candidates.Clear();
            EmberField embers = _chunk.Embers;

            for (int i = 0; i < embers.Count; i++)
            {
                Ember ember = embers[i];
                if (ember.TierIndex < 0 || ember.TierIndex >= _tierCatalog.Count || _tierCatalog[ember.TierIndex] == null)
                {
                    continue;
                }

                if (EmberGlimmer.TryFindSurface(grid, ember.Position, _settings.MaxDepth, _chunk.LocalFloorHeight,
                        out Vector3 surfacePoint, out float depth))
                {
                    _candidates.Add(new GlimmerPoint(surfacePoint, depth, ember.TierIndex));
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
