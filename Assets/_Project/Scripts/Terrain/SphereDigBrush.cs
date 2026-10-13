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
            return Dig(field, center, radius, null);
        }

        /// <param name="locks">Samples in a locked layer are left untouched; null digs everything.</param>
        public static DigResult Dig(IDensityField field, Vector3 center, float radius, LayerLocks locks)
        {
            return Dig(field, center, radius, locks, null);
        }

        /// <param name="removedByLayer">If given, cubic meters removed from each layer are added to it (index = layer).</param>
        public static DigResult Dig(IDensityField field, Vector3 center, float radius, LayerLocks locks, float[] removedByLayer)
        {
            return Carve(field, center, radius, locks, removedByLayer, true);
        }

        /// <summary>
        /// Reports exactly what <see cref="Dig(IDensityField, Vector3, float, LayerLocks, float[])"/> would remove, without
        /// changing anything; lets a scoop know its load before the hole is carved over a moment.
        /// </summary>
        public static DigResult Measure(IDensityField field, Vector3 center, float radius, LayerLocks locks, float[] removedByLayer)
        {
            return Carve(field, center, radius, locks, removedByLayer, false);
        }

        private static DigResult Carve(IDensityField field, Vector3 center, float radius, LayerLocks locks, float[] removedByLayer, bool write)
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
            int blockedLayer = -1;
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

                        int layer = field.GetLayer(x, y, z);
                        // A sealed layer stays as a wall; remember it so the player can be told why.
                        if (locks != null && !locks.IsUnlocked(layer))
                        {
                            blockedLayer = blockedLayer < 0 ? layer : blockedLayer;
                            continue;
                        }

                        // Each sample is visited once even where chunks share it, so nothing is counted twice.
                        float removed = VoxelGrid.Fill(current) - VoxelGrid.Fill(carved);
                        removedFill += removed;
                        if (removedByLayer != null && layer < removedByLayer.Length)
                        {
                            removedByLayer[layer] += removed * field.CellVolume;
                        }

                        if (write)
                        {
                            field.SetDensity(x, y, z, carved);
                        }
                    }
                }
            }

            return new DigResult(removedFill * field.CellVolume, 0, blockedLayer);
        }
    }
}
