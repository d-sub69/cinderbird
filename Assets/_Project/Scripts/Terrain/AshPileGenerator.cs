using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Fills a density field (one grid or the whole world) with a noisy mound. The mound is centered on
    /// field-local (0, 0, 0), with the floor at local y = 0.
    /// </summary>
    public static class AshPileGenerator
    {
        // Keeps different seeds far apart in Perlin space; Perlin repeats at large integer offsets otherwise.
        private const float SeedSpacing = 37.17f;

        public static void Generate(IDensityField grid, AshPileShape shape)
        {
            Generate(grid, shape, null);
        }

        /// <param name="layout">Assigns each sample its ash layer by depth below the surface; null leaves layer 0.</param>
        public static void Generate(IDensityField grid, AshPileShape shape, LayerLayout layout)
        {
            float cellSize = grid.CellSize;
            float noiseOffset = shape.NoiseSeed * SeedSpacing;

            for (int z = 0; z < grid.SamplesZ; z++)
            {
                for (int x = 0; x < grid.SamplesX; x++)
                {
                    Vector3 column = grid.SamplePosition(x, 0, z);
                    float height = SurfaceHeight(column.x, column.z, shape, noiseOffset);

                    for (int y = 0; y < grid.SamplesY; y++)
                    {
                        float density = VoxelGrid.Air;
                        if (height > 0f && !grid.IsBoundary(x, y, z))
                        {
                            float sampleY = grid.SamplePosition(x, y, z).y;
                            // Height difference in cells gives a one-cell-wide ramp, which keeps
                            // the measured volume close to the real shape.
                            density = (height - sampleY) / cellSize;
                        }

                        grid.SetDensity(x, y, z, density);
                        if (layout != null)
                        {
                            grid.SetLayer(x, y, z, layout.LayerAt(grid.SamplePosition(x, y, z), height));
                        }
                    }
                }
            }
        }

        private static float SurfaceHeight(float x, float z, AshPileShape shape, float noiseOffset)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            float t = distance / shape.Radius;
            if (t >= 1f)
            {
                return 0f;
            }

            float bell = Mathf.SmoothStep(1f, 0f, t);
            float noise = Mathf.PerlinNoise(x * shape.NoiseScale + noiseOffset, z * shape.NoiseScale + noiseOffset);
            float bumps = (noise * 2f - 1f) * shape.NoiseAmplitude;

            // Scaling bumps by the bell keeps the rim smooth where the mound meets the floor.
            return bell * (shape.Height + bumps);
        }
    }
}
