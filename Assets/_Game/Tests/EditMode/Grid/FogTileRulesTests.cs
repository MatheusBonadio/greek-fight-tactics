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

        [TestCase(4, 2)]
        [TestCase(0, 2)]
        [TestCase(2, 4)]
        [TestCase(2, 0)]
        public void KindAt_ACliffToTheRevealedNeighbour_FillsTheCellInsteadOfDitheringIt(int x, int y)
        {
            var cell = new Coord(x, y);
            Assert.AreNotEqual(FogTileKind.Full, FogTileRules.KindAt(grid, cell), "level ground dithers");

            GridFactory.Raise(grid, cell, TerrainSteps.MaxClimb);
            Assert.AreNotEqual(FogTileKind.Full, FogTileRules.KindAt(grid, cell), "one step is still walkable, so it still dithers");

            GridFactory.Raise(grid, cell, TerrainSteps.MaxClimb + 1);
            Assert.AreEqual(FogTileKind.Full, FogTileRules.KindAt(grid, cell), "a cliff fills the cell");
        }

        [Test]
        public void KindAt_TheDitherFacesTheRevealedNeighbourItCanBeReachedFrom()
        {
            var cell = new Coord(2, 0);
            GridFactory.Raise(grid, cell, TerrainSteps.MaxClimb + 1);
            Assert.AreEqual(FogTileKind.Full, FogTileRules.KindAt(grid, cell), "the only revealed neighbour is a cliff below");

            GridFactory.Raise(grid, new Coord(2, 1), TerrainSteps.MaxClimb + 1);
            Assert.AreEqual(FogTileKind.EdgeNorthWest, FogTileRules.KindAt(grid, cell), "level with it again, the dither comes back");
        }

        [TestCase(2, 2, true)]
        [TestCase(3, 2, true)]
        [TestCase(4, 2, false)]
        [TestCase(0, 2, false)]
        [TestCase(4, 4, false)]
        [TestCase(-1, 2, false)]
        public void ShowsWhatStandsOn_OnlyRevealedCells_EvenAtTheDitheredEdge(int x, int y, bool shown)
        {
            Assert.AreEqual(shown, FogTileRules.ShowsWhatStandsOn(grid, new Coord(x, y)));
        }

        [Test]
        public void ShowsWhatStandsAt_FollowsTheCellUnderTheSpriteWhileItSteps()
        {
            var revealed = IsoMath.GridToWorld(new Coord(3, 2));
            var hidden = IsoMath.GridToWorld(new Coord(4, 2));

            Assert.IsTrue(FogTileRules.ShowsWhatStandsAt(grid, revealed));
            Assert.IsTrue(FogTileRules.ShowsWhatStandsAt(grid, UnityEngine.Vector2.Lerp(revealed, hidden, 0.25f)), "an actor stays visible while it still stands on the revealed cell");
            Assert.IsFalse(FogTileRules.ShowsWhatStandsAt(grid, UnityEngine.Vector2.Lerp(revealed, hidden, 0.75f)), "an actor vanishes once it stands on the fogged cell");
            Assert.IsFalse(FogTileRules.ShowsWhatStandsAt(grid, hidden));
        }
    }
}
