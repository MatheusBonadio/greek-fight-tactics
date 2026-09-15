using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using AllocationIs = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.EditMode.Grid
{
    public sealed class PathFinderTests
    {
        private const int OccupantId = 7;

        private PathFinder finder;
        private List<Coord> buffer;

        [SetUp]
        public void SetUp()
        {
            finder = new PathFinder();
            buffer = new List<Coord>();
        }

        [Test]
        public void FindPath_OnOpenGround_ReturnsCellsAfterStartUpToGoal()
        {
            var grid = GridFactory.Open(5, 5);

            Assert.IsTrue(finder.FindPath(grid, new Coord(0, 0), new Coord(3, 0), buffer));
            CollectionAssert.AreEqual(new[] { new Coord(1, 0), new Coord(2, 0), new Coord(3, 0) }, buffer);
        }

        [Test]
        public void FindPath_UsesOnlyOrthogonalSteps()
        {
            var grid = GridFactory.Open(6, 6);
            var from = new Coord(0, 0);

            Assert.IsTrue(finder.FindPath(grid, from, new Coord(4, 3), buffer));
            Assert.AreEqual(7, buffer.Count);

            var previous = from;
            foreach (var cell in buffer)
            {
                Assert.AreEqual(1, System.Math.Abs(cell.X - previous.X) + System.Math.Abs(cell.Y - previous.Y));
                previous = cell;
            }
        }

        [Test]
        public void FindPath_WalksAroundExpensiveTerrain()
        {
            var grid = GridFactory.Open(3, 3);
            GridFactory.SetCost(grid, new Coord(1, 1), 10);

            Assert.IsTrue(finder.FindPath(grid, new Coord(0, 1), new Coord(2, 1), buffer));
            CollectionAssert.DoesNotContain(buffer, new Coord(1, 1));
            Assert.AreEqual(4, buffer.Count);
        }

        [Test]
        public void FindPath_AvoidsImpassableAndOccupiedCells()
        {
            var grid = GridFactory.Open(3, 3);
            GridFactory.Block(grid, new Coord(1, 1));
            GridFactory.Occupy(grid, new Coord(1, 0), OccupantId);

            Assert.IsTrue(finder.FindPath(grid, new Coord(0, 0), new Coord(2, 0), buffer));
            CollectionAssert.DoesNotContain(buffer, new Coord(1, 1));
            CollectionAssert.DoesNotContain(buffer, new Coord(1, 0));
        }

        [Test]
        public void FindPath_WithoutRoute_ReturnsFalseAndEmptyPath()
        {
            var grid = GridFactory.Open(5, 5);
            for (var y = 0; y < 5; y++)
            {
                GridFactory.Block(grid, new Coord(2, y));
            }

            buffer.Add(new Coord(9, 9));

            Assert.IsFalse(finder.FindPath(grid, new Coord(0, 0), new Coord(4, 4), buffer));
            Assert.IsEmpty(buffer);
        }

        [Test]
        public void FindPath_ToOccupiedOrOutsideCell_ReturnsFalse()
        {
            var grid = GridFactory.Open(4, 4);
            GridFactory.Occupy(grid, new Coord(3, 3), OccupantId);

            Assert.IsFalse(finder.FindPath(grid, new Coord(0, 0), new Coord(3, 3), buffer));
            Assert.IsFalse(finder.FindPath(grid, new Coord(0, 0), new Coord(4, 0), buffer));
            Assert.IsFalse(finder.FindPath(grid, new Coord(-1, 0), new Coord(1, 0), buffer));
        }

        [Test]
        public void FindPath_FromOccupiedStart_StillLeavesTheCell()
        {
            var grid = GridFactory.Open(3, 1);
            GridFactory.Occupy(grid, new Coord(0, 0), OccupantId);

            Assert.IsTrue(finder.FindPath(grid, new Coord(0, 0), new Coord(2, 0), buffer));
            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void FindPath_ToSameCell_ReturnsTrueWithEmptyPath()
        {
            var grid = GridFactory.Open(2, 2);

            Assert.IsTrue(finder.FindPath(grid, new Coord(1, 1), new Coord(1, 1), buffer));
            Assert.IsEmpty(buffer);
        }

        [Test]
        public void FindPath_ThroughSerpentine_FindsTheOnlyRoute()
        {
            var grid = GridFactory.Serpentine(9);

            Assert.IsTrue(finder.FindPath(grid, new Coord(0, 0), new Coord(8, 8), buffer));
            Assert.AreEqual(new Coord(8, 8), buffer[buffer.Count - 1]);
            foreach (var cell in buffer)
            {
                Assert.IsTrue(grid[cell].IsPassable);
            }
        }

        [Test]
        public void Reachable_OnOpenGround_ReturnsDiamondWithoutTheStart()
        {
            var grid = GridFactory.Open(7, 7);
            var center = new Coord(3, 3);

            finder.Reachable(grid, center, 2, buffer);

            Assert.AreEqual(12, buffer.Count);
            CollectionAssert.DoesNotContain(buffer, center);
            foreach (var cell in buffer)
            {
                Assert.LessOrEqual(System.Math.Abs(cell.X - center.X) + System.Math.Abs(cell.Y - center.Y), 2);
            }
        }

        [Test]
        public void Reachable_CountsTerrainCostAgainstTheLimit()
        {
            var grid = GridFactory.Open(5, 1);
            GridFactory.SetCost(grid, new Coord(1, 0), 3);

            finder.Reachable(grid, new Coord(0, 0), 3, buffer);

            CollectionAssert.AreEquivalent(new[] { new Coord(1, 0) }, buffer);
        }

        [Test]
        public void Reachable_WithNonPositiveLimit_ReturnsNothing()
        {
            var grid = GridFactory.Open(3, 3);

            finder.Reachable(grid, new Coord(1, 1), 0, buffer);

            Assert.IsEmpty(buffer);
        }

        [Test]
        public void RepeatedSearches_DoNotAllocate()
        {
            var grid = GridFactory.Serpentine(32);
            var from = new Coord(0, 0);
            var to = new Coord(31, 31);
            finder.FindPath(grid, from, to, buffer);
            finder.Reachable(grid, from, 40, buffer);

            Assert.That(() => { finder.FindPath(grid, from, to, buffer); }, AllocationIs.Not.AllocatingGCMemory());
            Assert.That(() => { finder.Reachable(grid, from, 40, buffer); }, AllocationIs.Not.AllocatingGCMemory());
        }
    }
}
