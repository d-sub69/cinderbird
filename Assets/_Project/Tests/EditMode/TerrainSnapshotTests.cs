using System.Collections.Generic;
using System.IO;
using Cinderbird.Embers;
using Cinderbird.Save;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class TerrainSnapshotTests
    {
        private const int ChunkCells = 8;
        private const float CellSize = 0.25f;
        private const float QuantizeError = 1f / 254f + 1e-6f;

        // A 6 × 2 × 4 m world (3 × 1 × 2 chunks), layered, with embers; the same arguments give the same mountain.
        private static AshTerrain CreateTerrain(int chunksX = 3)
        {
            Vector3 origin = new Vector3(-ChunkCells * CellSize, -CellSize, -ChunkCells * CellSize);
            VoxelWorld world = new VoxelWorld(ChunkCells, chunksX, 1, 2, CellSize, origin);
            AshTerrain terrain = new AshTerrain(world, 3);
            terrain.Generate(new AshPileShape(1.9f, 1.8f, 0f, 0f, 1),
                new LayerLayout(new[] { 0f, 0.5f }, new Vector3(0f, 0.4f, 0f), 0.3f));
            terrain.SpawnEmbers(new EmberSpawnParameters(40f, 0.99f, 3), new[] { 70f, 30f });
            return terrain;
        }

        // Digs where the first two chunks meet, so the dig crosses a seam.
        private static void DigAtSeam(AshTerrain terrain)
        {
            terrain.Locks.UnlockNext();
            terrain.Dig(new Vector3(0f, 1.2f, -0.3f), 0.7f, new List<Ember>());
        }

        private static TerrainSnapshot RoundTrip(TerrainSnapshot snapshot)
        {
            return TerrainSnapshot.Decode(snapshot.Encode());
        }

        [Test]
        public void Capture_FreshMountain_HasNoChunks()
        {
            TerrainSnapshot snapshot = TerrainSnapshot.Capture(CreateTerrain());

            Assert.That(snapshot.ChunkCount, Is.Zero);
        }

        [Test]
        public void Capture_AfterDigging_HoldsOnlyTheTouchedChunks()
        {
            AshTerrain terrain = CreateTerrain();
            DigAtSeam(terrain);

            TerrainSnapshot snapshot = TerrainSnapshot.Capture(terrain);

            Assert.That(snapshot.ChunkCount, Is.GreaterThan(0));
            Assert.That(snapshot.ChunkCount, Is.LessThan(terrain.World.ChunkCount), "Far chunks weren't dug");
            foreach (int index in snapshot.ChunkIndices)
            {
                Assert.That(terrain.IsChunkModified(index), Is.True);
            }
        }

        [Test]
        public void Capture_ChunkThatOnlyLostEmbers_IsIncluded()
        {
            AshTerrain terrain = CreateTerrain();
            // Out in the air, far from the mound: digging there takes the ember but changes no sample.
            Vector3 inAir = new Vector3(3f, 1.2f, 1f);
            terrain.PlaceEmber(new Ember(inAir, 0));
            List<Ember> taken = new List<Ember>();

            terrain.Dig(inAir, 0.2f, taken);

            Assert.That(taken.Count, Is.EqualTo(1));
            Assert.That(terrain.World.IsModified(terrain.World.ChunkIndexAt(inAir)), Is.False, "No ash should have changed");
            Assert.That(TerrainSnapshot.Capture(terrain).ChunkCount, Is.EqualTo(1));
        }

        [Test]
        public void Quantize_StaysWithinHalfAStep()
        {
            for (float density = -1f; density <= 1f; density += 0.013f)
            {
                float back = TerrainSnapshot.Dequantize(TerrainSnapshot.Quantize(density));
                Assert.That(back, Is.EqualTo(density).Within(QuantizeError));
            }

            Assert.That(TerrainSnapshot.Dequantize(TerrainSnapshot.Quantize(VoxelGrid.Air)), Is.EqualTo(VoxelGrid.Air));
            Assert.That(TerrainSnapshot.Dequantize(TerrainSnapshot.Quantize(VoxelGrid.Ash)), Is.EqualTo(VoxelGrid.Ash));
        }

        [Test]
        public void ApplyTo_FreshMountain_ReproducesTheDugOne()
        {
            AshTerrain dug = CreateTerrain();
            DigAtSeam(dug);
            AshTerrain fresh = CreateTerrain();

            RoundTrip(TerrainSnapshot.Capture(dug)).ApplyTo(fresh);

            VoxelWorld a = dug.World;
            VoxelWorld b = fresh.World;
            for (int z = 0; z < a.SamplesZ; z++)
            {
                for (int y = 0; y < a.SamplesY; y++)
                {
                    for (int x = 0; x < a.SamplesX; x++)
                    {
                        Assert.That(b.GetDensity(x, y, z), Is.EqualTo(a.GetDensity(x, y, z)).Within(QuantizeError), $"Density at ({x},{y},{z})");
                        Assert.That(b.GetLayer(x, y, z), Is.EqualTo(a.GetLayer(x, y, z)), $"Layer at ({x},{y},{z})");
                    }
                }
            }

            Assert.That(fresh.BuriedEmberCount, Is.EqualTo(dug.BuriedEmberCount));
            Assert.That(fresh.TotalAshVolume, Is.EqualTo(dug.TotalAshVolume).Within(0.01f));
        }

        [Test]
        public void ApplyTo_KeepsSharedBorderCopiesEqual()
        {
            AshTerrain dug = CreateTerrain();
            DigAtSeam(dug);
            AshTerrain fresh = CreateTerrain();

            RoundTrip(TerrainSnapshot.Capture(dug)).ApplyTo(fresh);

            // Every chunk's copy of a sample must equal the world's value, or meshes would crack at seams.
            VoxelWorld world = fresh.World;
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
        public void ApplyTo_RestoresEmbersPerChunk()
        {
            AshTerrain dug = CreateTerrain();
            DigAtSeam(dug);
            AshTerrain fresh = CreateTerrain();

            TerrainSnapshot snapshot = RoundTrip(TerrainSnapshot.Capture(dug));
            snapshot.ApplyTo(fresh);

            foreach (int index in snapshot.ChunkIndices)
            {
                EmberField expected = dug.EmbersIn(index);
                EmberField actual = fresh.EmbersIn(index);
                Assert.That(actual.Count, Is.EqualTo(expected.Count));
                for (int e = 0; e < expected.Count; e++)
                {
                    Assert.That(actual[e].Position, Is.EqualTo(expected[e].Position));
                    Assert.That(actual[e].TierIndex, Is.EqualTo(expected[e].TierIndex));
                    Assert.That(actual[e].Layer, Is.EqualTo(expected[e].Layer));
                }
            }
        }

        [Test]
        public void ApplyTo_OnlyRestoredChunksCountAsModified()
        {
            AshTerrain dug = CreateTerrain();
            DigAtSeam(dug);
            AshTerrain fresh = CreateTerrain();
            TerrainSnapshot snapshot = TerrainSnapshot.Capture(dug);

            snapshot.ApplyTo(fresh);

            Assert.That(TerrainSnapshot.Capture(fresh).ChunkCount, Is.EqualTo(snapshot.ChunkCount));
        }

        [Test]
        public void ApplyTo_WorldOfAnotherSize_ThrowsAndChangesNothing()
        {
            AshTerrain dug = CreateTerrain(3);
            DigAtSeam(dug);
            AshTerrain smaller = CreateTerrain(1);
            float before = smaller.TotalAshVolume;
            TerrainSnapshot snapshot = TerrainSnapshot.Capture(dug);

            // Chunk indices beyond the smaller world make it not fit.
            if (snapshot.FitsWorld(smaller.World))
            {
                Assert.Inconclusive("The dig happened to touch only chunks the smaller world also has.");
            }

            Assert.Throws<InvalidDataException>(() => snapshot.ApplyTo(smaller));
            Assert.That(smaller.TotalAshVolume, Is.EqualTo(before));
        }

        [Test]
        public void Decode_Garbage_ThrowsInvalidData()
        {
            Assert.Throws<InvalidDataException>(() => TerrainSnapshot.Decode("not base64 at all!"));
            Assert.Throws<InvalidDataException>(() => TerrainSnapshot.Decode("SGVsbG8gd29ybGQ="));
            Assert.Throws<InvalidDataException>(() => TerrainSnapshot.Decode(string.Empty));
        }

        [Test]
        public void Decode_Truncated_ThrowsInvalidData()
        {
            AshTerrain dug = CreateTerrain();
            DigAtSeam(dug);
            string data = TerrainSnapshot.Capture(dug).Encode();

            byte[] bytes = System.Convert.FromBase64String(data);
            string truncated = System.Convert.ToBase64String(bytes, 0, bytes.Length / 2);

            Assert.Throws<InvalidDataException>(() => TerrainSnapshot.Decode(truncated));
        }

        [Test]
        public void Encode_IsCompact()
        {
            // Real-size chunks and no embers, so the voxel data dominates as it does in the game.
            const int cells = 32;
            VoxelWorld world = new VoxelWorld(cells, 1, 1, 1, CellSize, new Vector3(-4f, -CellSize, -4f));
            AshTerrain terrain = new AshTerrain(world);
            terrain.Generate(new AshPileShape(3.5f, 5f, 0.5f, 0.3f, 1));
            terrain.Dig(new Vector3(0f, 3f, 0f), 1f, new List<Ember>());
            TerrainSnapshot snapshot = TerrainSnapshot.Capture(terrain);

            int raw = snapshot.ChunkCount * snapshot.SamplesPerChunk * 2;
            Assert.That(snapshot.ChunkCount, Is.EqualTo(1));
            Assert.That(snapshot.Encode().Length, Is.LessThan(raw / 2), "Packed data should be much smaller than raw bytes");
        }

        [Test]
        public void Update_WithoutChanges_CopiesNothing()
        {
            AshTerrain terrain = CreateTerrain();
            DigAtSeam(terrain);
            TerrainSnapshot snapshot = TerrainSnapshot.Capture(terrain);
            Assert.That(snapshot.LastCopiedChunkCount, Is.EqualTo(snapshot.ChunkCount));

            snapshot.Update(terrain);

            Assert.That(snapshot.LastCopiedChunkCount, Is.Zero);
            Assert.That(snapshot.ChunkCount, Is.GreaterThan(0), "Unchanged chunks stay in the snapshot");
        }

        [Test]
        public void Update_AfterAnotherDig_CopiesOnlyWhatChanged_AndStaysCurrent()
        {
            AshTerrain terrain = CreateTerrain();
            DigAtSeam(terrain);
            TerrainSnapshot snapshot = TerrainSnapshot.Capture(terrain);

            // Inside the first chunk only, away from its shared borders.
            terrain.Dig(new Vector3(-0.8f, 0.9f, -0.8f), 0.3f, new List<Ember>());
            snapshot.Update(terrain);

            Assert.That(snapshot.LastCopiedChunkCount, Is.LessThan(snapshot.ChunkCount), "Untouched chunks aren't copied again");

            AshTerrain fresh = CreateTerrain();
            RoundTrip(snapshot).ApplyTo(fresh);
            VoxelWorld a = terrain.World;
            VoxelWorld b = fresh.World;
            for (int z = 0; z < a.SamplesZ; z++)
            {
                for (int y = 0; y < a.SamplesY; y++)
                {
                    for (int x = 0; x < a.SamplesX; x++)
                    {
                        Assert.That(b.GetDensity(x, y, z), Is.EqualTo(a.GetDensity(x, y, z)).Within(QuantizeError), $"Density at ({x},{y},{z})");
                    }
                }
            }

            Assert.That(fresh.BuriedEmberCount, Is.EqualTo(terrain.BuriedEmberCount));
        }

        [Test]
        public void Update_WorldOfAnotherSize_Throws()
        {
            TerrainSnapshot snapshot = TerrainSnapshot.Capture(CreateTerrain(3));

            Assert.Throws<System.InvalidOperationException>(() => snapshot.Update(CreateTerrain(1)));
        }
    }
}
