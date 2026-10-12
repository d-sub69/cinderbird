using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class AshPileGeneratorTests
    {
        private const int CellsXZ = 32;
        private const int CellsY = 16;
        private const float CellSize = 0.25f;

        private static readonly AshPileShape Shape = new AshPileShape(
            radius: 3.5f, height: 2.5f, noiseAmplitude: 0.3f, noiseScale: 0.6f, noiseSeed: 1);

        private static VoxelGrid CreateCenteredGrid()
        {
            // Same layout VoxelTerrain uses: centered in x/z, one air layer below the floor.
            Vector3 origin = new Vector3(-CellsXZ * CellSize * 0.5f, -CellSize, -CellsXZ * CellSize * 0.5f);
            return new VoxelGrid(CellsXZ, CellsY, CellsXZ, CellSize, origin);
        }

        [Test]
        public void Generate_BoundaryStaysAir()
        {
            VoxelGrid grid = CreateCenteredGrid();

            AshPileGenerator.Generate(grid, Shape);

            for (int z = 0; z < grid.SamplesZ; z++)
            {
                for (int y = 0; y < grid.SamplesY; y++)
                {
                    for (int x = 0; x < grid.SamplesX; x++)
                    {
                        if (grid.IsBoundary(x, y, z))
                        {
                            Assert.That(grid.GetDensity(x, y, z), Is.EqualTo(VoxelGrid.Air));
                        }
                    }
                }
            }
        }

        [Test]
        public void Generate_CenterNearFloor_IsAsh()
        {
            VoxelGrid grid = CreateCenteredGrid();

            AshPileGenerator.Generate(grid, Shape);

            Assert.That(grid.GetDensity(CellsXZ / 2, 2, CellsXZ / 2), Is.GreaterThan(0f));
        }

        [Test]
        public void Generate_OutsideRadius_IsAir()
        {
            VoxelGrid grid = CreateCenteredGrid();

            AshPileGenerator.Generate(grid, Shape);

            // Corner column is ~5.3 m from the center, outside the 3.5 m radius.
            Assert.That(grid.GetDensity(1, 1, 1), Is.EqualTo(VoxelGrid.Air));
        }

        [Test]
        public void Generate_VolumeIsPlausible()
        {
            VoxelGrid grid = CreateCenteredGrid();

            AshPileGenerator.Generate(grid, Shape);

            float volume = grid.TotalAshVolume();
            float boundingCylinder = Mathf.PI * Shape.Radius * Shape.Radius * (Shape.Height + Shape.NoiseAmplitude);
            Assert.That(volume, Is.GreaterThan(boundingCylinder * 0.1f).And.LessThan(boundingCylinder));
        }

        [Test]
        public void Generate_SameSeed_IsDeterministic()
        {
            VoxelGrid first = CreateCenteredGrid();
            VoxelGrid second = CreateCenteredGrid();

            AshPileGenerator.Generate(first, Shape);
            AshPileGenerator.Generate(second, Shape);

            Assert.That(second.Densities, Is.EqualTo(first.Densities));
        }
    }
}
