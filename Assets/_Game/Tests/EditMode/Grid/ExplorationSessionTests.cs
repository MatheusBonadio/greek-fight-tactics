using System.Linq;
using Game.Core;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Grid
{
    public sealed class ExplorationSessionTests
    {
        private const int ActorId = 5;

        private GridModel grid;
        private FogOfWar fog;

        [SetUp]
        public void SetUp()
        {
            grid = GridFactory.Open(12, 12);
            fog = new FogOfWar(grid);
        }

        [Test]
        public void Starting_RevealsAroundTheActor()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(6, 6));

            using var session = new ExplorationSession(walker, fog);

            Assert.AreEqual(25, RevealedCount());
            Assert.IsTrue(grid[new Coord(6, 9)].Revealed);
            Assert.IsFalse(grid[new Coord(6, 10)].Revealed);
        }

        [Test]
        public void EnteringACell_RevealsItsRadius()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(3, 6));
            using var session = new ExplorationSession(walker, fog);

            walker.Follow(new[] { new Coord(4, 6) });

            Assert.AreEqual(25 + 7, RevealedCount());
            Assert.IsTrue(grid[new Coord(7, 6)].Revealed);
        }

        [Test]
        public void Dispose_StopsRevealing()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(3, 6));
            var session = new ExplorationSession(walker, fog);
            session.Dispose();

            walker.Follow(new[] { new Coord(4, 6) });

            Assert.AreEqual(25, RevealedCount());
        }

        [Test]
        public void Restore_ReturnsPositionFacingAndFogWithoutReloading()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(2, 2), Facing.SouthEast);
            using var session = new ExplorationSession(walker, fog);
            walker.Follow(new[] { new Coord(3, 2), new Coord(4, 2) });
            walker.Advance(GridWalker.SecondsPerCell * 2f);

            var snapshot = session.Capture();
            var revealedBefore = RevealedCells();

            walker.Follow(new[] { new Coord(4, 3), new Coord(4, 4), new Coord(4, 5) });
            walker.Advance(GridWalker.SecondsPerCell * 3f);
            Assert.AreNotEqual(revealedBefore.Length, RevealedCells().Length);

            Assert.IsTrue(session.TryRestore(snapshot));

            Assert.AreEqual(new Coord(4, 2), walker.Cell);
            Assert.AreEqual(Facing.NorthEast, walker.Facing);
            Assert.IsFalse(walker.IsMoving);
            Assert.AreEqual(ActorId, grid[new Coord(4, 2)].OccupantId);
            Assert.AreEqual(TileData.NoOccupant, grid[new Coord(4, 5)].OccupantId);
            CollectionAssert.AreEqual(revealedBefore, RevealedCells());
        }

        [Test]
        public void Snapshot_IsIsolatedFromLaterChanges()
        {
            var walker = new GridWalker(grid, ActorId, new Coord(6, 6));
            using var session = new ExplorationSession(walker, fog);
            var snapshot = session.Capture();

            var copy = snapshot.CopyFogBitmask();
            copy[0] = 0xFF;

            CollectionAssert.AreNotEqual(copy, snapshot.CopyFogBitmask());
        }

        private int RevealedCount() => RevealedCells().Length;

        private Coord[] RevealedCells() =>
            Enumerable.Range(0, grid.Height)
                .SelectMany(y => Enumerable.Range(0, grid.Width).Select(x => new Coord(x, y)))
                .Where(cell => grid[cell].Revealed)
                .ToArray();
    }
}
