using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class DuelProgressionTests
    {
        private const int WideRadius = 3;
        private const int NarrowRadius = 1;

        private static readonly Coord Market = new(7, 1);
        private static readonly Coord Stoa = new(11, 6);

        [Test]
        public void Watch_TriggersTheOpponentWhoseRadiusHoldsThePlayer()
        {
            var watch = new AggroWatch();
            var candidates = new[] { new AggroCandidate(Market, WideRadius), new AggroCandidate(Stoa, NarrowRadius) };

            Assert.AreEqual(AggroWatch.NoTrigger, watch.Update(new Coord(0, 1), candidates));
            Assert.AreEqual(0, watch.Update(new Coord(5, 2), candidates));
            Assert.AreEqual(1, watch.Update(new Coord(10, 6), candidates));
            Assert.AreEqual(AggroWatch.NoTrigger, watch.Update(new Coord(9, 6), candidates), "a narrow radius only reaches the neighbouring cells");
        }

        [Test]
        public void Watch_PrefersTheNearestOpponentWhenRadiiOverlap()
        {
            var watch = new AggroWatch();
            var candidates = new[] { new AggroCandidate(new Coord(0, 0), WideRadius), new AggroCandidate(new Coord(3, 0), WideRadius) };

            Assert.AreEqual(1, watch.Update(new Coord(2, 0), candidates));
            Assert.AreEqual(0, watch.Update(new Coord(1, 0), candidates));
        }

        [Test]
        public void Watch_NeverTriggersADormantOpponent()
        {
            var watch = new AggroWatch();
            var candidates = new[] { new AggroCandidate(Stoa, NarrowRadius, dormant: true), AggroCandidate.Asleep };

            Assert.AreEqual(AggroWatch.NoTrigger, watch.Update(new Coord(10, 6), candidates));
            Assert.AreEqual(AggroWatch.NoTrigger, watch.Update(default, candidates));
        }

        [Test]
        public void Watch_WaitsForThePlayerToLeaveAfterADisengagedBattle()
        {
            var watch = new AggroWatch();
            var candidates = new[] { new AggroCandidate(Stoa, NarrowRadius) };
            var beside = new Coord(10, 6);

            watch.Disengage(0);
            Assert.AreEqual(AggroWatch.NoTrigger, watch.Update(beside, candidates));
            Assert.IsTrue(watch.IsWaitingForExit(0));

            Assert.AreEqual(AggroWatch.NoTrigger, watch.Update(new Coord(9, 6), candidates));
            Assert.IsFalse(watch.IsWaitingForExit(0));
            Assert.AreEqual(0, watch.Update(beside, candidates));
        }

        [Test]
        public void Progress_KeepsTheFirstVictoryAndLoadsSavedDuels()
        {
            var progress = new DuelProgress();
            var saved = new SaveData();
            saved.Duels["oron"] = new DuelRecord(true, DuelVictory.Refute);
            saved.Duels["simo"] = new DuelRecord(false, DuelVictory.Concede);

            progress.Load(saved.Duels);
            progress.RecordVictory("oron", DuelVictory.Maieutics);
            progress.RecordVictory("batilo", DuelVictory.Maieutics);

            Assert.IsTrue(progress.HasWon("oron"));
            Assert.AreEqual(DuelVictory.Refute, progress.Records["oron"].Victory);
            Assert.IsFalse(progress.HasWon("simo"));
            Assert.IsTrue(progress.HasWon("batilo"));
            Assert.IsFalse(progress.HasWon("phaidra"));
            Assert.IsFalse(progress.HasWon(null));
            Assert.Throws<System.ArgumentException>(() => progress.RecordVictory(" ", DuelVictory.Refute));
        }

        [Test]
        public void Progress_WritesVictoriesOverUnwonRecordsWithoutLosingOthers()
        {
            var progress = new DuelProgress();
            progress.RecordVictory("simo", DuelVictory.Refute);
            var save = new SaveData();
            save.Duels["simo"] = new DuelRecord(false, DuelVictory.Concede);
            save.Duels["kritolaos"] = new DuelRecord(true, DuelVictory.Concede);

            progress.WriteTo(save);

            Assert.IsTrue(save.Duels["simo"].Won);
            Assert.AreEqual(DuelVictory.Refute, save.Duels["simo"].Victory);
            Assert.IsTrue(save.Duels["kritolaos"].Won);
            Assert.AreEqual(2, save.Duels.Count);
        }
    }
}
