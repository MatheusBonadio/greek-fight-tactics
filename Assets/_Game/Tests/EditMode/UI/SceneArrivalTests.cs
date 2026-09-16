using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class SceneArrivalTests
    {
        [SetUp]
        [TearDown]
        public void ClearPendingArrival()
        {
            SceneArrival.Clear();
        }

        [Test]
        public void TryConsume_OnlyHandsTheArrivalToTheMapItWasMeantFor()
        {
            var arrival = new PendingArrival(LocationScenes.Kerameikos, new Coord(10, 1), Facing.SouthWest);
            SceneArrival.Set(arrival);

            Assert.IsFalse(SceneArrival.TryConsume(LocationScenes.Agora, out _));
            Assert.IsTrue(SceneArrival.HasPending, "a map that is not the destination must leave the arrival alone");

            Assert.IsTrue(SceneArrival.TryConsume(LocationScenes.Kerameikos, out var consumed));
            Assert.AreEqual(arrival.Cell, consumed.Cell);
            Assert.AreEqual(arrival.Facing, consumed.Facing);
            Assert.IsFalse(SceneArrival.HasPending);
            Assert.IsFalse(SceneArrival.TryConsume(LocationScenes.Kerameikos, out _), "an arrival is used once");
        }
    }
}
