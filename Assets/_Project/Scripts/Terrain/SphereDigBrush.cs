using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Carves a sphere out of a density field (one chunk or the whole world) and reports how much ash was removed.
    /// </summary>
    public static class SphereDigBrush
    {
        /// <param name="center">Field-local position (same space as <see cref="IDensityField.SamplePosition"/>).</param>
        public static DigResult Dig(IDensityField field, Vector3 center, float radius)
        {
            if (radius <= 0f)
            {
                return default;
            }

            float cellSize = field.CellSize;
            // One extra cell covers the soft edge of the carve ramp.
            float reach = radius + cellSize;
            Vector3 min = (center - Vector3.one * reach - field.Origin) / cellSize;
            Vector3 max = (center + Vector3.one * reach - field.Origin) / cellSize;

            // Boundary samples are skipped: they are always air and must stay that way.
            int x0 = Mathf.Max(1, Mathf.FloorToInt(min.x));
            int y0 = Mathf.Max(1, Mathf.FloorToInt(min.y));
            int z0 = Mathf.Max(1, Mathf.FloorToInt(min.z));
            int x1 = Mathf.Min(field.SamplesX - 2, Mathf.CeilToInt(max.x));
            int y1 = Mathf.Min(field.SamplesY - 2, Mathf.CeilToInt(max.y));
            int z1 = Mathf.Min(field.SamplesZ - 2, Mathf.CeilToInt(max.z));

            float removedFill = 0f;
            for (int z = z0; z <= z1; z++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float distance = (field.SamplePosition(x, y, z) - center).magnitude;
                        // Signed distance to the sphere in cells: negative inside. Taking the min with the
                        // existing density is a CSG subtract, so digging the same spot twice removes nothing.
                        float carved = Mathf.Clamp((distance - radius) / cellSize, VoxelGrid.Air, VoxelGrid.Ash);
                        float current = field.GetDensity(x, y, z);
                        if (carved >= current)
                        {
                            continue;
                        }

                        // Each sample is visited once even where chunks share it, so nothing is counted twice.
                        removedFill += VoxelGrid.Fill(current) - VoxelGrid.Fill(carved);
                        field.SetDensity(x, y, z, carved);
                    }
                }
            }

            return new DigResult(removedFill * field.CellVolume, 0);
        }
    }
}
