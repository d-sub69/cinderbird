using System;
using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Nest
{
    /// <summary>
    /// Thin adapter: tracks whether the player is close enough, and on Interact pours the basket in
    /// through <see cref="NestFeeding"/>. Also works out the Heat goal from the pile at start.
    /// </summary>
    public sealed class NestController : MonoBehaviour
    {
        [SerializeField] private NestSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [SerializeField] private Basket _basket;
        [SerializeField] private Wallet _wallet;
        [Tooltip("The player's root transform, for the range check.")]
        [SerializeField] private Transform _player;
        [Tooltip("The pile whose buried embers define the total Heat to collect.")]
        [SerializeField] private VoxelChunk _pile;
        [SerializeField] private InputActionReference _interactAction;

        private readonly List<Ember> _fedEmbers = new List<Ember>();
        private int[] _glowByTier;
        private int[] _heatByTier;

        /// <summary>Fired after a feeding with what it earned and the embers that went in.</summary>
        public event Action<FeedResult, IReadOnlyList<Ember>> Fed;

        public event Action<bool> InRangeChanged;

        public bool InRange { get; private set; }

        /// <summary>Total Heat of every ember in the pile at start; 0 until Start has run.</summary>
        public int HeatGoal { get; private set; }

        public float Progress => HeatGoal > 0 ? Mathf.Clamp01((float)_wallet.Currencies.Heat / HeatGoal) : 0f;

        public int HeatOf(int tierIndex)
        {
            return tierIndex >= 0 && tierIndex < _heatByTier.Length ? _heatByTier[tierIndex] : 0;
        }

        private void Awake()
        {
            if (_settings == null || _tierCatalog == null || _basket == null || _wallet == null ||
                _player == null || _pile == null || _interactAction == null)
            {
                Debug.LogError($"{nameof(NestController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            CacheTierValues();
        }

        private void OnEnable()
        {
            if (_interactAction != null)
            {
                _interactAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.action.Disable();
            }
        }

        // Start, not Awake: the pile spawns its embers in its own Awake.
        private void Start()
        {
            HeatGoal = EmberValuation.Evaluate(_pile.Embers, _glowByTier, _heatByTier).Heat;
        }

        private void Update()
        {
            UpdateRange();

            if (!InRange || Cursor.lockState != CursorLockMode.Locked || !_interactAction.action.WasPressedThisFrame())
            {
                return;
            }

            FeedResult result = NestFeeding.Feed(_basket.Contents, _wallet.Currencies, _glowByTier, _heatByTier, _fedEmbers);
            if (result.EmberCount > 0)
            {
                Fed?.Invoke(result, _fedEmbers);
            }
        }

        private void UpdateRange()
        {
            // Horizontal distance, so standing on a slope or jumping doesn't drop you out of range.
            Vector3 offset = _player.position - transform.position;
            offset.y = 0f;
            bool inRange = offset.sqrMagnitude <= _settings.PourRadius * _settings.PourRadius;
            if (inRange == InRange)
            {
                return;
            }

            InRange = inRange;
            InRangeChanged?.Invoke(inRange);
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

        private void OnDrawGizmosSelected()
        {
            if (_settings == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _settings.PourRadius);
        }
    }
}
