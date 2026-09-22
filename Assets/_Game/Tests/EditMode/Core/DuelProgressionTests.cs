using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class DuelProgressionTests
    {
        private static readonly Coord Market = new(7, 1);
        private static readonly Coord Stoa = new(11, 6);

        [Test]
        public void Challenge_PicksOnlyTheChallengerOnTheConfirmedCell()
        {
            var challengers = new[] { new Challenger(Market, true, false), new Challenger(Stoa, true, false) };

            Assert.AreEqual(0, ChallengeRule.ChallengerAt(Market, GameMode.Exploration, challengers));
            Assert.AreEqual(1, ChallengeRule.ChallengerAt(Stoa, GameMode.Exploration, challengers));
            Assert.AreEqual(ChallengeRule.NoChallenger, ChallengeRule.ChallengerAt(new Coord(10, 6), GameMode.Exploration, challengers), "standing beside a challenger challenges nobody");
            Assert.AreEqual(ChallengeRule.NoChallenger, ChallengeRule.ChallengerAt(new Coord(0, 1), GameMode.Exploration, challengers));
        }

        [Test]
        public void Challenge_IgnoresDefeatedAbsentAndHiddenChallengers()
        {
            var challengers = new[] { new Challenger(Market, true, true), Challenger.Absent, new Challenger(Stoa, false, false) };

            Assert.AreEqual(ChallengeRule.NoChallenger, ChallengeRule.ChallengerAt(Market, GameMode.Exploration, challengers), "a defeated opponent never takes up a challenge again");
            Assert.AreEqual(ChallengeRule.NoChallenger, ChallengeRule.ChallengerAt(default, GameMode.Exploration, challengers), "an actor that is not on the map is nobody");
            Assert.AreEqual(ChallengeRule.NoChallenger, ChallengeRule.ChallengerAt(Stoa, GameMode.Exploration, challengers), "a challenger under the fog cannot be seen, so he cannot be challenged");
            Assert.IsFalse(Challenger.Absent.StandsAt(default));
        }

        [TestCase(GameMode.Dialogue)]
        [TestCase(GameMode.Cutscene)]
        [TestCase(GameMode.Paused)]
        [TestCase(GameMode.BattleTurn)]
        public void Challenge_OnlyHappensWhileExploring(GameMode mode)
        {
            var challengers = new[] { new Challenger(Market, true, false) };

            Assert.AreEqual(ChallengeRule.NoChallenger, ChallengeRule.ChallengerAt(Market, mode, challengers));
            Assert.AreEqual(ChallengeRule.NoChallenger, ChallengeRule.ChallengerAt(Market, GameMode.Exploration, null));
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
