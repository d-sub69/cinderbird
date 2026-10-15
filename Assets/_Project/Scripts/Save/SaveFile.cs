using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>
    /// Everything a save holds, as plain serializable data (written with Unity's JsonUtility). Bump
    /// <see cref="CurrentVersion"/> whenever the layout changes and teach <see cref="SaveMigrator"/> the upgrade.
    /// </summary>
    [Serializable]
    public sealed class SaveFile
    {
        // 2: loose embers in the terrain save. 3: upgrades bought in the shop. 4: offerings found.
        public const int CurrentVersion = 4;

        // Field names are the JSON keys: renaming one breaks older saves unless the migrator handles it.
        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private string _savedAtUtc = string.Empty;
        [SerializeField] private TerrainSave _terrain = new TerrainSave();
        [SerializeField] private CurrencySave _currencies = new CurrencySave();
        [SerializeField] private PlayerSave _player = new PlayerSave();
        [SerializeField] private ToolsSave _tools = new ToolsSave();
        [SerializeField] private SieveSave _sieve = new SieveSave();
        [SerializeField] private BasketSave _basket = new BasketSave();
        [SerializeField] private UpgradesSave _upgrades = new UpgradesSave();
        [SerializeField] private OfferingsSave _offerings = new OfferingsSave();

        public int Version { get => _version; set => _version = value; }

        /// <summary>When it was written, ISO 8601 in UTC; for display and debugging only.</summary>
        public string SavedAtUtc { get => _savedAtUtc; set => _savedAtUtc = value; }

        public TerrainSave Terrain => _terrain;
        public CurrencySave Currencies => _currencies;
        public PlayerSave Player => _player;
        public ToolsSave Tools => _tools;
        public SieveSave Sieve => _sieve;
        public BasketSave Basket => _basket;
        public UpgradesSave Upgrades => _upgrades;
        public OfferingsSave Offerings => _offerings;
    }
}
