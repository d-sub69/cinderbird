using System;
using System.Collections.Generic;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class VoxelWorldTests
    {
        private const float CellSize = 0.25f;

        private static void Fill(IDensityField field, Func<Vector3, float> densityAt)
        {
            for (int z = 0; z < field.SamplesZ; z++)
            {
                for (int y = 0; y < field.SamplesY; y++)
                {
                    for (int x = 0; x < field.SamplesX; x++)
                    {
                        field.SetDensity(x, y, z, densityAt(field.SamplePosition(x, y, z)));
                    }
                }
            }
        }

        private static Func<Vector3, float> Ball(Vector3 center, float radius)
        {
            return position => (radius - (position - center).magnitude) / CellSize;
        }

        private static void FillInteriorWithAsh(IDensityField field)
        {
            for (int z = 0; z < field.SamplesZ; z++)
            {
                for (int y = 0; y < field.SamplesY; y++)
                {
                    for (int x = 0; x < field.SamplesX; x++)
                    {
                        field.SetDensity(x, y, z, field.IsBoundary(x, y, z) ? VoxelGrid.Air : VoxelGrid.Ash);
                    }
                }
            }
        }

        [Test]
        public void SampleCounts_ShareBordersBetweenChunks()
        {
            VoxelWorld world = new VoxelWorld(4, 3, 2, 1, CellSize, Vector3.zero);

            Assert.That(world.SamplesX, Is.EqualTo(13));
            Assert.That(world.SamplesY, Is.EqualTo(9));
            Assert.That(world.SamplesZ, Is.EqualTo(5));
            Assert.That(world.ChunkCount, Is.EqualTo(6));
        }

        [Test]
        public void SetDensity_OnSeam_UpdatesBothChunks()
        {
            VoxelWorld world = new VoxelWorld(4, 2, 1, 1, CellSize, Vector3.zero);

            world.SetDensity(4, 2, 2, 0.5f);

            Assert.That(world.GetChunk(0).GetDensity(4, 2, 2), Is.EqualTo(0.5f));
            Assert.That(world.GetChunk(1).GetDensity(0, 2, 2), Is.EqualTo(0.5f));
        }

        [Test]
        public void SetDensity_OnCornerOfEightChunks_UpdatesAllEight()
        {
            VoxelWorld world = new VoxelWorld(4, 2, 2, 2, CellSize, Vector3.zero);

            world.SetDensity(4, 4, 4, 0.25f);

            for (int i = 0; i < world.ChunkCount; i++)
            {
                Vector3Int local = new Vector3Int(4, 4, 4) - world.ChunkSampleOffset(i);
                Assert.That(world.GetChunk(i).GetDensity(local.x, local.y, local.z), Is.EqualTo(0.25f), $"Chunk {i}");
            }
        }

        [Test]
        public void EveryChunkCopy_MatchesTheWorldValue()
        {
            VoxelWorld world = new VoxelWorld(4, 2, 2, 2, CellSize, Vector3.zero);
            Fill(world, position => Mathf.Sin(position.x * 3f) * Mathf.Cos(position.z * 2f) + position.y * 0.1f);

            for (int i = 0; i < world.ChunkCount; i++)
            {
                VoxelGrid chunk = world.GetChunk(i);
                Vector3Int offset = world.ChunkSampleOffset(i);
                for (int z = 0; z < chunk.SamplesZ; z++)
                {
                    for (int y = 0; y < chunk.SamplesY; y++)
                    {
                        for (int x = 0; x < chunk.SamplesX; x++)
                        {
                            Assert.That(chunk.GetDensity(x, y, z), Is.EqualTo(world.GetDensity(x + offset.x, y + offset.y, z + offset.z)));
                        }
                    }
                }
            }
        }

        [Test]
        public void IsBoundary_OnlyAtTheWorldsOuterEdge()
        {
            VoxelWorld world = new VoxelWorld(4, 2, 1, 1, CellSize, Vector3.zero);

            Assert.That(world.IsBoundary(0, 2, 2), Is.True);
            Assert.That(world.IsBoundary(8, 2, 2), Is.True);
            Assert.That(world.IsBoundary(4, 2, 2), Is.False, "A chunk seam is not a boundary");
        }

        [Test]
        public void TotalAshVolume_CountsSharedSamplesOnce()
        {
            VoxelWorld world = new VoxelWorld(4, 2, 2, 2, CellSize, Vector3.zero);
            FillInteriorWithAsh(world);
            int interiorSamples = 7 * 7 * 7;

            Assert.That(world.TotalAshVolume(), Is.EqualTo(interiorSamples * world.CellVolume).Within(1e-5f));
        }

        [Test]
        public void SetDensity_MarksTouchedChunksDirty_AndTakeClears()
        {
            VoxelWorld world = new VoxelWorld(4, 3, 1, 1, CellSize, Vector3.zero);
            List<int> dirty = new List<int>();
            world.TakeDirtyChunks(dirty);

            world.SetDensity(4, 2, 2, 0.5f);
            world.TakeDirtyChunks(dirty);

            Assert.That(dirty, Is.EquivalentTo(new[] { 0, 1 }));
            world.TakeDirtyChunks(dirty);
            Assert.That(dirty, Is.Empty);
        }

        [Test]
        public void ChunksOverlapping_FindsChunksInABox()
        {
            VoxelWorld world = new VoxelWorld(4, 3, 1, 1, 1f, Vector3.zero);
            List<int> chunks = new List<int>();

            world.ChunksOverlapping(new Vector3(3.5f, 1f, 1f), new Vector3(4.5f, 2f, 2f), chunks);

            Assert.That(chunks, Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void SampleDensity_IsContinuousAcrossSeams()
        {
            VoxelWorld world = new VoxelWorld(4, 2, 1, 1, CellSize, Vector3.zero);
            // A gentle ramp that stays inside the -1..1 density range across the whole world.
            Fill(world, position => position.x * 0.5f);
            float seamX = 4 * CellSize;

            float justBefore = world.SampleDensity(new Vector3(seamX - 0.01f, 0.5f, 0.5f));
            float justAfter = world.SampleDensity(new Vector3(seamX + 0.01f, 0.5f, 0.5f));

            Assert.That(justAfter - justBefore, Is.EqualTo(0.01f).Within(1e-4f));
        }

        [Test]
        public void Mesh_AcrossSeam_IsClosed_AndMatchesASingleGrid()
        {
            const int chunkCells = 8;
            Vector3 center = new Vector3(chunkCells * CellSize, 1f, 1f);
            VoxelWorld world = new VoxelWorld(chunkCells, 2, 1, 1, CellSize, Vector3.zero);
            Fill(world, Ball(center, 0.87f));
            VoxelGrid single = new VoxelGrid(2 * chunkCells, chunkCells, chunkCells, CellSize, Vector3.zero);
            Fill(single, Ball(center, 0.87f));

            MarchingCubes mesher = new MarchingCubes();
            MeshBuffers buffers = new MeshBuffers();
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> indices = new List<int>();
            for (int chunk = 0; chunk < world.ChunkCount; chunk++)
            {
                mesher.Polygonize(world, chunk, buffers);
                int baseIndex = positions.Count;
                positions.AddRange(buffers.Vertices);
                normals.AddRange(buffers.Normals);
                foreach (int index in buffers.Indices)
                {
                    indices.Add(baseIndex + index);
                }
            }

            mesher.Polygonize(single, buffers);
            Assert.That(indices.Count, Is.EqualTo(buffers.Indices.Count), "Same triangles as one big grid");

            // Weld by exact position: vertices on the seam are computed identically by both chunks.
            Dictionary<Vector3, int> welded = new Dictionary<Vector3, int>();
            Dictionary<int, Vector3> weldedNormal = new Dictionary<int, Vector3>();
            int[] remap = new int[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                if (!welded.TryGetValue(positions[i], out int id))
                {
                    id = welded.Count;
                    welded.Add(positions[i], id);
                    weldedNormal.Add(id, normals[i]);
                }
                else
                {
                    Assert.That(Vector3.Distance(weldedNormal[id], normals[i]), Is.LessThan(1e-4f), "Normals differ across the seam");
                }

                remap[i] = id;
            }

            HashSet<(int, int)> edges = new HashSet<(int, int)>();
            for (int i = 0; i < indices.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    Assert.That(edges.Add((remap[indices[i + k]], remap[indices[i + (k + 1) % 3]])), Is.True, "Edge used twice in one direction");
                }
            }

            foreach ((int from, int to) in edges)
            {
                Assert.That(edges.Contains((to, from)), Is.True, "Crack at the seam");
            }
        }

        [Test]
        public void Dig_AcrossSeam_MatchesASingleGrid()
        {
            const int chunkCells = 8;
            VoxelWorld world = new VoxelWorld(chunkCells, 2, 1, 1, CellSize, Vector3.zero);
            VoxelGrid single = new VoxelGrid(2 * chunkCells, chunkCells, chunkCells, CellSize, Vector3.zero);
            FillInteriorWithAsh(world);
            FillInteriorWithAsh(single);
            Vector3 center = new Vector3(chunkCells * CellSize + 0.1f, 1f, 1f);

            DigResult worldDig = SphereDigBrush.Dig(world, center, 0.7f);
            DigResult singleDig = SphereDigBrush.Dig(single, center, 0.7f);

            Assert.That(worldDig.RemovedVolume, Is.EqualTo(singleDig.RemovedVolume).Within(1e-5f));
            for (int z = 0; z < single.SamplesZ; z++)
            {
                for (int y = 0; y < single.SamplesY; y++)
                {
                    for (int x = 0; x < single.SamplesX; x++)
                    {
                        Assert.That(world.GetDensity(x, y, z), Is.EqualTo(single.GetDensity(x, y, z)));
                    }
                }
            }
        }

        [Test]
        public void Generate_OnWorld_MatchesASingleGrid()
        {
            const int chunkCells = 8;
            Vector3 origin = new Vector3(-2f, -CellSize, -2f);
            AshPileShape shape = new AshPileShape(1.6f, 1.2f, 0.2f, 0.6f, 3);
            VoxelWorld world = new VoxelWorld(chunkCells, 2, 1, 2, CellSize, origin);
            VoxelGrid single = new VoxelGrid(2 * chunkCells, chunkCells, 2 * chunkCells, CellSize, origin);

            AshPileGenerator.Generate(world, shape);
            AshPileGenerator.Generate(single, shape);

            for (int z = 0; z < single.SamplesZ; z++)
            {
                for (int y = 0; y < single.SamplesY; y++)
                {
                    for (int x = 0; x < single.SamplesX; x++)
                    {
                        Assert.That(world.GetDensity(x, y, z), Is.EqualTo(single.GetDensity(x, y, z)));
                    }
                }
            }
        }
    }
}
