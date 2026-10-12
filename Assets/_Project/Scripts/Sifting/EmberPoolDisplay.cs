using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Shows a list of embers as pooled objects on a disc, each ember on its own random, non-overlapping spot.
    /// Shared by the sieve and basket views. Assumes embers only ever leave from the end of the list,
    /// so an ember keeps its index, and therefore its spot, while it is shown.
    /// </summary>
    public sealed class EmberPoolDisplay
    {
        // URP Lit property names.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly Renderer[] _visuals;
        private readonly Vector2[] _spots;
        private readonly SieveSpotAssigner _spotAssigner;
        private readonly int[] _spotOfEmber;
        private readonly EmberTierCatalog _tierCatalog;
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
        private int _shownCount;

        /// <param name="radius">Usable disc radius; keep it a bit inside the rim so embers don't clip it.</param>
        /// <param name="logContext">Object that warnings point at in the Console.</param>
        public EmberPoolDisplay(
            Renderer prefab,
            Transform parent,
            int capacity,
            float radius,
            float jitter,
            EmberTierCatalog tierCatalog,
            Object logContext)
        {
            _tierCatalog = tierCatalog;
            // Time-based so layouts differ between sessions; mixed with the parent so two displays differ too.
            System.Random random = new System.Random(System.Environment.TickCount ^ parent.GetHashCode());

            // Cap the jitter so even the two largest embers on neighboring spots can't touch.
            float largestEmber = LargestEmberSize(tierCatalog);
            float safeJitter = SieveSpotLayout.MaxSafeJitter(capacity, radius, largestEmber);
            if (safeJitter < 0f)
            {
                Debug.LogWarning($"{capacity} ember spots don't fit in a {radius * 100f:F0} cm disc without " +
                                 $"{largestEmber * 100f:F1} cm embers touching. Lower the count or raise the radius.", logContext);
            }

            _spots = SieveSpotLayout.CreateSpots(capacity, radius, Mathf.Clamp(jitter, 0f, Mathf.Max(0f, safeJitter)), random);
            _spotAssigner = new SieveSpotAssigner(capacity, random);
            _spotOfEmber = new int[capacity];
            _visuals = new Renderer[capacity];

            for (int i = 0; i < capacity; i++)
            {
                Renderer visual = Object.Instantiate(prefab, parent);
                visual.name = $"Ember Visual {i}";
                visual.gameObject.SetActive(false);
                _visuals[i] = visual;
            }
        }

        public int Capacity => _visuals.Length;

        /// <summary>Embers currently assigned a spot (the first this-many of the list).</summary>
        public int ShownCount => _shownCount;

        /// <summary>
        /// Places, colors and shows the embers resting on a surface at <paramref name="surfaceHeight"/>.
        /// </summary>
        /// <returns>How many were shown before this call; indices from there up to <see cref="ShownCount"/> are new.</returns>
        public int Sync(IReadOnlyList<Ember> embers, float surfaceHeight)
        {
            int previous = _shownCount;
            UpdateSpotAssignments(Mathf.Min(embers.Count, _visuals.Length));

            for (int i = 0; i < _visuals.Length; i++)
            {
                Renderer visual = _visuals[i];
                EmberTier tier = null;
                bool visible = i < _shownCount && TryGetTier(embers[i].TierIndex, out tier);
                visual.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                float size = tier.VisualScale;
                Vector2 spot = _spots[_spotOfEmber[i]];
                Transform visualTransform = visual.transform;
                visualTransform.localScale = Vector3.one * size;
                // Slightly sunk into the surface so they read as resting in it, not floating on it.
                visualTransform.localPosition = new Vector3(spot.x, surfaceHeight + size * 0.3f, spot.y);

                _propertyBlock.SetColor(BaseColorId, tier.Color);
                _propertyBlock.SetColor(EmissionColorId, tier.EmissionColor);
                visual.SetPropertyBlock(_propertyBlock);
            }

            return previous;
        }

        public Transform VisualTransform(int index)
        {
            return _visuals[index].transform;
        }

        private void UpdateSpotAssignments(int count)
        {
            // New embers each take a random free spot.
            while (_shownCount < count)
            {
                _spotOfEmber[_shownCount] = _spotAssigner.TakeRandom();
                _shownCount++;
            }

            // Embers that left give their spots back.
            while (_shownCount > count)
            {
                _shownCount--;
                _spotAssigner.Release(_spotOfEmber[_shownCount]);
            }
        }

        private bool TryGetTier(int index, out EmberTier tier)
        {
            tier = index >= 0 && index < _tierCatalog.Count ? _tierCatalog[index] : null;
            return tier != null;
        }

        private static float LargestEmberSize(EmberTierCatalog catalog)
        {
            float largest = 0f;
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null)
                {
                    largest = Mathf.Max(largest, catalog[i].VisualScale);
                }
            }

            return largest;
        }
    }
}
