using Cinderbird.Economy;
using Cinderbird.Sifting;
using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Thin adapter: gives the sieve in hand the size of the sieve upgrade's current level: the scoop radius, the
    /// ember capacity and the look. Applied at start and whenever the level changes (bought, or loaded from a save).
    /// Lives on an always-active object: the sieve itself is inactive while put away.
    /// </summary>
    public sealed class SieveUpgradeEffect : MonoBehaviour
    {
        [SerializeField] private Upgrades _upgrades;
        [SerializeField] private SieveUpgrade _upgrade;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private ScoopInteractor _scoop;
        [SerializeField] private SieveView _view;
        [SerializeField] private SieveSettings _settings;
        [Tooltip("Optional: a bigger sieve shakes out faster, so a full sieve of any size takes the same time.")]
        [SerializeField] private SieveShakeController _shake;

        private void Awake()
        {
            if (_upgrades == null || _upgrade == null || _sieve == null || _scoop == null || _view == null || _settings == null)
            {
                Debug.LogError($"{nameof(SieveUpgradeEffect)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _upgrades.Levels.Changed += OnLevelChanged;
        }

        private void OnDisable()
        {
            _upgrades.Levels.Changed -= OnLevelChanged;
        }

        private void Start()
        {
            Apply();
        }

        private void OnLevelChanged(string id)
        {
            if (id == _upgrade.Id)
            {
                Apply();
            }
        }

        private void Apply()
        {
            SieveTier tier = _upgrade.TierAt(_upgrades.LevelOf(_upgrade));
            _scoop.SetScoopRadius(tier.ScoopRadius);
            _sieve.SetCapacity(tier.EmberCapacity);
            if (_shake != null)
            {
                _shake.SetRateScale(SieveShakeScale.For(_upgrade.TierAt(0).ScoopRadius, tier.ScoopRadius));
            }

            // Room to show a full sieve, and a little over: a scoop can bring more than the capacity.
            _view.SetSize(tier.VisualScale, Mathf.Max(tier.EmberCapacity, _settings.MaxVisibleEmbers));
        }
    }
}
