using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>The upgrades bought in the shop: every one above level 0, by id.</summary>
    [Serializable]
    public sealed class UpgradesSave
    {
        [SerializeField] private List<UpgradeLevelSave> _levels = new List<UpgradeLevelSave>();

        public List<UpgradeLevelSave> Levels => _levels;
    }
}
