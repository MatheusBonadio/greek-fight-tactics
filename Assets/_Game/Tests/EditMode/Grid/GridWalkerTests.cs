using Game.Core;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Grid
{
    public sealed class GridWalkerTests
    {
        private const int ActorId = 11;
        private const int OtherActorId = 22;
        private const float Tolerance = 0.0001f;

        private GridModel grid;

        [SetUp]
        public void SetUp()
        {
            grid = GridFactory.Open(8, 8);
        }

        [Test]
        public void Constructor_OccupiesTheStartCell()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(2, 2));

            Assert.AreEqual(ActorId, grid[new Coord(2, 2)].OccupantId);
            Assert.IsFalse(walker.IsMoving);
        }

        [Test]
        public void Step_TakesQuarterSecond_WithLinearInterpolation()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(2, 2));
            var from = IsoMath.GridToWorld(new Coord(2, 2));
            var to = IsoMath.GridToWorld(new Coord(3, 2));

            walker.Follow(new[] { new Coord(3, 2) });

            walker.Advance(GridWalker.SecondsPerCell * 0.25f);
            AssertClose(Vector2.Lerp(from, to, 0.25f), walker.WorldPosition);
            walker.Advance(GridWalker.SecondsPerCell * 0.25f);
            AssertClose(Vector2.Lerp(from, to, 0.5f), walker.WorldPosition);
            Assert.IsTrue(walker.IsStepping);

            walker.Advance(GridWalker.SecondsPerCell * 0.5f);
            AssertClose(to, walker.WorldPosition);
            Assert.IsFalse(walker.IsMoving);
        }

        [Test]
        public void Occupancy_MovesAtTheStartOfEachStep_NeverHalfway()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(2, 2));

            walker.Follow(new[] { new Coord(3, 2), new Coord(4, 2) });

            Assert.AreEqual(TileData.NoOccupant, grid[new Coord(2, 2)].OccupantId);
            Assert.AreEqual(ActorId, grid[new Coord(3, 2)].OccupantId);
            Assert.AreEqual(TileData.NoOccupant, grid[new Coord(4, 2)].OccupantId);

            walker.Advance(GridWalker.SecondsPerCell * 0.9f);
            Assert.AreEqual(ActorId, grid[new Coord(3, 2)].OccupantId);

            walker.Advance(GridWalker.SecondsPerCell * 0.2f);
            Assert.AreEqual(TileData.NoOccupant, grid[new Coord(3, 2)].OccupantId);
            Assert.AreEqual(ActorId, grid[new Coord(4, 2)].OccupantId);
            Assert.AreEqual(new Coord(4, 2), walker.Cell);
        }

        [Test]
        public void LargeDelta_WalksSeveralCellsAndCarriesTheRemainder()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(0, 0));

            walker.Follow(new[] { new Coord(1, 0), new Coord(2, 0), new Coord(3, 0) });
            walker.Advance(GridWalker.SecondsPerCell * 2.5f);

            Assert.AreEqual(new Coord(3, 0), walker.Cell);
            AssertClose(Vector2.Lerp(IsoMath.GridToWorld(new Coord(2, 0)), IsoMath.GridToWorld(new Coord(3, 0)), 0.5f), walker.WorldPosition);
        }

        [Test]
        public void Cancel_FinishesTheCurrentStepAndDropsTheRest()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(0, 0));
            walker.Follow(new[] { new Coord(1, 0), new Coord(2, 0), new Coord(3, 0) });

            walker.Advance(GridWalker.SecondsPerCell * 0.5f);
            walker.Cancel();
            walker.Advance(GridWalker.SecondsPerCell * 4f);

            Assert.AreEqual(new Coord(1, 0), walker.Cell);
            Assert.IsFalse(walker.IsMoving);
            Assert.AreEqual(ActorId, grid[new Coord(1, 0)].OccupantId);
        }

        [TestCase(1, 0, Facing.NorthEast)]
        [TestCase(-1, 0, Facing.SouthWest)]
        [TestCase(0, 1, Facing.NorthWest)]
        [TestCase(0, -1, Facing.SouthEast)]
        public void Facing_FollowsTheScreenDirectionOfTheStep(int deltaX, int deltaY, Facing expected)
        {
            var start = new Coord(4, 4);
            var walker = new GridWalker(grid, ActorId, start);

            walker.Follow(new[] { start + new Coord(deltaX, deltaY) });

            Assert.AreEqual(expected, walker.Facing);
            var screenStep = IsoMath.GridToWorld(walker.Cell) - IsoMath.GridToWorld(start);
            Assert.AreEqual(expected is Facing.NorthEast or Facing.NorthWest, screenStep.y > 0);
            Assert.AreEqual(expected is Facing.NorthEast or Facing.SouthEast, screenStep.x > 0);
        }

        [Test]
        public void Face_TurnsAStandingActorAndWaitsOutAStep()
        {
            var start = new Coord(4, 4);
            var walker = new GridWalker(grid, ActorId, start, Facing.SouthEast);

            walker.Follow(new[] { start + new Coord(1, 0) });
            walker.Face(Facing.SouthWest);
            Assert.AreEqual(Facing.NorthEast, walker.Facing);

            walker.Advance(GridWalker.SecondsPerCell);
            walker.Face(Facing.SouthWest);
            Assert.AreEqual(Facing.SouthWest, walker.Facing);
        }

        [TestCase(Facing.SouthEast, false, Facing.SouthEast)]
        [TestCase(Facing.NorthEast, false, Facing.NorthEast)]
        [TestCase(Facing.SouthWest, true, Facing.SouthEast)]
        [TestCase(Facing.NorthWest, true, Facing.NorthEast)]
        public void WestFacings_AreMirroredFromTheEastRows(Facing facing, bool mirrored, Facing sheetRow)
        {
            Assert.AreEqual(mirrored, FacingRules.IsMirrored(facing));
            Assert.AreEqual(sheetRow, FacingRules.SheetFacing(facing));
        }

        [Test]
        public void BlockedStep_StopsWithoutEnteringTheCell()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(0, 0));
            walker.Follow(new[] { new Coord(1, 0), new Coord(2, 0) });
            GridFactory.Occupy(grid, new Coord(2, 0), OtherActorId);

            walker.Advance(GridWalker.SecondsPerCell * 3f);

            Assert.AreEqual(new Coord(1, 0), walker.Cell);
            Assert.IsTrue(walker.WasBlocked);
            Assert.IsFalse(walker.IsMoving);
            Assert.AreEqual(OtherActorId, grid[new Coord(2, 0)].OccupantId);
        }

        [Test]
        public void NonAdjacentPath_IsRejected()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(0, 0));

            walker.Follow(new[] { new Coord(2, 0) });

            Assert.IsTrue(walker.WasBlocked);
            Assert.AreEqual(new Coord(0, 0), walker.Cell);
        }

        private static void AssertClose(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance);
            Assert.AreEqual(expected.y, actual.y, Tolerance);
        }
    }
}
