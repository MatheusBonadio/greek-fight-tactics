using Game.Core;
using Game.Grid;
using Game.Tests.EditMode.Grid;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class ExplorationSaveMapperTests
    {
        private const string Location = "sandbox";

        [Test]
        public void ToSave_TakesPositionFogLocationAndPlaytimeAndKeepsProgress()
        {
            var fog = new byte[] { 3, 7 };
            var snapshot = new ExplorationSnapshot(new Coord(4, 9), Facing.NorthEast, fog);
            var previous = new SaveData { ActFlag = Act.Two, DamarisBranch = DamarisChoice.Refused, Reputation = 11, Position = new Coord(1, 1), Timestamp = 10 };
            previous.Inventory["heraclitus"] = 1;
            previous.Duels["batilo"] = new DuelRecord(true, DuelVictory.Refute);
            previous.Flags["met_menexeno"] = "yes";

            var save = ExplorationSaveMapper.ToSave(snapshot, previous, 99, Location, 125);
            fog[0] = 0;

            Assert.AreEqual(new Coord(4, 9), save.Position);
            CollectionAssert.AreEqual(new byte[] { 3, 7 }, save.FogBitmask);
            Assert.AreEqual(Location, save.Location);
            Assert.AreEqual(125, save.PlaytimeSeconds);
            Assert.AreEqual(99, save.Timestamp);
            Assert.AreEqual(Act.Two, save.ActFlag);
            Assert.AreEqual(DamarisChoice.Refused, save.DamarisBranch);
            Assert.AreEqual(11, save.Reputation);
            Assert.AreEqual(1, save.Inventory["heraclitus"]);
            Assert.IsTrue(save.Duels["batilo"].Won);
            Assert.AreEqual("yes", save.Flags["met_menexeno"]);
        }

        [Test]
        public void ToSave_StartsAFreshSaveWithoutPreviousProgress()
        {
            var save = ExplorationSaveMapper.ToSave(new ExplorationSnapshot(new Coord(2, 2), Facing.SouthEast, new byte[] { 1 }), null, 5, null, -3);

            Assert.AreEqual(Act.One, save.ActFlag);
            Assert.AreEqual(DamarisChoice.Undecided, save.DamarisBranch);
            Assert.IsEmpty(save.Duels);
            Assert.AreEqual(string.Empty, save.Location);
            Assert.AreEqual(0, save.PlaytimeSeconds);
        }

        [Test]
        public void Restore_MovesThePlayerAndLoadsTheFogOnlyForTheSameLocation()
        {
            var savedGrid = GridFactory.Open(8, 8);
            var savedFog = new FogOfWar(savedGrid);
            savedFog.Reveal(new Coord(6, 6), 1);
            var save = new SaveData { Position = new Coord(6, 6), FogBitmask = savedFog.ToBitmask(), Location = Location };

            var grid = GridFactory.Open(8, 8);
            var walker = new GridWalker(grid, 1, new Coord(1, 1));
            using var session = new ExplorationSession(walker, new FogOfWar(grid));

            Assert.IsFalse(ExplorationRestore.TryApply(session, new SaveData { Position = new Coord(6, 6), FogBitmask = save.FogBitmask, Location = "agora" }, Location, Facing.SouthEast));
            Assert.AreEqual(new Coord(1, 1), walker.Cell);

            Assert.IsTrue(ExplorationRestore.TryApply(session, save, Location, Facing.NorthWest));
            Assert.AreEqual(new Coord(6, 6), walker.Cell);
            Assert.IsTrue(grid[new Coord(6, 6)].Revealed);
            Assert.IsFalse(ExplorationRestore.TryApply(null, save, Location, Facing.NorthWest));
        }
    }
}
