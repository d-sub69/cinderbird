using System;
using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// Thin adapter: makes the nest a pour target (look at it and pour the sieve or basket in), and works
    /// out the Heat goal from the pile at start. Needs a collider (a trigger is fine) for the look raycast.
    /// </summary>
    public sealed class NestController : MonoBehaviour, IPourTarget
    {
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [SerializeField] private Wallet _wallet;
        [Tooltip("The pile whose buried embers define the total Heat to collect.")]
        [SerializeField] private VoxelTerrain _pile;

        private NestIntake _intake;
        private int[] _glowByTier;
        private int[] _heatByTier;

        /// <summary>Fired after a feeding with what it earned, the embers that went in, and where they came from.</summary>
        public event Action<FeedResult, IReadOnlyList<Ember>, Vector3> Fed;

        public string DisplayName => "the nest";

        public IEmberSink Sink => _intake;

        /// <summary>Total Heat of every ember in the pile at start; 0 until Start has run.</summary>
        public int HeatGoal { get; private set; }

        public float Progress => HeatGoal > 0 ? Mathf.Clamp01((float)_wallet.Currencies.Heat / HeatGoal) : 0f;

        public int HeatOf(int tierIndex)
        {
            return _heatByTier != null && tierIndex >= 0 && tierIndex < _heatByTier.Length ? _heatByTier[tierIndex] : 0;
        }

        private void Awake()
        {
            if (_tierCatalog == null || _wallet == null || _pile == null)
            {
                Debug.LogError($"{nameof(NestController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            CacheTierValues();
            _intake = new NestIntake(_wallet.Currencies, _glowByTier, _heatByTier);
        }

        // Start, not Awake: the pile spawns its embers in its own Awake.
        private void Start()
        {
            if (_pile.Terrain == null)
            {
                return;
            }

            // One-off at start; the mountain can hold tens of thousands of embers spread over many chunks.
            List<Ember> allEmbers = new List<Ember>();
            _pile.Terrain.CopyAllEmbers(allEmbers);
            HeatGoal = EmberValuation.Evaluate(allEmbers, _glowByTier, _heatByTier).Heat;
        }

        public void OnPoured(IReadOnlyList<Ember> embers, Vector3 fromPosition)
        {
            Fed?.Invoke(_intake.LastResult, embers, fromPosition);
        }

        private void CacheTierValues()
        {
            int count = _tierCatalog.Count;
            _glowByTier = new int[count];
            _heatByTier = new int[count];
            for (int i = 0; i < count; i++)
            {
                EmberTier tier = _tierCatalog[i];
                _glowByTier[i] = tier != null ? tier.GlowValue : 0;
                _heatByTier[i] = tier != null ? tier.HeatValue : 0;
            }
        }
    }
}
