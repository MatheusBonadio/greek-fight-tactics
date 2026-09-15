using Game.Core;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Grid
{
    public sealed class FogTileRulesTests
    {
        private GridModel grid;

        [SetUp]
        public void SetUp()
        {
            grid = GridFactory.Open(5, 5);
            new FogOfWar(grid).Reveal(new Coord(2, 2), 1);
        }

        [TestCase(2, 2, FogTileKind.None)]
        [TestCase(3, 2, FogTileKind.None)]
        [TestCase(4, 2, FogTileKind.EdgeSouthWest)]
        [TestCase(0, 2, FogTileKind.EdgeNorthEast)]
        [TestCase(2, 4, FogTileKind.EdgeSouthEast)]
        [TestCase(2, 0, FogTileKind.EdgeNorthWest)]
        [TestCase(4, 4, FogTileKind.Full)]
        [TestCase(0, 0, FogTileKind.Full)]
        [TestCase(-1, 2, FogTileKind.None)]
        public void KindAt_PicksTheEdgeFacingTheRevealedArea(int x, int y, FogTileKind expected)
        {
            Assert.AreEqual(expected, FogTileRules.KindAt(grid, new Coord(x, y)));
        }
    }
}
