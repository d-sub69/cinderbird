using UnityEngine;

namespace Cinderbird.Terrain
{
    [CreateAssetMenu(fileName = "VoxelTerrainSettings", menuName = "Cinderbird/Voxel Terrain Settings")]
    public sealed class VoxelTerrainSettings : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Cells along each edge of a chunk. Chunks are the unit of re-meshing after a dig.")]
        [SerializeField, Min(2)] private int _chunkCells = 32;
        [SerializeField, Min(1)] private int _chunksX = 6;
        [SerializeField, Min(1)] private int _chunksY = 3;
        [SerializeField, Min(1)] private int _chunksZ = 6;
        [Tooltip("Edge length of one voxel cell in meters.")]
        [SerializeField, Min(0.01f)] private float _cellSize = 0.25f;

        [Header("Mountain shape")]
        [SerializeField, Min(0.1f)] private float _pileRadius = 22f;
        [SerializeField, Min(0.1f)] private float _pileHeight = 16f;
        [SerializeField, Min(0f)] private float _noiseAmplitude = 1.5f;
        [SerializeField, Min(0f)] private float _noiseScale = 0.08f;
        [SerializeField] private int _noiseSeed = 1;

        public int ChunkCells => _chunkCells;
        public int ChunksX => _chunksX;
        public int ChunksY => _chunksY;
        public int ChunksZ => _chunksZ;
        public float CellSize => _cellSize;

        public AshPileShape PileShape => new AshPileShape(_pileRadius, _pileHeight, _noiseAmplitude, _noiseScale, _noiseSeed);

        public Vector3 WorldSize => new Vector3(_chunksX, _chunksY, _chunksZ) * (_chunkCells * _cellSize);

        /// <summary>
        /// World origin that centers the world on the object in x/z and puts one air layer below y = 0,
        /// so the mountain's base sits on the floor and its closing bottom face is hidden underneath.
        /// </summary>
        public Vector3 WorldOrigin
        {
            get
            {
                Vector3 size = WorldSize;
                return new Vector3(-size.x * 0.5f, -_cellSize, -size.z * 0.5f);
            }
        }
    }
}
