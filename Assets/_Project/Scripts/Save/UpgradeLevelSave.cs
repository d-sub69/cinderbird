using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>One upgrade's level, by its stable id.</summary>
    [Serializable]
    public sealed class UpgradeLevelSave
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private int _level;

        public UpgradeLevelSave()
        {
        }

        public UpgradeLevelSave(string id, int level)
        {
            _id = id;
            _level = level;
        }

        public string Id => _id;
        public int Level => _level;
    }
}
