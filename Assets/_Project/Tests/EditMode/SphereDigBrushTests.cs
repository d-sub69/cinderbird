using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SphereDigBrushTests
    {
        private const int Cells = 24;
        private const float CellSize = 0.25f;

        private static Vector3 GridCenter => Vector3.one * (Cells * CellSize * 0.5f);

        [Test]
        public void Dig_InAir_RemovesNothing()
        {
            VoxelGrid grid = new VoxelGrid(Cells, Cells, Cells, CellSize, Vector3.zero);

            DigResult result = SphereDigBrush.Dig(grid, GridCenter, 1f);

            Assert.That(result.RemovedVolume, Is.EqualTo(0f));
            Assert.That(result.RemovedAnyAsh, Is.False);
        }

        [Test]
        public void Dig_InSolidAsh_RemovesAboutTheSphereVolume()
        {
            VoxelGrid grid = TerrainTestGrids.SolidBlock(Cells, CellSize);
            const float radius = 1f;
            // Sphere volume plus the known curvature bias of the one-cell ramp (see VoxelGrid.Fill).
            float expected = 4f / 3f * Mathf.PI * radius * (radius * radius + CellSize * CellSize);

            DigResult result = SphereDigBrush.Dig(grid, GridCenter, radius);

            Assert.That(result.RemovedVolume, Is.EqualTo(expected).Within(expected * 0.02f));
        }

        [Test]
        public void Dig_SameSpotTwice_SecondDigRemovesNothing()
        {
            VoxelGrid grid = TerrainTestGrids.SolidBlock(Cells, CellSize);
            SphereDigBrush.Dig(grid, GridCenter, 1f);

            DigResult second = SphereDigBrush.Dig(grid, GridCenter, 1f);

            Assert.That(second.RemovedVolume, Is.EqualTo(0f));
        }

        [Test]
        public void Dig_RemovedVolume_MatchesDropInTotal()
        {
            VoxelGrid grid = TerrainTestGrids.SolidBlock(Cells, CellSize);
            float before = grid.TotalAshVolume();

            float removed = 0f;
            removed += SphereDigBrush.Dig(grid, GridCenter, 0.8f).RemovedVolume;
            removed += SphereDigBrush.Dig(grid, GridCenter + new Vector3(0.5f, 0.2f, 0f), 0.6f).RemovedVolume;

            // Totals are ~190 m³ stored as float, so allow float rounding at that magnitude (~1 mL).
            Assert.That(before - grid.TotalAshVolume(), Is.EqualTo(removed).Within(1e-3f));
        }

        [Test]
        public void Dig_OverlappingEdge_KeepsBoundaryAir()
        {
            VoxelGrid grid = TerrainTestGrids.SolidBlock(Cells, CellSize);

            SphereDigBrush.Dig(grid, Vector3.zero, 1.5f);

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
        public void Dig_FarOutsideGrid_RemovesNothingAndDoesNotThrow()
        {
            VoxelGrid grid = TerrainTestGrids.SolidBlock(Cells, CellSize);

            DigResult result = SphereDigBrush.Dig(grid, new Vector3(100f, -50f, 100f), 1f);

            Assert.That(result.RemovedVolume, Is.EqualTo(0f));
        }

        [Test]
        public void Dig_ZeroRadius_RemovesNothing()
        {
            VoxelGrid grid = TerrainTestGrids.SolidBlock(Cells, CellSize);

            DigResult result = SphereDigBrush.Dig(grid, GridCenter, 0f);

            Assert.That(result.RemovedVolume, Is.EqualTo(0f));
        }
    }
}
