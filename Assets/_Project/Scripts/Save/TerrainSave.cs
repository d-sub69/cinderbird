using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>The mountain's saved state: which generator settings it belongs to, open layers, changed chunks.</summary>
    [Serializable]
    public sealed class TerrainSave
    {
        [SerializeField] private string _fingerprint = string.Empty;
        [SerializeField] private int _deepestUnlockedLayer;
        [SerializeField] private int _chunkCount;
        // Changed chunks and their embers, packed by TerrainSnapshot (gzip, base64): far smaller than JSON arrays.
        [SerializeField] private string _chunks = string.Empty;

        public string Fingerprint { get => _fingerprint; set => _fingerprint = value; }
        public int DeepestUnlockedLayer { get => _deepestUnlockedLayer; set => _deepestUnlockedLayer = value; }

        /// <summary>How many chunks are packed in <see cref="Chunks"/>; informational, for reading save files.</summary>
        public int ChunkCount { get => _chunkCount; set => _chunkCount = value; }

        public string Chunks { get => _chunks; set => _chunks = value; }
    }
}
