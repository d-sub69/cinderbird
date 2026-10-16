using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Helpers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class HelperTests
    {
        private const float Cell = 0.25f;

        // Two by two plots of 4 m; the floor (local y = 0) is sample 1.
        private static VoxelWorld CreateWorld()
        {
            return new VoxelWorld(16, 2, 1, 2, Cell, new Vector3(0f, -Cell, 0f));
        }

        // Ash up to a height over a rectangle of sample columns; below `layerSplit` the samples are layer 1.
        private static void Fill(VoxelWorld world, int x0, int z0, int x1, int z1, float height, float layerSplit = -1f)
        {
            for (int z = Mathf.Max(1, z0); z <= Mathf.Min(world.SamplesZ - 2, z1); z++)
            {
                for (int x = Mathf.Max(1, x0); x <= Mathf.Min(world.SamplesX - 2, x1); x++)
                {
                    for (int y = 1; y < world.SamplesY - 1; y++)
                    {
                        float sampleY = world.SamplePosition(x, y, z).y;
                        world.SetDensity(x, y, z, Mathf.Clamp((height - sampleY) / Cell, -1f, 1f));
                        world.SetLayer(x, y, z, (byte)(sampleY < layerSplit ? 1 : 0));
                    }
                }
            }
        }

        private static HelperFacts Facts(bool talking = false, bool busy = false, bool ash = false, bool full = false, bool embers = false,
            bool work = true, bool atWork = false, bool atNest = false, bool atHome = false)
        {
            return new HelperFacts(talking, busy, ash, full, embers, work, atWork, atNest, atHome);
        }

        [Test]
        public void Brain_TalkingOrBusy_Waits()
        {
            Assert.That(HelperBrain.Decide(HelperMode.Auto, Facts(talking: true, ash: true)), Is.EqualTo(HelperActivity.Wait));
            Assert.That(HelperBrain.Decide(HelperMode.Auto, Facts(busy: true)), Is.EqualTo(HelperActivity.Wait));
        }

        [Test]
        public void Brain_AshInTheSieve_ShakesFirst_EvenWhenSentHome()
        {
            Assert.That(HelperBrain.Decide(HelperMode.Off, Facts(ash: true, full: true)), Is.EqualTo(HelperActivity.Shake));
        }

        [Test]
        public void Brain_SentToASpot_GoesThereFirst_AfterShakingAndAFullSieve()
        {
            Assert.That(HelperBrain.Decide(HelperMode.Assigned, new HelperFacts(false, false, false, false, true, true, true, false, false, true)),
                Is.EqualTo(HelperActivity.GoToSpot), "before digging, though work is in reach");
            Assert.That(HelperBrain.Decide(HelperMode.Assigned, new HelperFacts(false, false, true, false, false, true, false, false, false, true)),
                Is.EqualTo(HelperActivity.Shake));
            Assert.That(HelperBrain.Decide(HelperMode.Assigned, new HelperFacts(false, false, false, true, true, true, false, false, false, true)),
                Is.EqualTo(HelperActivity.GoToNest));
        }

        [Test]
        public void Brain_FullSieve_GoesToTheNest_ThenPours()
        {
            Assert.That(HelperBrain.Decide(HelperMode.Auto, Facts(full: true, embers: true)), Is.EqualTo(HelperActivity.GoToNest));
            Assert.That(HelperBrain.Decide(HelperMode.Auto, Facts(full: true, embers: true, atNest: true)), Is.EqualTo(HelperActivity.Pour));
        }

        [Test]
        public void Brain_Work_WalksThereThenScoops()
        {
            Assert.That(HelperBrain.Decide(HelperMode.Auto, Facts(embers: true)), Is.EqualTo(HelperActivity.GoToWork));
            Assert.That(HelperBrain.Decide(HelperMode.Assigned, Facts(atWork: true)), Is.EqualTo(HelperActivity.Scoop));
        }

        [Test]
        public void Brain_Off_DeliversWhatSheCarries_ThenGoesHome()
        {
            Assert.That(HelperBrain.Decide(HelperMode.Off, Facts(embers: true)), Is.EqualTo(HelperActivity.GoToNest));
            Assert.That(HelperBrain.Decide(HelperMode.Off, Facts()), Is.EqualTo(HelperActivity.GoHome));
            Assert.That(HelperBrain.Decide(HelperMode.Off, Facts(atHome: true)), Is.EqualTo(HelperActivity.Idle));
        }

        [Test]
        public void Brain_NoWorkLeft_DeliversThenWaitsByTheShop()
        {
            Assert.That(HelperBrain.Decide(HelperMode.Auto, Facts(work: false, embers: true)), Is.EqualTo(HelperActivity.GoToNest));
            Assert.That(HelperBrain.Decide(HelperMode.Auto, Facts(work: false)), Is.EqualTo(HelperActivity.GoHome));
        }

        private static NavGrid OpenGrid(int size)
        {
            NavGrid grid = new NavGrid(Vector2.zero, size, size, 1f, 0.5f);
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    grid.SetStatic(x, z, false, 0f);
                }
            }

            return grid;
        }

        private sealed class CellGoal : IPathGoal
        {
            private readonly Vector2Int _cell;

            public CellGoal(Vector2Int cell)
            {
                _cell = cell;
            }

            public bool IsGoal(int x, int z)
            {
                return x == _cell.x && z == _cell.y;
            }

            public float Estimate(int x, int z)
            {
                return Vector2.Distance(new Vector2(x, z), _cell);
            }
        }

        [Test]
        public void Pathfinder_GoesAroundAWall()
        {
            NavGrid grid = OpenGrid(10);
            for (int z = 0; z < 8; z++)
            {
                grid.SetStatic(5, z, true, 0f);
            }

            GridPathfinder finder = new GridPathfinder(1000, 0f);
            List<Vector2Int> path = new List<Vector2Int>();

            Assert.That(finder.FindPath(grid, new Vector2Int(1, 1), new CellGoal(new Vector2Int(8, 1)), path), Is.True);
            Assert.That(path[0], Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(path[path.Count - 1], Is.EqualTo(new Vector2Int(8, 1)));
            foreach (Vector2Int cell in path)
            {
                Assert.That(grid.IsWalkable(cell.x, cell.y), Is.True);
            }

            Assert.That(path.Exists(cell => cell.y >= 8), Is.True, "around the wall's end");
        }

        [Test]
        public void Pathfinder_WalledIn_Fails()
        {
            NavGrid grid = OpenGrid(10);
            for (int z = 0; z < 10; z++)
            {
                grid.SetStatic(5, z, true, 0f);
            }

            GridPathfinder finder = new GridPathfinder(1000, 0f);

            Assert.That(finder.FindPath(grid, new Vector2Int(1, 1), new CellGoal(new Vector2Int(8, 1)), new List<Vector2Int>()), Is.False);
        }

        [Test]
        public void Pathfinder_TooSteep_IsAWall()
        {
            NavGrid grid = OpenGrid(10);
            for (int z = 0; z < 10; z++)
            {
                // A 2 m cliff; the grid allows 0.5 m per cell.
                grid.SetStatic(6, z, false, 2f);
            }

            GridPathfinder finder = new GridPathfinder(1000, 0f);

            Assert.That(finder.FindPath(grid, new Vector2Int(1, 1), new CellGoal(new Vector2Int(8, 1)), new List<Vector2Int>()), Is.False);
        }

        private static NavGrid JumpingGrid(int size)
        {
            // Walks 0.5 m per 1 m cell, jumps 0.9 m, drops 2 m.
            NavGrid grid = new NavGrid(Vector2.zero, size, size, 1f, 0.5f, 0.9f, 2f);
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    grid.SetStatic(x, z, false, 0f);
                }
            }

            return grid;
        }

        [Test]
        public void Pathfinder_JumpsOntoALowWall_AndDropsOffIt()
        {
            NavGrid grid = JumpingGrid(10);
            for (int z = 0; z < 10; z++)
            {
                // An 0.8 m wall across the whole yard, two cells thick.
                grid.SetStatic(5, z, false, 0.8f);
                grid.SetStatic(6, z, false, 0.8f);
            }

            GridPathfinder finder = new GridPathfinder(1000, 0f);
            List<Vector2Int> path = new List<Vector2Int>();

            Assert.That(finder.FindPath(grid, new Vector2Int(1, 1), new CellGoal(new Vector2Int(8, 1)), path), Is.True);
            Assert.That(grid.NeedsJump(4, 1, 5, 1), Is.True);
            Assert.That(grid.NeedsJump(5, 1, 6, 1), Is.False);
        }

        [Test]
        public void Pathfinder_TooHighToJump_IsAWall()
        {
            NavGrid grid = JumpingGrid(10);
            for (int z = 0; z < 10; z++)
            {
                grid.SetStatic(5, z, false, 1.5f);
            }

            GridPathfinder finder = new GridPathfinder(1000, 0f);

            Assert.That(finder.FindPath(grid, new Vector2Int(1, 1), new CellGoal(new Vector2Int(8, 1)), new List<Vector2Int>()), Is.False);
        }

        [Test]
        public void Pathfinder_SteepSlope_IsNotJumpedUp()
        {
            NavGrid grid = JumpingGrid(10);
            for (int x = 5; x < 10; x++)
            {
                for (int z = 0; z < 10; z++)
                {
                    // Rises 0.8 m every cell: each rise could be jumped, but the slope never levels off.
                    grid.SetStatic(x, z, false, (x - 4) * 0.8f);
                }
            }

            Assert.That(grid.CanStep(4, 1, 5, 1), Is.False);
            Assert.That(grid.CanStep(8, 1, 9, 1), Is.True, "the top cell: nothing higher beyond it");
        }

        [Test]
        public void Pathfinder_Smooth_KeepsOnlyTheTurns()
        {
            NavGrid grid = OpenGrid(10);
            GridPathfinder finder = new GridPathfinder(1000, 0f);
            List<Vector2Int> path = new List<Vector2Int>();
            List<Vector2Int> smooth = new List<Vector2Int>();
            finder.FindPath(grid, new Vector2Int(0, 0), new CellGoal(new Vector2Int(9, 3)), path);

            GridPathfinder.Smooth(grid, path, smooth);

            Assert.That(smooth.Count, Is.EqualTo(2), "open ground: one straight line");
            Assert.That(smooth[1], Is.EqualTo(new Vector2Int(9, 3)));
        }

        [Test]
        public void NavGrid_BlockedForAWhile_ThenFreeAgain()
        {
            NavGrid grid = OpenGrid(4);
            grid.Now = 10f;

            grid.BlockUntil(2, 2, 15f);

            Assert.That(grid.IsWalkable(2, 2), Is.False);
            grid.Now = 15f;
            Assert.That(grid.IsWalkable(2, 2), Is.True);
        }

        [Test]
        public void NavGrid_ReadsAshHeights_AndCatchesUpWithDigs()
        {
            VoxelWorld world = CreateWorld();
            Fill(world, 1, 1, 31, 31, 1f);
            NavGrid grid = OpenGrid(8);
            grid.AttachTerrain(world, Vector3.zero, 0.05f);

            grid.RefreshTerrain();
            Assert.That(grid.HeightAt(2, 2), Is.EqualTo(1f).Within(0.15f));

            Fill(world, 1, 1, 31, 31, 0.5f);
            grid.RefreshTerrain();
            Assert.That(grid.HeightAt(2, 2), Is.EqualTo(0.5f).Within(0.15f), "the chunk changed, so its cells were read again");
        }

        [Test]
        public void ReachGoal_StandsInARingAroundTheTarget()
        {
            NavGrid grid = OpenGrid(10);
            ReachGoal goal = new ReachGoal();
            goal.Set(grid, new Vector3(5.5f, 0.5f, 5.5f), 0.9f, 1.6f, 1.4f, 1.2f);

            Assert.That(goal.IsGoal(5, 5), Is.False, "not on the spot itself");
            Assert.That(goal.IsGoal(6, 5), Is.True);
            Assert.That(goal.IsGoal(9, 5), Is.False, "too far");
            Assert.That(goal.Estimate(9, 5), Is.GreaterThan(0f));
        }

        [Test]
        public void TableUpgrade_PricesAndValuesPerLevel()
        {
            TableUpgrade upgrade = ScriptableObject.CreateInstance<TableUpgrade>();
            try
            {
                Assert.That(upgrade.MaxLevel, Is.EqualTo(0));
                Assert.That(upgrade.ValueAt(0), Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(upgrade);
            }
        }
    }
}
