using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class ExplorationSaveMapperTests
    {
        [Test]
        public void ToSave_TakesPositionAndFogFromExplorationAndKeepsProgress()
        {
            var fog = new byte[] { 3, 7 };
            var snapshot = new ExplorationSnapshot(new Coord(4, 9), Facing.NorthEast, fog);
            var previous = new SaveData { ActFlag = Act.Two, DamarisBranch = DamarisChoice.Refused, Reputation = 11, Position = new Coord(1, 1), Timestamp = 10 };
            previous.Inventory["heraclitus"] = 1;
            previous.Duels["batilo"] = new DuelRecord(true, DuelVictory.Refute);
            previous.Flags["met_menexeno"] = "yes";

            var save = ExplorationSaveMapper.ToSave(snapshot, previous, 99);
            fog[0] = 0;

            Assert.AreEqual(new Coord(4, 9), save.Position);
            CollectionAssert.AreEqual(new byte[] { 3, 7 }, save.FogBitmask);
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
            var save = ExplorationSaveMapper.ToSave(new ExplorationSnapshot(new Coord(2, 2), Facing.SouthEast, new byte[] { 1 }), null, 5);

            Assert.AreEqual(Act.One, save.ActFlag);
            Assert.AreEqual(DamarisChoice.Undecided, save.DamarisBranch);
            Assert.IsEmpty(save.Duels);
            Assert.AreEqual(new Coord(2, 2), save.Position);
        }
    }
}
