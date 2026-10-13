using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Owns the sieve's <see cref="SieveContents"/> so other components can reach it through the Inspector,
    /// and applies the ember capacity from the settings.
    /// </summary>
    public sealed class Sieve : MonoBehaviour, IEmberHolder
    {
        [Tooltip("Optional: supplies the ember capacity. Without it the sieve holds any number of embers.")]
        [SerializeField] private SieveSettings _settings;

        public SieveContents Contents { get; } = new SieveContents();

        public IEmberSource Source => Contents;

        private void Awake()
        {
            if (_settings != null)
            {
                Contents.EmberCapacity = _settings.EmberCapacity;
            }
        }
    }
}
