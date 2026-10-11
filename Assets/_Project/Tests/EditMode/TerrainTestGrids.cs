using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    /// <summary>Builders for grids with known shapes, shared by the terrain tests.</summary>
    internal static class TerrainTestGrids
    {
        /// <summary>Grid whose interior is solid ash; boundary samples stay air.</summary>
        public static VoxelGrid SolidBlock(int cells, float cellSize)
        {
            VoxelGrid grid = new VoxelGrid(cells, cells, cells, cellSize, Vector3.zero);
            for (int z = 0; z < grid.SamplesZ; z++)
            {
                for (int y = 0; y < grid.SamplesY; y++)
                {
                    for (int x = 0; x < grid.SamplesX; x++)
                    {
                        if (!grid.IsBoundary(x, y, z))
                        {
                            grid.SetDensity(x, y, z, VoxelGrid.Ash);
                        }
                    }
                }
            }

            return grid;
        }

        /// <summary>Ball of ash with a one-cell signed-distance ramp, centered in the grid.</summary>
        public static VoxelGrid Ball(int cells, float cellSize, float radius, out Vector3 center)
        {
            VoxelGrid grid = new VoxelGrid(cells, cells, cells, cellSize, Vector3.zero);
            center = Vector3.one * (cells * cellSize * 0.5f);

            for (int z = 0; z < grid.SamplesZ; z++)
            {
                for (int y = 0; y < grid.SamplesY; y++)
                {
                    for (int x = 0; x < grid.SamplesX; x++)
                    {
                        float distance = (grid.SamplePosition(x, y, z) - center).magnitude;
                        grid.SetDensity(x, y, z, (radius - distance) / cellSize);
                    }
                }
            }

            return grid;
        }
    }
}
