using System;
using Cinderbird.Core;
using Cinderbird.Economy;
using Cinderbird.Nest;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.UI
{
    /// <summary>
    /// Testing aid (F7): sets a fresh game to the end of the vertical slice in one press, so the performance test reaches
    /// everything that costs frame time quickly and the same way every run: the nest at Heat level 3 (the phoenix whole,
    /// the valley thawing, the Warm Wind unlocked), Glow to buy upgrades with, and Sally hired. Only where
    /// <see cref="DevTools.Enabled"/> (Editor, development and Performance Test builds).
    /// </summary>
    public sealed class PerfScenarioKey : MonoBehaviour
    {
        [SerializeField] private InputActionReference _action;
        [SerializeField] private Wallet _wallet;
        [SerializeField] private Upgrades _upgrades;
        [SerializeField] private NestHeat _heat;
        [Tooltip("The Heat level it brings the nest to.")]
        [SerializeField, Min(0)] private int _heatLevel = 3;
        [Tooltip("Glow it adds, for buying upgrades during the test.")]
        [SerializeField, Min(0)] private int _glow = 5000;
        [Tooltip("Upgrades it buys a level of if none is bought yet (e.g. the helper, so she works during the test).")]
        [SerializeField] private UpgradeDefinition[] _upgradesToOwn = new UpgradeDefinition[0];

        /// <summary>A short message for the HUD saying what was set.</summary>
        public event Action<string> Notice;

        private void Awake()
        {
            if (!DevTools.Enabled || _action == null || _wallet == null || _upgrades == null || _heat == null)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_action != null)
            {
                _action.action.Enable();
            }
        }

        private void Update()
        {
            if (!_action.action.WasPressedThisFrame())
            {
                return;
            }

            // Added rather than restored, so the nest reacts as in play: the phoenix grows, the valley thaws, powers come.
            int missingHeat = Mathf.Max(0, _heat.Levels.HeatFor(_heatLevel) - _wallet.Currencies.Heat);
            _wallet.Currencies.Add(_glow, missingHeat);
            foreach (UpgradeDefinition upgrade in _upgradesToOwn)
            {
                if (upgrade != null && _upgrades.LevelOf(upgrade) == 0)
                {
                    _upgrades.Levels.SetLevel(upgrade.Id, 1);
                }
            }

            Notice?.Invoke($"Performance scenario: Heat level {_heatLevel}, +{_glow} Glow, helpers hired");
        }
    }
}
