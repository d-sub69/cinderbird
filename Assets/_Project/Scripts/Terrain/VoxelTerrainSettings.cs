using UnityEngine;

namespace Cinderbird.Terrain
{
    [CreateAssetMenu(fileName = "VoxelTerrainSettings", menuName = "Cinderbird/Voxel Terrain Settings")]
    public sealed class VoxelTerrainSettings : ScriptableObject
    {
        [Header("Grid")]
        [SerializeField, Min(2)] private int _cellsX = 32;
        [SerializeField, Min(2)] private int _cellsY = 16;
        [SerializeField, Min(2)] private int _cellsZ = 32;
        [Tooltip("Edge length of one voxel cell in meters.")]
        [SerializeField, Min(0.01f)] private float _cellSize = 0.25f;

        [Header("Pile shape")]
        [SerializeField, Min(0.1f)] private float _pileRadius = 3.5f;
        [SerializeField, Min(0.1f)] private float _pileHeight = 2.5f;
        [SerializeField, Min(0f)] private float _noiseAmplitude = 0.3f;
        [SerializeField, Min(0f)] private float _noiseScale = 0.6f;
        [SerializeField] private int _noiseSeed = 1;

        public int CellsX => _cellsX;
        public int CellsY => _cellsY;
        public int CellsZ => _cellsZ;
        public float CellSize => _cellSize;

        public AshPileShape PileShape => new AshPileShape(_pileRadius, _pileHeight, _noiseAmplitude, _noiseScale, _noiseSeed);

        /// <summary>
        /// Grid origin that centers the grid on the object in x/z and puts one air layer below y = 0,
        /// so the mound's base sits on the floor and its closing bottom face is hidden underneath.
        /// </summary>
        public Vector3 GridOrigin => new Vector3(-_cellsX * _cellSize * 0.5f, -_cellSize, -_cellsZ * _cellSize * 0.5f);

        public Vector3 GridSize => new Vector3(_cellsX, _cellsY, _cellsZ) * _cellSize;
    }
}
