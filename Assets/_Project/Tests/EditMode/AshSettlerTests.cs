using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class AshSettlerTests
    {
        private const float CellSize = 0.25f;
        private static readonly SettleParameters Parameters = new SettleParameters(35f, 3f, 0.5f);

        // 16 × 12 × 16 m, a 6 m mound (its mid slopes are steeper than 35°), four layers with a locked pocket.
        private static AshTerrain CreateMountain()
        {
            const int cells = 16;
            float size = cells * CellSize;
            VoxelWorld world = new VoxelWorld(cells, 4, 3, 4, CellSize, new Vector3(-2f * size, -CellSize, -2f * size));
            AshTerrain terrain = new AshTerrain(world, 4);
            terrain.Generate(new AshPileShape(7f, 6f, 0.5f, 0.3f, 1),
                new LayerLayout(new[] { 0f, 0.6f, 1.5f }, new Vector3(0f, 1f, 0f), 0.8f));
            terrain.SpawnEmbers(new EmberSpawnParameters(20f, 0.99f, 7), new[] { 60f, 30f, 10f });
            return terrain;
        }

        // 8 × 4 × 8 m of flat ash, surface at y = 3 m; the top half meter is layer 0, the rest layer 1.
        private static AshTerrain CreateSlab(int layerCount)
        {
            VoxelWorld world = new VoxelWorld(16, 2, 1, 2, CellSize, new Vector3(-4f, -CellSize, -4f));
            AshTerrain terrain = new AshTerrain(world, layerCount);
            for (int z = 1; z < world.SamplesZ - 1; z++)
            {
                for (int y = 1; y < world.SamplesY - 1; y++)
                {
                    for (int x = 1; x < world.SamplesX - 1; x++)
                    {
                        float height = world.SamplePosition(x, y, z).y;
                        world.SetDensity(x, y, z, Mathf.Clamp((3f - height) / CellSize, VoxelGrid.Air, VoxelGrid.Ash));
                        world.SetLayer(x, y, z, (byte)(height > 2.5f || layerCount == 1 ? 0 : 1));
                    }
                }
            }

            return terrain;
        }

        // A deep pit from several stacked digs, so its walls are far too steep.
        private static void DigPit(AshTerrain terrain, Vector3 top, float radius, int digs)
        {
            for (int i = 0; i < digs; i++)
            {
                terrain.Dig(top + Vector3.down * (0.35f * i), radius, new List<Ember>());
            }
        }

        private static void Settle(AshTerrain terrain, Vector3 center, float radius)
        {
            AshSettler settler = new AshSettler(Parameters);
            settler.Begin(terrain, center, radius);
            settler.Finish();
        }

        private static float[] CopyDensities(VoxelWorld world)
        {
            float[] densities = new float[world.SamplesX * world.SamplesY * world.SamplesZ];
            int n = 0;
            for (int z = 0; z < world.SamplesZ; z++)
            {
                for (int y = 0; y < world.SamplesY; y++)
                {
                    for (int x = 0; x < world.SamplesX; x++)
                    {
                        densities[n++] = world.GetDensity(x, y, z);
                    }
                }
            }

            return densities;
        }

        private static float MaxChange(VoxelWorld world, float[] before)
        {
            float max = 0f;
            int n = 0;
            for (int z = 0; z < world.SamplesZ; z++)
            {
                for (int y = 0; y < world.SamplesY; y++)
                {
                    for (int x = 0; x < world.SamplesX; x++)
                    {
                        max = Mathf.Max(max, Mathf.Abs(world.GetDensity(x, y, z) - before[n++]));
                    }
                }
            }

            return max;
        }

        // Surface height of a column in samples, the same way the settler measures it.
        private static float ColumnHeight(VoxelWorld world, int x, int z)
        {
            float mass = 0f;
            for (int y = 1; y < world.SamplesY - 1; y++)
            {
                mass += VoxelGrid.Fill(world.GetDensity(x, y, z));
            }

            return 1f + (mass <= 0.5f ? 2f * mass - 1f : mass - 0.5f);
        }

        private static Vector2Int ColumnOf(VoxelWorld world, Vector3 position)
        {
            Vector3 sample = (position - world.Origin) / world.CellSize;
            return new Vector2Int(Mathf.RoundToInt(sample.x), Mathf.RoundToInt(sample.z));
        }

        [Test]
        public void UntouchedMountain_DoesNotMove_EvenWhereSteeperThanTheAngle()
        {
            AshTerrain terrain = CreateMountain();
            float[] before = CopyDensities(terrain.World);

            Settle(terrain, new Vector3(3f, 3f, 0f), 0.01f);

            Assert.That(MaxChange(terrain.World, before), Is.LessThan(1e-4f));
        }

        [Test]
        public void Pit_Settles_WithoutCreatingOrLosingAsh()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            float before = terrain.World.TotalAshVolume();

            Settle(terrain, top, 0.6f);

            Assert.That(terrain.World.TotalAshVolume(), Is.EqualTo(before).Within(before * 1e-4f));
        }

        [Test]
        public void Pit_Settles_KeepingEveryEmberBuried()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            int count = terrain.BuriedEmberCount;

            Settle(terrain, top, 0.6f);

            List<Ember> embers = new List<Ember>();
            terrain.CopyAllEmbers(embers);
            Assert.That(embers.Count, Is.EqualTo(count));
            foreach (Ember ember in embers)
            {
                Assert.That(terrain.World.SampleDensity(ember.Position), Is.GreaterThan(0f), $"Ember at {ember.Position} is in the open");
            }
        }

        [Test]
        public void LockedAsh_NeverMoves()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            VoxelWorld world = terrain.World;
            float[] before = CopyDensities(world);

            Settle(terrain, top, 0.6f);

            int n = 0;
            for (int z = 0; z < world.SamplesZ; z++)
            {
                for (int y = 0; y < world.SamplesY; y++)
                {
                    for (int x = 0; x < world.SamplesX; x++)
                    {
                        float old = before[n++];
                        if (VoxelGrid.Fill(old) > 0f && !terrain.Locks.IsUnlocked(world.GetLayer(x, y, z)))
                        {
                            Assert.That(world.GetDensity(x, y, z), Is.EqualTo(old), $"Locked ash at ({x},{y},{z}) moved");
                        }
                    }
                }
            }
        }

        [Test]
        public void SettledArea_StaysAtRest()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            Settle(terrain, top, 0.6f);
            float[] before = CopyDensities(terrain.World);

            AshSettler again = new AshSettler(Parameters);
            again.Begin(terrain, top, 0.6f);
            bool moving = again.Step(1);
            again.Finish();

            Assert.That(moving, Is.False);
            Assert.That(MaxChange(terrain.World, before), Is.LessThan(1e-4f));
        }

        [Test]
        public void Slopes_EndUpNoSteeperThanAllowed()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(2);
            Vector3 top = new Vector3(-1f, 5f, 1f);
            DigPit(terrain, top, 0.7f, 8);

            Settle(terrain, top, 0.7f);

            VoxelWorld world = terrain.World;
            float maxRise = Parameters.MaxRisePerCell;
            Vector2Int center = ColumnOf(world, top);
            for (int z = center.y - 6; z <= center.y + 6; z++)
            {
                for (int x = center.x - 6; x <= center.x + 6; x++)
                {
                    foreach (Vector2Int step in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) })
                    {
                        float natural = Mathf.Abs(terrain.NaturalSurfaceSample(x, z) - terrain.NaturalSurfaceSample(x + step.x, z + step.y));
                        float allowed = Mathf.Max(maxRise, natural);
                        float rise = Mathf.Abs(ColumnHeight(world, x, z) - ColumnHeight(world, x + step.x, z + step.y));
                        Assert.That(rise, Is.LessThanOrEqualTo(allowed + 0.1f), $"Too steep between ({x},{z}) and its neighbor");
                    }
                }
            }
        }

        [Test]
        public void Ash_StaysWithinTheReach()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            VoxelWorld world = terrain.World;
            float[] before = CopyDensities(world);

            Settle(terrain, top, 0.6f);

            float limit = (0.6f + Parameters.Reach) / CellSize + 1f;
            Vector2Int center = ColumnOf(world, top);
            int n = 0;
            for (int z = 0; z < world.SamplesZ; z++)
            {
                for (int y = 0; y < world.SamplesY; y++)
                {
                    for (int x = 0; x < world.SamplesX; x++)
                    {
                        float old = before[n++];
                        if (Mathf.Max(Mathf.Abs(x - center.x), Mathf.Abs(z - center.y)) > limit)
                        {
                            Assert.That(world.GetDensity(x, y, z), Is.EqualTo(old), $"Ash at ({x},{y},{z}) outside the reach changed");
                        }
                    }
                }
            }
        }

        [Test]
        public void Overhang_Collapses_KeepingItsAsh()
        {
            AshTerrain terrain = CreateSlab(1);
            VoxelWorld world = terrain.World;
            // Centered 0.8 m under the surface with a 0.5 m radius: a cave with a roof of ash.
            Vector3 cave = new Vector3(0f, 2.2f, 0f);
            terrain.Dig(cave, 0.5f, new List<Ember>());
            Vector2Int column = ColumnOf(world, cave);
            int caveY = Mathf.RoundToInt((cave.y - world.Origin.y) / CellSize);
            Assert.That(world.GetDensity(column.x, caveY, column.y), Is.LessThan(0f), "Setup: the cave should be air");
            float before = world.TotalAshVolume();

            Settle(terrain, cave, 0.5f);

            // Every column is now solid from the floor up to its surface.
            for (int y = 1; y < world.SamplesY - 1; y++)
            {
                if (world.GetDensity(column.x, y, column.y) < 0f)
                {
                    for (int above = y + 1; above < world.SamplesY - 1; above++)
                    {
                        Assert.That(world.GetDensity(column.x, above, column.y), Is.LessThanOrEqualTo(0f), $"Ash floats at y={above}");
                    }

                    break;
                }
            }

            Assert.That(world.TotalAshVolume(), Is.EqualTo(before).Within(before * 1e-4f));
        }

        [Test]
        public void SlidingCrust_KeepsItsLayer()
        {
            AshTerrain terrain = CreateSlab(2);
            terrain.Locks.UnlockNext();
            Vector3 top = new Vector3(0f, 3f, 0f);
            DigPit(terrain, top, 0.5f, 5);

            Settle(terrain, top, 0.5f);

            VoxelWorld world = terrain.World;
            Vector2Int column = ColumnOf(world, top);
            int surface = 0;
            for (int y = 1; y < world.SamplesY - 1; y++)
            {
                if (world.GetDensity(column.x, y, column.y) > 0f)
                {
                    surface = y;
                }
            }

            Assert.That(world.GetLayer(column.x, surface, column.y), Is.EqualTo(0), "Crust that slid into the pit should stay crust");
        }

        [Test]
        public void World_CopyColumn_MatchesSampleBySample()
        {
            AshTerrain terrain = CreateMountain();
            VoxelWorld world = terrain.World;
            float[] densities = new float[world.SamplesY];
            byte[] layers = new byte[world.SamplesY];

            world.CopyColumn(20, 33, densities, layers);

            for (int y = 0; y < world.SamplesY; y++)
            {
                Assert.That(densities[y], Is.EqualTo(world.GetDensity(20, y, 33)));
                Assert.That(layers[y], Is.EqualTo(world.GetLayer(20, y, 33)));
            }
        }

        [Test]
        public void Terrain_MoveEmber_CrossesChunks()
        {
            AshTerrain terrain = CreateMountain();
            terrain.PlaceEmber(new Ember(new Vector3(-0.5f, 2f, -0.5f), 2, 1));
            Ember from = new Ember(new Vector3(-0.5f, 2f, -0.5f), 2, 1);
            Ember to = new Ember(new Vector3(0.5f, 2f, 0.5f), 2, 1);
            int count = terrain.BuriedEmberCount;

            int chunk = terrain.MoveEmber(terrain.World.ChunkIndexAt(from.Position), from, to);

            Assert.That(chunk, Is.EqualTo(terrain.World.ChunkIndexAt(to.Position)));
            Assert.That(terrain.MoveEmber(terrain.World.ChunkIndexAt(from.Position), from, to), Is.EqualTo(-1), "Already moved");
            List<Ember> all = new List<Ember>();
            terrain.CopyAllEmbers(all);
            Assert.That(all.Exists(e => e.Position == to.Position), Is.True);
            Assert.That(all.Count, Is.EqualTo(count));
        }

        [Test]
        public void Parameters_RejectImpossibleAngles()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleParameters(0f, 3f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleParameters(90f, 3f, 0.5f));
        }

        [Test]
        public void Commit_ReportsWhereAshRan_ForDust()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            AshSettler settler = new AshSettler(Parameters);
            settler.Begin(terrain, top, 0.6f);

            settler.Step(40);
            settler.Commit();

            Assert.That(settler.LastCommitChangedAnything, Is.True);
            Assert.That(settler.LastSlidePoints.Count, Is.GreaterThan(0));
            Assert.That(settler.LastSlideLayers.Count, Is.EqualTo(settler.LastSlidePoints.Count));
            foreach (Vector3 point in settler.LastSlidePoints)
            {
                Assert.That((point - top).magnitude, Is.LessThan(0.6f + Parameters.Reach + 1f), "Dust far from the dig");
            }

            Bounds changed = settler.LastCommitBounds;
            Assert.That(changed.size.x, Is.GreaterThan(0f));
            settler.End();
        }

        [Test]
        public void Settling_InSeveralUpdates_EndsLikeSettlingAtOnce()
        {
            AshTerrain stepwise = CreateMountain();
            AshTerrain atOnce = CreateMountain();
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            foreach (AshTerrain terrain in new[] { stepwise, atOnce })
            {
                terrain.Locks.UnlockThrough(1);
                DigPit(terrain, top, 0.6f, 6);
            }

            AshSettler settler = new AshSettler(Parameters);
            settler.Begin(stepwise, top, 0.6f);
            int updates = 0;
            while (settler.Step(30) && updates < 100)
            {
                settler.Commit();
                updates++;
            }

            settler.Commit();
            settler.End();
            Settle(atOnce, top, 0.6f);

            Assert.That(updates, Is.GreaterThan(1), "A deep pit should take several updates");
            Assert.That(MaxChange(stepwise.World, CopyDensities(atOnce.World)), Is.LessThan(1e-4f));
            Assert.That(stepwise.BuriedEmberCount, Is.EqualTo(atOnce.BuriedEmberCount));
        }

        // A scoop center a quarter meter under the generated surface at x/z.
        private static Vector3 UnderSurface(AshTerrain terrain, float x, float z)
        {
            VoxelWorld world = terrain.World;
            Vector2Int column = ColumnOf(world, new Vector3(x, 0f, z));
            float surface = world.Origin.y + terrain.NaturalSurfaceSample(column.x, column.y) * world.CellSize;
            return new Vector3(x, surface - 0.25f, z);
        }

        [Test]
        public void Scoop_DrainedWhileSettling_TakesExactlyWhatWasCounted()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 center = UnderSurface(terrain, 3f, 0.3f);
            float before = terrain.World.TotalAshVolume();
            int embersBefore = terrain.BuriedEmberCount;
            List<Ember> taken = new List<Ember>();
            DigResult counted = terrain.StartDig(center, 0.5f, taken, null, null, 0f);
            Assert.That(counted.RemovedVolume, Is.GreaterThan(0.1f), "Setup: the scoop should take ash");

            AshSettler settler = new AshSettler(new SettleParameters(50f, 2f, 0.5f, 0.1f));
            settler.BeginScoop(terrain, center, 0.5f);
            Assert.That(settler.IsDraining, Is.True);
            for (int update = 1; update <= 400; update++)
            {
                settler.Drain(update / 9f);
                bool moving = settler.Step(4);
                settler.Commit();
                if (!moving && !settler.IsDraining)
                {
                    break;
                }
            }

            settler.End();
            Assert.That(terrain.World.TotalAshVolume(), Is.EqualTo(before - counted.RemovedVolume).Within(0.002f));
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(embersBefore - taken.Count));
            List<Ember> embers = new List<Ember>();
            terrain.CopyAllEmbers(embers);
            foreach (Ember ember in embers)
            {
                Assert.That(terrain.World.SampleDensity(ember.Position), Is.GreaterThan(0f), $"Ember at {ember.Position} is in the open");
            }
        }

        [Test]
        public void Drain_HalfwayTakesAboutHalf()
        {
            AshTerrain terrain = CreateMountain();
            Vector3 center = UnderSurface(terrain, 0.2f, 0.1f);
            float before = terrain.World.TotalAshVolume();
            DigResult counted = terrain.StartDig(center, 0.5f, new List<Ember>(), null, null, 0f);
            AshSettler settler = new AshSettler(Parameters);
            settler.BeginScoop(terrain, center, 0.5f);

            settler.Drain(0.5f);
            settler.Commit();

            Assert.That(before - terrain.World.TotalAshVolume(), Is.EqualTo(counted.RemovedVolume * 0.5f).Within(counted.RemovedVolume * 0.02f));
            Assert.That(settler.IsDraining, Is.True);
            settler.Finish();
            Assert.That(before - terrain.World.TotalAshVolume(), Is.EqualTo(counted.RemovedVolume).Within(0.002f));
        }

        [Test]
        public void FlowCap_SpreadsTheSlideOverMoreSteps()
        {
            int Steps(float cap)
            {
                AshTerrain terrain = CreateMountain();
                terrain.Locks.UnlockThrough(1);
                Vector3 top = new Vector3(3f, 4.2f, 0.3f);
                DigPit(terrain, top, 0.6f, 6);
                AshSettler settler = new AshSettler(new SettleParameters(35f, 3f, 0.5f, cap));
                settler.Begin(terrain, top, 0.6f);
                int steps = 0;
                while (settler.Step(1) && steps < 5000)
                {
                    steps++;
                }

                settler.End();
                return steps;
            }

            Assert.That(Steps(0.05f), Is.GreaterThan(Steps(0f)));
        }

        [Test]
        public void LongSlideThroughSeveralLayers_KeepsFewLayerSlicesPerColumn()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(2);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 1.2f, 4);
            AshSettler settler = new AshSettler(new SettleParameters(35f, 3f, 0.5f, 0.02f));
            settler.Begin(terrain, top, 1.2f);

            for (int update = 0; update < 300 && settler.Step(4); update++)
            {
                settler.Commit();
            }

            Assert.That(settler.MostLayerSlicesInAColumn, Is.LessThanOrEqualTo(8), "thin alternating slices used to pile up without end");
            settler.End();
        }

        [Test]
        public void LooseEmber_OnAPitsRim_RidesTheSlide()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            // On the steep wall just outside the pit, at the surface.
            VoxelWorld world = terrain.World;
            Vector2Int column = ColumnOf(world, top + new Vector3(0.7f, 0f, 0f));
            float surface = world.Origin.y + (ColumnHeight(world, column.x, column.y)) * world.CellSize;
            Vector3 onRim = new Vector3(world.SamplePosition(column.x, 0, column.y).x, surface, world.SamplePosition(column.x, 0, column.y).z);
            int id = terrain.AddLoose(new Ember(onRim, 0, 0));
            terrain.SetLooseRest(terrain.IndexOfLoose(id), 0f);
            int count = terrain.BuriedEmberCount;

            AshSettler settler = new AshSettler(Parameters);
            settler.UseLooseEmbers(terrain.LooseEmbers);
            settler.Begin(terrain, top, 0.6f);
            settler.Finish();

            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(count), "Still on the mountain");
            int index = terrain.IndexOfLoose(id);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Still loose: found embers are never buried again");
            Vector3 now = terrain.LooseEmbers[index].Ember.Position;
            Assert.That(now.y, Is.LessThan(onRim.y), "It went down with the ash");
            Assert.That(world.SampleDensity(now + Vector3.up * 0.01f), Is.LessThanOrEqualTo(0.05f), "And sits at the surface, not inside the ash");
        }

        [Test]
        public void LooseEmber_AtTheBottomOfAPit_IsLiftedByAshSlidingIn_NotBuried()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            VoxelWorld world = terrain.World;
            Vector2Int column = ColumnOf(world, top);
            float floor = world.Origin.y + ColumnHeight(world, column.x, column.y) * world.CellSize;
            Vector3 sample = world.SamplePosition(column.x, 0, column.y);
            Vector3 atBottom = new Vector3(sample.x, floor, sample.z);
            int id = terrain.AddLoose(new Ember(atBottom, 0, 0));
            terrain.SetLooseRest(terrain.IndexOfLoose(id), 0.05f);

            AshSettler settler = new AshSettler(Parameters);
            settler.UseLooseEmbers(terrain.LooseEmbers);
            settler.Begin(terrain, top, 0.6f);
            settler.Finish();

            int index = terrain.IndexOfLoose(id);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Still loose");
            Vector3 now = terrain.LooseEmbers[index].Ember.Position;
            Assert.That(now.y, Is.GreaterThan(atBottom.y), "The walls slid in and lifted it");
            Assert.That(world.SampleDensity(now), Is.LessThanOrEqualTo(0f), "Its center is above the ash, never inside it");
        }

        [Test]
        public void LooseEmber_NotMeasuredYet_IsLeftForTheViewToPlace()
        {
            AshTerrain terrain = CreateMountain();
            terrain.Locks.UnlockThrough(1);
            Vector3 top = new Vector3(3f, 4.2f, 0.3f);
            DigPit(terrain, top, 0.6f, 6);
            VoxelWorld world = terrain.World;
            Vector2Int column = ColumnOf(world, top + new Vector3(0.7f, 0f, 0f));
            float surface = world.Origin.y + ColumnHeight(world, column.x, column.y) * world.CellSize;
            Vector3 onRim = new Vector3(world.SamplePosition(column.x, 0, column.y).x, surface, world.SamplePosition(column.x, 0, column.y).z);
            int id = terrain.AddLoose(new Ember(onRim, 0, 0));

            AshSettler settler = new AshSettler(Parameters);
            settler.UseLooseEmbers(terrain.LooseEmbers);
            settler.Begin(terrain, top, 0.6f);
            settler.Finish();

            Assert.That(terrain.LooseEmbers[terrain.IndexOfLoose(id)].Ember.Position, Is.EqualTo(onRim));
        }
    }
}
