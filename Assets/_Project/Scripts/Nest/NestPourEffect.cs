using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// After a feeding, flies the embers one by one from the basket to the fire along an arc,
    /// and reports each landing so the fire can flare. Purely visual: the currencies are already updated.
    /// </summary>
    public sealed class NestPourEffect : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Embers leave from slightly different points so the stream doesn't look like a single bead.
        private const float LaunchSpread = 0.08f;

        [SerializeField] private NestController _nest;
        [SerializeField] private NestSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Small sphere with an emissive URP Lit material and no collider.")]
        [SerializeField] private Renderer _emberPrefab;
        [Tooltip("Where embers land: the base of the fire. They start from whichever tool was poured.")]
        [SerializeField] private Transform _landingPoint;

        private Renderer[] _visuals;
        private float[] _startTimes;
        private Vector3[] _launchPositions;
        private int[] _tiers;
        private bool[] _inUse;
        private int _activeCount;
        private readonly System.Random _random = new System.Random();
        private MaterialPropertyBlock _propertyBlock;

        /// <summary>Fired with the ember's tier index when it reaches the fire.</summary>
        public event Action<int> EmberLanded;

        private void Awake()
        {
            if (_nest == null || _settings == null || _tierCatalog == null || _emberPrefab == null || _landingPoint == null)
            {
                Debug.LogError($"{nameof(NestPourEffect)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            CreatePool(_settings.MaxFlights);
        }

        private void OnEnable()
        {
            if (_visuals != null)
            {
                _nest.Fed += OnFed;
            }
        }

        private void OnDisable()
        {
            if (_visuals != null)
            {
                _nest.Fed -= OnFed;
            }
        }

        private void Update()
        {
            if (_activeCount == 0)
            {
                return;
            }

            float now = Time.time;
            Vector3 landing = _landingPoint.position;
            for (int i = 0; i < _visuals.Length; i++)
            {
                if (_inUse[i])
                {
                    AdvanceFlight(i, now - _startTimes[i], landing);
                }
            }
        }

        private void CreatePool(int count)
        {
            _visuals = new Renderer[count];
            _startTimes = new float[count];
            _launchPositions = new Vector3[count];
            _tiers = new int[count];
            _inUse = new bool[count];

            for (int i = 0; i < count; i++)
            {
                // Under this object to keep the Hierarchy tidy; positions are set in world space.
                // Keep the nest's scale at 1 so ember sizes come out right.
                Renderer visual = Instantiate(_emberPrefab, transform);
                visual.name = $"Flying Ember {i}";
                visual.gameObject.SetActive(false);
                _visuals[i] = visual;
            }
        }

        private void OnFed(FeedResult result, IReadOnlyList<Ember> embers, Vector3 fromPosition)
        {
            float now = Time.time;
            for (int i = 0; i < embers.Count; i++)
            {
                int tierIndex = embers[i].TierIndex;
                EmberTier tier = tierIndex >= 0 && tierIndex < _tierCatalog.Count ? _tierCatalog[tierIndex] : null;
                int slot = FindFreeSlot();

                // No visual available: still count the landing so the fire reacts to every ember.
                if (slot < 0 || tier == null)
                {
                    EmberLanded?.Invoke(tierIndex);
                    continue;
                }

                LaunchFlight(slot, tierIndex, tier, now + i * _settings.FlightStagger, fromPosition);
            }
        }

        private void LaunchFlight(int slot, int tierIndex, EmberTier tier, float startTime, Vector3 fromPosition)
        {
            _inUse[slot] = true;
            _activeCount++;
            _startTimes[slot] = startTime;
            _tiers[slot] = tierIndex;
            _launchPositions[slot] = fromPosition + RandomOffset(LaunchSpread);

            Renderer visual = _visuals[slot];
            visual.transform.localScale = Vector3.one * (tier.VisualScale * _settings.FlightScale);
            _propertyBlock.SetColor(BaseColorId, tier.Color);
            _propertyBlock.SetColor(EmissionColorId, tier.EmissionColor);
            visual.SetPropertyBlock(_propertyBlock);
        }

        private void AdvanceFlight(int slot, float elapsed, Vector3 landing)
        {
            // Staggered: not this ember's turn yet.
            if (elapsed < 0f)
            {
                return;
            }

            Renderer visual = _visuals[slot];
            float progress = elapsed / _settings.FlightDuration;
            if (progress >= 1f)
            {
                visual.gameObject.SetActive(false);
                _inUse[slot] = false;
                _activeCount--;
                EmberLanded?.Invoke(_tiers[slot]);
                return;
            }

            if (!visual.gameObject.activeSelf)
            {
                visual.gameObject.SetActive(true);
            }

            // Straight line plus a parabola (4p(1-p) peaks at 1 halfway) gives a simple lob.
            Vector3 position = Vector3.Lerp(_launchPositions[slot], landing, progress);
            position.y += _settings.FlightArcHeight * 4f * progress * (1f - progress);
            visual.transform.position = position;
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _inUse.Length; i++)
            {
                if (!_inUse[i])
                {
                    return i;
                }
            }

            return -1;
        }

        private Vector3 RandomOffset(float radius)
        {
            return new Vector3(
                ((float)_random.NextDouble() * 2f - 1f) * radius,
                ((float)_random.NextDouble() * 2f - 1f) * radius,
                ((float)_random.NextDouble() * 2f - 1f) * radius);
        }
    }
}
