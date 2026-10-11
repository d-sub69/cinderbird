using System;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class VoxelGridTests
    {
        [Test]
        public void Constructor_SampleCountsAreCellsPlusOne()
        {
            VoxelGrid grid = new VoxelGrid(4, 5, 6, 0.25f, Vector3.zero);

            Assert.That(grid.SamplesX, Is.EqualTo(5));
            Assert.That(grid.SamplesY, Is.EqualTo(6));
            Assert.That(grid.SamplesZ, Is.EqualTo(7));
            Assert.That(grid.Densities.Length, Is.EqualTo(5 * 6 * 7));
        }

        [Test]
        public void Constructor_InvalidSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelGrid(0, 1, 1, 1f, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelGrid(1, 1, 1, 0f, Vector3.zero));
        }

        [Test]
        public void NewGrid_IsAllAir_WithZeroVolume()
        {
            VoxelGrid grid = new VoxelGrid(3, 3, 3, 1f, Vector3.zero);

            Assert.That(grid.Densities, Has.All.EqualTo(VoxelGrid.Air));
            Assert.That(grid.TotalAshVolume(), Is.EqualTo(0f));
        }

        [Test]
        public void Index_IsUniqueAndCoversArray()
        {
            VoxelGrid grid = new VoxelGrid(3, 4, 5, 1f, Vector3.zero);
            bool[] seen = new bool[grid.Densities.Length];

            for (int z = 0; z < grid.SamplesZ; z++)
            {
                for (int y = 0; y < grid.SamplesY; y++)
                {
                    for (int x = 0; x < grid.SamplesX; x++)
                    {
                        int index = grid.Index(x, y, z);
                        Assert.That(seen[index], Is.False, $"Index {index} reused at ({x},{y},{z})");
                        seen[index] = true;
                    }
                }
            }

            Assert.That(seen, Has.All.True);
        }

        [Test]
        public void SamplePosition_UsesOriginAndCellSize()
        {
            VoxelGrid grid = new VoxelGrid(4, 4, 4, 0.5f, new Vector3(-1f, 2f, 3f));

            Assert.That(grid.SamplePosition(2, 1, 4), Is.EqualTo(new Vector3(0f, 2.5f, 5f)));
        }

        [Test]
        public void SetDensity_ClampsToRange()
        {
            VoxelGrid grid = new VoxelGrid(2, 2, 2, 1f, Vector3.zero);

            grid.SetDensity(1, 1, 1, 5f);
            Assert.That(grid.GetDensity(1, 1, 1), Is.EqualTo(VoxelGrid.Ash));

            grid.SetDensity(1, 1, 1, -5f);
            Assert.That(grid.GetDensity(1, 1, 1), Is.EqualTo(VoxelGrid.Air));
        }

        [Test]
        public void IsBoundary_TrueOnlyOnOuterSamples()
        {
            VoxelGrid grid = new VoxelGrid(2, 2, 2, 1f, Vector3.zero);

            Assert.That(grid.IsBoundary(1, 1, 1), Is.False);
            Assert.That(grid.IsBoundary(0, 1, 1), Is.True);
            Assert.That(grid.IsBoundary(1, 2, 1), Is.True);
            Assert.That(grid.IsBoundary(1, 1, 2), Is.True);
        }

        [Test]
        public void SampleDensity_AtSamplePoint_ReturnsThatSample()
        {
            VoxelGrid grid = new VoxelGrid(4, 4, 4, 0.5f, new Vector3(1f, 0f, 0f));
            grid.SetDensity(2, 1, 3, 0.7f);

            Assert.That(grid.SampleDensity(grid.SamplePosition(2, 1, 3)), Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void SampleDensity_BetweenSamples_Blends()
        {
            VoxelGrid grid = new VoxelGrid(4, 4, 4, 1f, Vector3.zero);
            grid.SetDensity(1, 1, 1, 1f);
            grid.SetDensity(2, 1, 1, 0f);

            Assert.That(grid.SampleDensity(new Vector3(1.25f, 1f, 1f)), Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void SampleDensity_OutsideGrid_ClampsAndDoesNotThrow()
        {
            VoxelGrid grid = new VoxelGrid(4, 4, 4, 1f, Vector3.zero);

            Assert.That(grid.SampleDensity(new Vector3(-10f, 50f, 4f)), Is.EqualTo(VoxelGrid.Air));
        }

        [Test]
        public void TotalAshVolume_SolidBlock_CountsInteriorSamples()
        {
            VoxelGrid grid = TerrainTestGrids.SolidBlock(4, 0.5f);
            int interiorSamples = 3 * 3 * 3;

            Assert.That(grid.TotalAshVolume(), Is.EqualTo(interiorSamples * grid.CellVolume).Within(1e-5f));
        }
    }
}
