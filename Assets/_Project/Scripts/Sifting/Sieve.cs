using Cinderbird.Core;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Owns the sieve's <see cref="SieveContents"/> so other components can reach it through the Inspector,
    /// and applies the ember capacity from the settings (unlimited while test mode says so).
    /// </summary>
    public sealed class Sieve : MonoBehaviour, IEmberHolder
    {
        [Tooltip("Optional: supplies the ember capacity. Without it the sieve holds any number of embers.")]
        [SerializeField] private SieveSettings _settings;
        [Tooltip("Optional: test mode can lift the ember capacity.")]
        [SerializeField] private TestModeSettings _testMode;

        public SieveContents Contents { get; } = new SieveContents();

        public IEmberSource Source => Contents;

        // Set by the sieve upgrade; until then the settings give the capacity.
        private int _capacityOverride;

        /// <summary>The ember capacity of the sieve in hand (an upgrade sets it); test mode can still lift it.</summary>
        public void SetCapacity(int capacity)
        {
            _capacityOverride = Mathf.Max(1, capacity);
            ApplyCapacity();
        }

        private void Awake()
        {
            ApplyCapacity();
        }

        // Every frame, so ticking test mode during Play takes effect at once.
        private void Update()
        {
            ApplyCapacity();
        }

        private void ApplyCapacity()
        {
            if (_testMode != null && _testMode.UnlimitedSieveEmbers)
            {
                Contents.EmberCapacity = int.MaxValue;
            }
            else if (_capacityOverride > 0)
            {
                Contents.EmberCapacity = _capacityOverride;
            }
            else if (_settings != null)
            {
                Contents.EmberCapacity = _settings.EmberCapacity;
            }
        }
    }
}
