using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Draws the sieve's contents: an ash disc whose height follows the ash volume, and pooled ember
    /// objects resting on top of it. Redraws only when the contents change.
    /// </summary>
    public sealed class SieveView : MonoBehaviour
    {
        // URP Lit property names.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Spots stay inside this share of the sieve radius so embers don't clip the rim.
        private const float UsableRadiusFraction = 0.85f;

        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Cylinder representing the ash. Its local y = 0 should be the sieve floor.")]
        [SerializeField] private Transform _ashDisc;
        [Tooltip("Parent for ember visuals; its local XZ plane is the sieve floor.")]
        [SerializeField] private Transform _emberRoot;
        [Tooltip("Small sphere with an emissive URP Lit material and no collider.")]
        [SerializeField] private Renderer _emberPrefab;

        private Renderer[] _emberVisuals;
        private Vector2[] _spots;
        private SieveSpotAssigner _spotAssigner;
        // Spot taken by the ember at each index in the sieve's list. Embers are only appended until the
        // sieve is emptied, so an ember keeps its index, and therefore its spot, while it stays in the sieve.
        private int[] _spotOfEmber;
        private int _assignedCount;
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            if (_sieve == null || _settings == null || _tierCatalog == null || _ashDisc == null || _emberRoot == null || _emberPrefab == null)
            {
                Debug.LogError($"{nameof(SieveView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            CreateSpots();
            CreateEmberPool();
        }

        private void OnEnable()
        {
            if (_emberVisuals == null)
            {
                return;
            }

            _sieve.Contents.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_emberVisuals == null)
            {
                return;
            }

            _sieve.Contents.Changed -= Refresh;
        }

        private void CreateSpots()
        {
            int count = _settings.MaxVisibleEmbers;
            float radius = _settings.Radius * UsableRadiusFraction;
            // Time-based seed so the layout differs between play sessions.
            System.Random random = new System.Random(System.Environment.TickCount);

            // Cap the jitter so even the two largest embers on neighboring spots can't touch.
            float largestEmber = LargestEmberSize();
            float safeJitter = SieveSpotLayout.MaxSafeJitter(count, radius, largestEmber);
            if (safeJitter < 0f)
            {
                Debug.LogWarning($"{count} ember spots don't fit in the sieve without {largestEmber * 100f:F1} cm embers touching. " +
                                 "Lower Max Visible Embers or raise the sieve Radius.", this);
            }

            float jitter = Mathf.Clamp(_settings.SpotJitter, 0f, Mathf.Max(0f, safeJitter));
            _spots = SieveSpotLayout.CreateSpots(count, radius, jitter, random);
            _spotAssigner = new SieveSpotAssigner(count, random);
            _spotOfEmber = new int[count];
        }

        private void CreateEmberPool()
        {
            _emberVisuals = new Renderer[_settings.MaxVisibleEmbers];
            for (int i = 0; i < _emberVisuals.Length; i++)
            {
                Renderer visual = Instantiate(_emberPrefab, _emberRoot);
                visual.name = $"Ember Visual {i}";
                visual.gameObject.SetActive(false);
                _emberVisuals[i] = visual;
            }
        }

        private void Refresh()
        {
            SieveContents contents = _sieve.Contents;
            float fill = Mathf.Clamp01(contents.AshVolume / _settings.ReferenceAshVolume);
            float ashHeight = fill * _settings.FullAshHeight;

            UpdateAshDisc(ashHeight);
            UpdateSpotAssignments(Mathf.Min(contents.Embers.Count, _emberVisuals.Length));
            UpdateEmbers(contents.Embers, ashHeight);
        }

        private void UpdateSpotAssignments(int visibleCount)
        {
            // New embers each take a random free spot.
            while (_assignedCount < visibleCount)
            {
                _spotOfEmber[_assignedCount] = _spotAssigner.TakeRandom();
                _assignedCount++;
            }

            // Embers that left the sieve give their spots back.
            while (_assignedCount > visibleCount)
            {
                _assignedCount--;
                _spotAssigner.Release(_spotOfEmber[_assignedCount]);
            }
        }

        private void UpdateAshDisc(float height)
        {
            bool visible = height > 0f;
            _ashDisc.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            // Unity's cylinder primitive is 2 units tall at scale 1, so scale.y is half the height.
            // Raising it by the same amount keeps the bottom on the sieve floor.
            float halfHeight = height * 0.5f;
            Vector3 scale = _ashDisc.localScale;
            _ashDisc.localScale = new Vector3(scale.x, halfHeight, scale.z);
            Vector3 position = _ashDisc.localPosition;
            _ashDisc.localPosition = new Vector3(position.x, halfHeight, position.z);
        }

        private void UpdateEmbers(IReadOnlyList<Ember> embers, float surfaceHeight)
        {
            for (int i = 0; i < _emberVisuals.Length; i++)
            {
                Renderer visual = _emberVisuals[i];
                EmberTier tier = null;
                bool visible = i < _assignedCount && TryGetTier(embers[i].TierIndex, out tier);
                visual.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                float size = tier.VisualScale;
                Vector2 spot = _spots[_spotOfEmber[i]];
                Transform visualTransform = visual.transform;
                visualTransform.localScale = Vector3.one * size;
                // Slightly sunk into the ash surface so they read as caught in it, not floating on it.
                visualTransform.localPosition = new Vector3(spot.x, surfaceHeight + size * 0.3f, spot.y);

                _propertyBlock.SetColor(BaseColorId, tier.Color);
                _propertyBlock.SetColor(EmissionColorId, tier.EmissionColor);
                visual.SetPropertyBlock(_propertyBlock);
            }
        }

        private float LargestEmberSize()
        {
            float largest = 0f;
            for (int i = 0; i < _tierCatalog.Count; i++)
            {
                if (_tierCatalog[i] != null)
                {
                    largest = Mathf.Max(largest, _tierCatalog[i].VisualScale);
                }
            }

            return largest;
        }

        private bool TryGetTier(int index, out EmberTier tier)
        {
            tier = index >= 0 && index < _tierCatalog.Count ? _tierCatalog[index] : null;
            return tier != null;
        }
    }
}
