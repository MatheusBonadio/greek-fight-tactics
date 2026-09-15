using Game.Battle;
using Game.Core;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Battle
{
    public sealed class TurnQueueTests
    {
        [Test]
        public void Queue_StartsWithTheHighestInitiative_AndAlternates()
        {
            var queue = new TurnQueue(new[] { Debater(1, 3), Debater(2, 9), Debater(3, 6) });

            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, queue.Preview(3));
            queue.Advance(1);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, queue.Preview(3));
        }

        [Test]
        public void TwoTurnAction_SkipsTheActorsNextTurn()
        {
            var queue = new TurnQueue(new[] { Debater(1, 10), Debater(2, 5) });

            queue.Advance(2);

            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, queue.Preview(3));
            queue.Advance(1);
            Assert.AreEqual(2, queue.Current);
            queue.Advance(1);
            Assert.AreEqual(1, queue.Current);
        }

        [Test]
        public void Preview_DoesNotChangeTheQueue()
        {
            var queue = new TurnQueue(new[] { Debater(1, 10), Debater(2, 5) });
            queue.Advance(3);

            queue.Preview(10);

            Assert.AreEqual(2, queue.Current);
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 1 }, queue.Preview(4));
        }

        [Test]
        public void EqualInitiative_BreaksTiesById()
        {
            var queue = new TurnQueue(new[] { Debater(7, 4), Debater(3, 4) });

            Assert.AreEqual(3, queue.Current);
        }

        private static Debater Debater(int id, int initiative) =>
            new(id, id == 1 ? Side.Player : Side.Opponent, Profile.Sophist, 10, initiative, new Coord(id, 0), Facing.SouthEast);
    }
}
