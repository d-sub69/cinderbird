using System;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Density field sampled at cell corners. Density is in [-1, 1]: above 0 is ash, below is air,
    /// and the surface is where it crosses 0. Stored as one flat array so meshing can later move to Burst.
    /// </summary>
    public sealed class VoxelGrid
    {
        public const float Air = -1f;
        public const float Ash = 1f;

        private readonly float[] _densities;

        public VoxelGrid(int cellsX, int cellsY, int cellsZ, float cellSize, Vector3 origin)
        {
            if (cellsX < 1 || cellsY < 1 || cellsZ < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsX), "Grid needs at least one cell per axis.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            }

            CellsX = cellsX;
            CellsY = cellsY;
            CellsZ = cellsZ;
            CellSize = cellSize;
            Origin = origin;

            _densities = new float[SamplesX * SamplesY * SamplesZ];
            Array.Fill(_densities, Air);
        }

        public int CellsX { get; }
        public int CellsY { get; }
        public int CellsZ { get; }
        public int SamplesX => CellsX + 1;
        public int SamplesY => CellsY + 1;
        public int SamplesZ => CellsZ + 1;
        public float CellSize { get; }
        public float CellVolume => CellSize * CellSize * CellSize;

        /// <summary>Local-space position of sample (0, 0, 0).</summary>
        public Vector3 Origin { get; }

        /// <summary>Raw samples, indexed with <see cref="Index"/>. Exposed so hot loops avoid per-sample calls.</summary>
        public float[] Densities => _densities;

        /// <summary>
        /// Share of a sample's cell volume that counts as ash. Linear in density, so a one-cell
        /// signed-distance ramp sums to the true volume for flat surfaces. Curved surfaces read slightly
        /// high (a sphere of radius r by about 4/3·π·r·cellSize²); the bias is consistent, so totals
        /// still balance and gameplay amounts are unaffected.
        /// </summary>
        public static float Fill(float density)
        {
            return Mathf.Clamp01(density * 0.5f + 0.5f);
        }

        public int Index(int x, int y, int z)
        {
            return x + SamplesX * (y + SamplesY * z);
        }

        public float GetDensity(int x, int y, int z)
        {
            return _densities[Index(x, y, z)];
        }

        public void SetDensity(int x, int y, int z, float density)
        {
            _densities[Index(x, y, z)] = Mathf.Clamp(density, Air, Ash);
        }

        public Vector3 SamplePosition(int x, int y, int z)
        {
            return Origin + new Vector3(x, y, z) * CellSize;
        }

        /// <summary>
        /// Boundary samples are kept as air so the mesh is always closed at the grid edges.
        /// </summary>
        public bool IsBoundary(int x, int y, int z)
        {
            return x == 0 || y == 0 || z == 0 || x == SamplesX - 1 || y == SamplesY - 1 || z == SamplesZ - 1;
        }

        /// <summary>Total ash volume in cubic meters. Walks every sample, so not for per-frame use.</summary>
        public float TotalAshVolume()
        {
            // Double accumulator: summing tens of thousands of floats otherwise drifts by ~1e-4 m³.
            double fill = 0d;
            for (int i = 0; i < _densities.Length; i++)
            {
                fill += Fill(_densities[i]);
            }

            return (float)(fill * CellVolume);
        }
    }
}
