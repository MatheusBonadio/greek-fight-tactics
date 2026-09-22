using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Grid
{
    public sealed class CellGateTests
    {
        private static readonly Coord Gate = new(3, 3);
        private static readonly Coord Start = new(0, 0);

        [Test]
        public void ClosedGate_StaysUnderTheFogAndCannotBeWalkedInto()
        {
            var grid = GridFactory.Open(5, 5);
            var fog = new FogOfWar(grid);
            fog.Reveal(Gate, 5);

            Assert.IsTrue(CellGate.Close(grid, fog, Gate));
            Assert.IsTrue(CellGate.IsClosed(grid, fog, Gate));
            Assert.IsFalse(grid[Gate].Revealed, "closing hides the cell again");
            Assert.IsFalse(grid[Gate].IsPassable);
            Assert.IsFalse(new PathFinder().FindPath(grid, Start, Gate, new List<Coord>()), "nobody walks into a closed gate");

            Assert.AreEqual(0, fog.Reveal(Gate), "walking beside it never lifts its fog");
            Assert.IsFalse(grid[Gate].Revealed);

            var bitmask = fog.ToBitmask();
            var everything = new byte[bitmask.Length];
            for (var index = 0; index < everything.Length; index++)
            {
                everything[index] = byte.MaxValue;
            }

            Assert.IsTrue(fog.TryLoadBitmask(everything));
            Assert.IsFalse(grid[Gate].Revealed, "an old save that saw the cell does not open it");
        }

        [Test]
        public void OpenGate_ComesOutOfTheFogAndLeadsOn()
        {
            var grid = GridFactory.Open(5, 5);
            var fog = new FogOfWar(grid);
            var cost = grid[Gate].Cost;
            CellGate.Close(grid, fog, Gate);

            Assert.IsTrue(CellGate.Open(grid, fog, Gate, cost));
            Assert.IsFalse(CellGate.IsClosed(grid, fog, Gate));
            Assert.IsFalse(fog.IsHeld(Gate));
            Assert.IsTrue(grid[Gate].Revealed, "the road comes out of the fog");
            Assert.AreEqual(cost, grid[Gate].Cost);
            Assert.IsTrue(new PathFinder().FindPath(grid, Start, Gate, new List<Coord>()));
        }

        [Test]
        public void Gate_OutsideTheGridIsIgnored()
        {
            var grid = GridFactory.Open(2, 2);
            var fog = new FogOfWar(grid);

            Assert.IsFalse(CellGate.Close(grid, fog, Gate));
            Assert.IsFalse(CellGate.Open(grid, fog, Gate, PathFinder.MinimumStepCost));
            Assert.IsEmpty(fog.Held);
        }
    }
}
