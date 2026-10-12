using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// A density field addressed by sample index: one chunk's <see cref="VoxelGrid"/> or the whole
    /// <see cref="VoxelWorld"/>. Lets digging, glimmers and generation work on either.
    /// </summary>
    public interface IDensityField
    {
        int SamplesX { get; }
        int SamplesY { get; }
        int SamplesZ { get; }
        float CellSize { get; }
        float CellVolume { get; }

        /// <summary>Local-space position of sample (0, 0, 0).</summary>
        Vector3 Origin { get; }

        float GetDensity(int x, int y, int z);

        /// <summary>Writes a sample (clamped to -1..1).</summary>
        void SetDensity(int x, int y, int z, float density);

        /// <summary>Outer-edge samples, which stay air so the surface is always closed.</summary>
        bool IsBoundary(int x, int y, int z);

        Vector3 SamplePosition(int x, int y, int z);

        /// <summary>Density at any local position, blended from the surrounding samples.</summary>
        float SampleDensity(Vector3 localPosition);
    }
}
