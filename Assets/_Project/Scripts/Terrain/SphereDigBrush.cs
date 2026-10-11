using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Carves a sphere out of a <see cref="VoxelGrid"/> and reports how much ash was removed.
    /// </summary>
    public static class SphereDigBrush
    {
        /// <param name="center">Grid-local position (same space as <see cref="VoxelGrid.SamplePosition"/>).</param>
        public static DigResult Dig(VoxelGrid grid, Vector3 center, float radius)
        {
            if (radius <= 0f)
            {
                return default;
            }

            float cellSize = grid.CellSize;
            // One extra cell covers the soft edge of the carve ramp.
            float reach = radius + cellSize;
            Vector3 min = (center - Vector3.one * reach - grid.Origin) / cellSize;
            Vector3 max = (center + Vector3.one * reach - grid.Origin) / cellSize;

            // Boundary samples are skipped: they are always air and must stay that way.
            int x0 = Mathf.Max(1, Mathf.FloorToInt(min.x));
            int y0 = Mathf.Max(1, Mathf.FloorToInt(min.y));
            int z0 = Mathf.Max(1, Mathf.FloorToInt(min.z));
            int x1 = Mathf.Min(grid.SamplesX - 2, Mathf.CeilToInt(max.x));
            int y1 = Mathf.Min(grid.SamplesY - 2, Mathf.CeilToInt(max.y));
            int z1 = Mathf.Min(grid.SamplesZ - 2, Mathf.CeilToInt(max.z));

            float[] densities = grid.Densities;
            float removedFill = 0f;

            for (int z = z0; z <= z1; z++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float distance = (grid.SamplePosition(x, y, z) - center).magnitude;
                        // Signed distance to the sphere in cells: negative inside. Taking the min with the
                        // existing density is a CSG subtract, so digging the same spot twice removes nothing.
                        float carved = Mathf.Clamp((distance - radius) / cellSize, VoxelGrid.Air, VoxelGrid.Ash);

                        int index = grid.Index(x, y, z);
                        float current = densities[index];
                        if (carved >= current)
                        {
                            continue;
                        }

                        removedFill += VoxelGrid.Fill(current) - VoxelGrid.Fill(carved);
                        densities[index] = carved;
                    }
                }
            }

            return new DigResult(removedFill * grid.CellVolume);
        }
    }
}
