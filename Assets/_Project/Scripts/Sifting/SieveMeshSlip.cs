using System;
using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;
using Random = System.Random;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Thin adapter for the sieve's mesh: when a load with ash arrives, plans which small embers slip through
    /// (<see cref="MeshSlipPlan"/>, by the mesh upgrade's level), and as the ash is shaken out drops them straight down
    /// from under the sieve, where they lie loose on the ground or ash until picked up again. Lives on an
    /// always-active object, listening to the sieve's contents.
    /// </summary>
    public sealed class SieveMeshSlip : MonoBehaviour
    {
        [SerializeField] private Upgrades _upgrades;
        [SerializeField] private MeshUpgrade _upgrade;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private SieveSettings _settings;
        [Tooltip("The sieve floor (its local XZ plane); slipped embers fall from just under it.")]
        [SerializeField] private Transform _sieveFloor;

        private readonly MeshSlipPlan _plan = new MeshSlipPlan();
        private readonly Random _random = new Random();
        private readonly List<Ember> _due = new List<Ember>();
        private Func<int, bool> _canSlip;
        private bool _dropping;

        /// <summary>Fired with how many embers just slipped through the mesh (for player feedback).</summary>
        public event Action<int> Slipped;

        private void Awake()
        {
            if (_upgrades == null || _upgrade == null || _sieve == null || _tierCatalog == null || _pile == null ||
                _settings == null || _sieveFloor == null)
            {
                Debug.LogError($"{nameof(SieveMeshSlip)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            // Created once: a new delegate per load would be garbage.
            _canSlip = CanSlip;
        }

        private void OnEnable()
        {
            _sieve.Contents.LoadAdded += OnLoadAdded;
            _sieve.Contents.Changed += OnContentsChanged;
        }

        private void OnDisable()
        {
            _sieve.Contents.LoadAdded -= OnLoadAdded;
            _sieve.Contents.Changed -= OnContentsChanged;
        }

        private void OnLoadAdded(int first, int count)
        {
            SieveContents contents = _sieve.Contents;
            // A load without ash (a loose ember picked up, or test mode) isn't shaken, so nothing slips.
            if (!contents.HasAsh)
            {
                _plan.Clear();
                return;
            }

            float chance = _upgrade.SlipChanceAt(_upgrades.LevelOf(_upgrade));
            _plan.Plan(contents.Embers, first, count, _canSlip, chance, _random);
        }

        private void OnContentsChanged()
        {
            if (_dropping || _plan.Count == 0)
            {
                return;
            }

            SieveContents contents = _sieve.Contents;
            _due.Clear();
            _plan.TakeDue(contents.HasAsh ? contents.AshFraction : 0f, _due);
            if (_due.Count == 0)
            {
                return;
            }

            // Taking embers out changes the contents again; that change is ours.
            _dropping = true;
            int dropped = 0;
            try
            {
                foreach (Ember ember in _due)
                {
                    if (contents.RemoveEmber(ember))
                    {
                        _pile.DropLoose(ember, DropPoint());
                        dropped++;
                    }
                }
            }
            finally
            {
                _dropping = false;
            }

            if (dropped > 0)
            {
                Slipped?.Invoke(dropped);
            }
        }

        // Straight down from a random spot of the sieve floor, just under it.
        private Vector3 DropPoint()
        {
            float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)_random.NextDouble()) * _settings.Radius * 0.8f;
            Vector3 local = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            return _sieveFloor.TransformPoint(local) + Vector3.down * 0.05f;
        }

        private bool CanSlip(int tierIndex)
        {
            return tierIndex >= 0 && tierIndex < _tierCatalog.Count && _tierCatalog[tierIndex] != null &&
                   _tierCatalog[tierIndex].SlipsThroughMesh;
        }
    }
}
