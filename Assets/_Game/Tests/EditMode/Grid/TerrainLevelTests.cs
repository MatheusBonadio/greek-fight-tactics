using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Grid
{
    public sealed class TerrainLevelTests
    {
        private const int ActorId = 11;
        private const int ArtPixelsPerUnit = 16;
        private const float Tolerance = 0.0001f;

        private PathFinder finder;
        private List<Coord> path;

        [SetUp]
        public void SetUp()
        {
            finder = new PathFinder();
            path = new List<Coord>();
        }

        [Test]
        public void OneLevel_IsEightPixelsAndLiftsTheSurface()
        {
            Assert.AreEqual(IsoMath.LevelPixels, IsoMath.LevelHeight * ArtPixelsPerUnit);
            Assert.AreEqual(IsoMath.GridToWorld(new Coord(3, 2)) + new Vector2(0f, 2f * IsoMath.LevelHeight), IsoMath.GridToSurface(new Coord(3, 2), 2));
        }

        [Test]
        public void Steps_ClimbOneLevelAtATimeAndPayForGoingUp()
        {
            var low = new TileData { Cost = 1, Level = 0 };
            var step = new TileData { Cost = 1, Level = 1 };
            var cliff = new TileData { Cost = 1, Level = 2 };

            Assert.IsTrue(TerrainSteps.CanStep(low, step));
            Assert.IsTrue(TerrainSteps.CanStep(step, low));
            Assert.IsFalse(TerrainSteps.CanStep(low, cliff));
            Assert.IsFalse(TerrainSteps.CanStep(cliff, low));
            Assert.AreEqual(1 + TerrainSteps.ClimbCost, TerrainSteps.StepCost(low, step), "climbing costs one more");
            Assert.AreEqual(1, TerrainSteps.StepCost(step, low), "going down costs the terrain only");
        }

        [Test]
        public void FindPath_ClimbsThePlateauByItsRampAndNeverUpACliff()
        {
            var grid = GridFactory.Open(4, 4);
            for (var y = 0; y < 4; y++)
            {
                GridFactory.Raise(grid, new Coord(2, y), 2);
                GridFactory.Raise(grid, new Coord(3, y), 2);
            }

            GridFactory.Raise(grid, new Coord(1, 0), 1);

            Assert.IsTrue(finder.FindPath(grid, new Coord(0, 3), new Coord(3, 3), path));
            CollectionAssert.Contains(path, new Coord(1, 0), "the only way up is the ramp");
            AssertEveryStepIsWalkable(grid, new Coord(0, 3), path);

            GridFactory.Raise(grid, new Coord(1, 0), 0);
            Assert.IsFalse(finder.FindPath(grid, new Coord(0, 3), new Coord(3, 3), path), "without the ramp the plateau is out of reach");
        }

        [Test]
        public void Reachable_CountsTheClimbAgainstTheMoveRange()
        {
            var grid = GridFactory.Open(5, 1);
            GridFactory.Raise(grid, new Coord(1, 0), 1);
            GridFactory.Raise(grid, new Coord(2, 0), 2);
            GridFactory.Raise(grid, new Coord(3, 0), 2);
            GridFactory.Raise(grid, new Coord(4, 0), 1);
            var cells = new List<Coord>();

            finder.Reachable(grid, new Coord(0, 0), 3, cells);
            CollectionAssert.AreEquivalent(new[] { new Coord(1, 0) }, cells, "each climb costs two, so the second one is out of range");

            finder.Reachable(grid, new Coord(2, 0), 3, cells);
            CollectionAssert.AreEquivalent(new[] { new Coord(1, 0), new Coord(0, 0), new Coord(3, 0), new Coord(4, 0) }, cells, "going down and along costs one per cell");
        }

        [Test]
        public void FindPathBeside_EndsWhereTheTargetIsOneStepAwayAndNotAtTheFootOfACliff()
        {
            var grid = GridFactory.Open(5, 5);
            var target = new Coord(2, 2);
            GridFactory.Raise(grid, target, 2);
            GridFactory.Raise(grid, new Coord(3, 2), 1);
            GridFactory.Occupy(grid, target, ActorId);

            Assert.IsTrue(finder.FindPathBeside(grid, new Coord(0, 2), target, path));
            Assert.AreEqual(new Coord(3, 2), path[^1], "the only side one step away is the ledge");
            AssertEveryStepIsWalkable(grid, new Coord(0, 2), path);
            Assert.IsFalse(PathFinder.AreBesideOnFoot(grid, new Coord(1, 2), target), "the foot of the cliff is not beside on foot");
            Assert.IsTrue(PathFinder.AreBesideOnFoot(grid, new Coord(3, 2), target));
        }

        [Test]
        public void Walker_StopsBeforeACliffAndRisesWithTheStep()
        {
            var grid = GridFactory.Open(4, 1);
            GridFactory.Raise(grid, new Coord(1, 0), 1);
            GridFactory.Raise(grid, new Coord(2, 0), 3);
            var walker = new GridWalker(grid, ActorId, new Coord(0, 0));

            walker.Follow(new[] { new Coord(1, 0), new Coord(2, 0) });
            walker.Advance(GridWalker.SecondsPerCell * 0.5f);
            Assert.AreEqual(0.5f, walker.Level, Tolerance, "the level follows the step");
            Assert.AreEqual(walker.WorldPosition.y + IsoMath.Lift(0.5f), walker.SurfacePosition.y, Tolerance);

            walker.Advance(GridWalker.SecondsPerCell * 2f);
            Assert.AreEqual(new Coord(1, 0), walker.Cell);
            Assert.IsTrue(walker.WasBlocked, "two levels up is a cliff");
            Assert.AreEqual(1f, walker.Level, Tolerance);
        }

        [Test]
        public void StepsFrom_NeverCrossesACliff()
        {
            var grid = GridFactory.Open(6, 1);
            GridFactory.Raise(grid, new Coord(2, 0), 2);
            GridFactory.Raise(grid, new Coord(3, 0), 2);

            var steps = DuelCrowdPlanner.StepsFrom(grid, new Coord(0, 0), _ => true);
            CollectionAssert.AreEqual(new[] { 0, 1, DuelCrowdPlanner.Unreachable, DuelCrowdPlanner.Unreachable, DuelCrowdPlanner.Unreachable, DuelCrowdPlanner.Unreachable }, steps);

            GridFactory.Raise(grid, new Coord(1, 0), 1);
            GridFactory.Raise(grid, new Coord(4, 0), 1);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, DuelCrowdPlanner.StepsFrom(grid, new Coord(0, 0), _ => true), "a ramp on each side opens the way");
        }

        [Test]
        public void Arena_FramesTheDuelOnTheLevelOfTheDebaters()
        {
            var grid = GridFactory.Open(16, 16);
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var cell = new Coord(x, y);
                    var tile = grid[cell];
                    tile.Revealed = true;
                    tile.Level = 2;
                    grid.TrySet(cell, tile);
                }
            }

            var arena = DuelCrowdPlanner.Plan(grid, new Coord(7, 8), new Coord(8, 8), new CrowdRules(), 1);

            Assert.AreEqual(2f, arena.CenterLevel);
            Assert.AreEqual((7.5f + 8f) * IsoMath.CellHeight + IsoMath.Lift(2f), arena.WorldCenter.y, Tolerance);
        }

        [Test]
        public void Picking_OnFlatGroundIsTheDiamondUnderThePointer()
        {
            var grid = GridFactory.Open(6, 6);
            foreach (var offset in new[] { Vector2.zero, new Vector2(0.4f, 0.1f), new Vector2(-0.6f, -0.1f) })
            {
                var world = IsoMath.GridToWorld(new Coord(3, 2)) + offset;
                Assert.AreEqual(IsoMath.WorldToGrid(world), TerrainPicking.CellAt(grid, world));
            }
        }

        [Test]
        public void Picking_FindsTheRaisedTopItsCliffAndWhatItHides()
        {
            var grid = GridFactory.Open(6, 6);
            var raised = new Coord(3, 3);
            GridFactory.Raise(grid, raised, 2);

            Assert.AreEqual(raised, TerrainPicking.CellAt(grid, IsoMath.GridToSurface(raised, 2)), "the top of the raised cell");
            Assert.AreEqual(raised, TerrainPicking.CellAt(grid, IsoMath.GridToWorld(raised)), "its cliff hides where its ground would be");
            Assert.AreEqual(raised, TerrainPicking.CellAt(grid, IsoMath.GridToWorld(new Coord(4, 3)) - new Vector2(0.5f, 0f)), "the cliff in front hides part of the cell behind it");
            Assert.AreEqual(new Coord(4, 3), TerrainPicking.CellAt(grid, IsoMath.GridToWorld(new Coord(4, 3)) + new Vector2(0.5f, 0f)), "the rest of that cell still shows");

            var cursor = new GridCursor(grid, new Coord(0, 0));
            Assert.IsTrue(cursor.TryHover(IsoMath.GridToSurface(raised, 2), Vector2.zero));
            Assert.AreEqual(raised, cursor.Cell);
            Assert.IsTrue(cursor.TryPoint(new Coord(1, 1)));
            Assert.AreEqual(new Coord(1, 1), cursor.Cell);
            Assert.IsFalse(cursor.TryPoint(new Coord(9, 9)));
        }

        [Test]
        public void Sorting_AnActorInFrontOfARaisedCellIsDrawnOverItsCliffAndTop()
        {
            var raised = new Coord(4, 4);
            foreach (var front in new[] { new Coord(3, 4), new Coord(4, 3) })
            {
                var actor = StandingActor(front, 0);
                Assert.Less(actor, Floor(raised, 2), $"actor on {front} over the raised top");
                Assert.Less(actor, Face(raised, 2), $"actor on {front} over the upper cliff");
                Assert.Less(actor, Face(raised, 1), $"actor on {front} over the lower cliff");
            }
        }

        [Test]
        public void Sorting_ARaisedCellHidesTheActorBehindIt()
        {
            var raised = new Coord(4, 4);
            foreach (var back in new[] { new Coord(5, 4), new Coord(4, 5), new Coord(5, 5) })
            {
                foreach (var level in new[] { 0, 1 })
                {
                    var actor = StandingActor(back, level);
                    Assert.Greater(actor, Floor(raised, 2), $"the raised top covers the actor on {back} at level {level}");
                    Assert.Greater(actor, Face(raised, 2), $"the cliff covers the actor on {back} at level {level}");
                }
            }
        }

        [Test]
        public void Sorting_AnActorOnARaisedCellStandsOverItsFloorAndItsOverlays()
        {
            var cell = new Coord(4, 4);
            var actor = StandingActor(cell, 2);
            foreach (var offset in new[] { TerrainSorting.FloorOffset, TerrainSorting.ExitOffset, TerrainSorting.FloorVeilOffset, TerrainSorting.RangeOffset, TerrainSorting.HighlightOffset, TerrainSorting.FogOffset })
            {
                Assert.Less(actor, TerrainSorting.TileSortValue(cell, 2, offset), $"offset {offset}");
            }

            Assert.Less(TerrainSorting.FogOffset, TerrainSorting.HighlightOffset, "the fog covers the cursor");
            Assert.Less(TerrainSorting.HighlightOffset, TerrainSorting.RangeOffset, "the cursor covers the range");
            Assert.Less(TerrainSorting.RangeOffset, TerrainSorting.FloorVeilOffset, "the range stays readable over the veil");
            Assert.Less(TerrainSorting.FloorVeilOffset, TerrainSorting.ExitOffset, "the veil darkens the exit arrow");
            Assert.Less(TerrainSorting.ExitOffset, TerrainSorting.FloorOffset, "the arrow lies on the floor");
        }

        [Test]
        public void Sorting_AnActorWalkingOverARaisedPlateauNeverSinksUnderTheFloor()
        {
            var grid = GridFactory.Open(8, 8);
            for (var y = 2; y < 6; y++)
            {
                for (var x = 2; x < 6; x++)
                {
                    GridFactory.Raise(grid, new Coord(x, y), 2);
                }
            }

            foreach (var (from, to) in new[] { (new Coord(4, 4), new Coord(3, 4)), (new Coord(3, 4), new Coord(4, 4)), (new Coord(4, 4), new Coord(4, 3)), (new Coord(3, 3), new Coord(3, 4)) })
            {
                var walker = new GridWalker(grid, ActorId, from);
                walker.Follow(new[] { to });
                for (var step = 0; step <= 10; step++)
                {
                    var actor = WalkingActor(walker);
                    if (walker.IsStepping)
                    {
                        Assert.Less(actor, Floor(from, 2), $"{from} to {to} at {step}");
                    }

                    Assert.Less(actor, Floor(to, 2), $"{from} to {to} at {step}");
                    Assert.Less(actor, TerrainSorting.TileSortValue(to, 2, TerrainSorting.FogOffset), $"{from} to {to} at {step}: the overlays stay under the feet");
                    walker.Advance(GridWalker.SecondsPerCell * 0.1f);
                }

                walker.Teleport(from, Facing.SouthEast);
                GridFactory.Occupy(grid, from, TileData.NoOccupant);
            }
        }

        [Test]
        public void Sorting_AnActorClimbingOntoALedgeStaysInFrontOfItsCliff()
        {
            var grid = GridFactory.Open(6, 6);
            GridFactory.Raise(grid, new Coord(2, 2), 1);
            GridFactory.Raise(grid, new Coord(3, 2), 2);
            var walker = new GridWalker(grid, ActorId, new Coord(2, 2));

            walker.Follow(new[] { new Coord(3, 2) });
            for (var step = 0; step <= 10; step++)
            {
                var actor = WalkingActor(walker);
                Assert.Less(actor, Face(new Coord(3, 2), 2), $"at {step}");
                Assert.Less(actor, Floor(new Coord(3, 2), 2), $"at {step}");
                if (walker.IsStepping)
                {
                    Assert.Less(actor, Floor(new Coord(2, 2), 1), $"at {step}");
                }

                walker.Advance(GridWalker.SecondsPerCell * 0.1f);
            }
        }

        [Test]
        public void Sorting_AHigherCellBesideTheStepStaysInFrontOfTheWalker()
        {
            var grid = GridFactory.Open(8, 8);
            var higher = new Coord(5, 4);
            GridFactory.Raise(grid, higher, 1);
            var walker = new GridWalker(grid, ActorId, new Coord(5, 5));

            walker.Follow(new[] { new Coord(4, 5) });
            for (var step = 0; step <= 10; step++)
            {
                Assert.Greater(WalkingActor(walker), Floor(higher, 1), $"at {step} the ledge in front still covers the feet");
                walker.Advance(GridWalker.SecondsPerCell * 0.1f);
            }
        }

        [Test]
        public void Sorting_AColumnOnTopOfACliffNeverCoversTheActorBelow()
        {
            foreach (var level in Enumerable.Range(1, TerrainSorting.MaxLevel))
            {
                Assert.Greater(Block(new Coord(5, 4), level), StandingActor(new Coord(4, 4), 0), $"a column on a level {level} cell behind the actor");
                Assert.Less(Block(new Coord(3, 4), level), StandingActor(new Coord(4, 4), 0), $"a column on a level {level} cell in front of the actor");
                Assert.Less(Block(new Coord(4, 4), level), Floor(new Coord(4, 4), level), $"a column on level {level} stands on its floor");
            }
        }

        [Test]
        public void Sorting_ARaisedFloorCoversTheBaseOfAColumnBehindIt()
        {
            Assert.Less(Floor(new Coord(4, 4), 1), Block(new Coord(5, 4), 0));
            Assert.Less(Floor(new Coord(4, 4), 2), Block(new Coord(5, 4), 1));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void Sorting_KeepsTheFourHardCasesOnEveryLevel(int level)
        {
            var actor = new Coord(4, 4);
            Assert.Less(Block(new Coord(3, 4), level), StandingActor(actor, level), "a prop in front covers the actor");
            Assert.Greater(Block(new Coord(5, 4), level), StandingActor(actor, level), "a prop behind stays behind");

            var diagonal = new[] { new Coord(2, 2), new Coord(3, 3), new Coord(4, 4) }.Select(cell => StandingActor(cell, level)).ToList();
            Assert.Less(diagonal[0], diagonal[1], "three actors in a diagonal are drawn back to front");
            Assert.Less(diagonal[1], diagonal[2]);

            Assert.Less(Block(new Coord(4, 4), level), StandingActor(new Coord(5, 5), level), "a tall column covers the actor behind it");
            Assert.Less(Block(new Coord(4, 4), level), StandingActor(new Coord(6, 4), level), "a block two cells tall covers the actor two cells behind");
        }

        private static float Floor(Coord cell, int level) => TerrainSorting.TileSortValue(cell, level, TerrainSorting.FloorOffset);

        private static float Face(Coord cell, int level) => TerrainSorting.TileSortValue(cell, level, TerrainSorting.FaceOffset, Game.EditorTools.SandboxTools.FaceSpriteDrop);

        private static float Block(Coord cell, int level) => TerrainSorting.TileSortValue(cell, level, TerrainSorting.BlockOffset);

        private static float StandingActor(Coord cell, int level) => TerrainSorting.ActorSortValue(IsoMath.GridToWorld(cell), level, IsoMath.Depth(cell));

        private static float WalkingActor(GridWalker walker) => walker.SurfacePosition.y - walker.SortDepth;

        private static void AssertEveryStepIsWalkable(GridModel grid, Coord from, IEnumerable<Coord> steps)
        {
            var previous = from;
            foreach (var cell in steps)
            {
                Assert.IsTrue(TerrainSteps.CanStep(grid, previous, cell), $"{previous} to {cell}");
                previous = cell;
            }
        }
    }
}
