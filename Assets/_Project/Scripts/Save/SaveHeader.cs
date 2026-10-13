using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>Just the version of a save, read first so an old layout can be upgraded before the full parse.</summary>
    [Serializable]
    public sealed class SaveHeader
    {
        // Same key as SaveFile._version.
        [SerializeField] private int _version;

        public int Version => _version;
    }
}
