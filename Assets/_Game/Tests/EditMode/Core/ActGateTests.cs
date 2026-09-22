using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class ActGateTests
    {
        [Test]
        public void SecondActAreas_StayClosedWhileTheSaveIsInTheFirstAct()
        {
            var firstAct = new SaveData();
            var secondAct = new SaveData { ActFlag = Act.Two };

            Assert.AreEqual(Act.One, ActGate.ActOf(firstAct));
            Assert.AreEqual(Act.One, ActGate.ActOf(null), "no save is the start of the game");
            Assert.IsFalse(ActGate.IsReached(firstAct, Act.Two));
            Assert.IsFalse(ActGate.IsOpen(firstAct, Act.Two, null), "ActFlag = 1 keeps the second act locked");
            Assert.IsFalse(ActGate.IsOpen(null, Act.Two, null));
            Assert.IsTrue(ActGate.IsOpen(secondAct, Act.Two, null), "ActFlag = 2 opens it");
            Assert.IsTrue(ActGate.IsOpen(firstAct, Act.One, null), "the first act is always open");
            Assert.IsTrue(ActGate.IsOpen(secondAct, Act.One, null), "going back is always allowed");
        }

        [Test]
        public void Gate_AlsoWaitsForItsStoryConditions()
        {
            var searching = new[] { StoryCondition.Has("searching_menexeno") };
            var save = new SaveData();

            Assert.IsTrue(ActGate.IsGated(Act.One, searching));
            Assert.IsTrue(ActGate.IsGated(Act.Two, null));
            Assert.IsFalse(ActGate.IsGated(Act.One, null), "an exit with nothing to wait for is never closed");
            Assert.IsFalse(ActGate.IsOpen(save, Act.One, searching));

            save.Flags["searching_menexeno"] = StoryEffect.DefaultValue;
            Assert.IsTrue(ActGate.IsOpen(save, Act.One, searching));
            Assert.IsFalse(ActGate.IsOpen(save, Act.Two, searching), "the act and the story must both be there");
        }

        [Test]
        public void SettingTheActThroughAStoryEffect_OpensTheGate()
        {
            var save = new SaveData();
            Assert.IsTrue(new StoryEffect(SaveFlags.Act, "2").TryApply(save, out _));

            Assert.AreEqual(Act.Two, save.ActFlag);
            Assert.IsTrue(ActGate.IsReached(save, Act.Two));
            Assert.IsTrue(StoryCondition.Is(SaveFlags.Act, "2").IsMet(save));
        }

        [Test]
        public void NotCondition_HoldsUntilTheFlagTakesThatValue()
        {
            var undecided = StoryCondition.Not(SaveFlags.DamarisBranch, "undecided");
            var notWon = StoryCondition.Not("duel_thrasybios_street_trial", "won");
            var save = new SaveData();

            Assert.IsTrue(undecided.IsValid);
            Assert.IsTrue(notWon.IsValid);
            Assert.IsFalse(new StoryCondition("flag", "value", false, "other").IsValid, "equals and not cannot be mixed");
            Assert.IsFalse(new StoryCondition("flag", null, true, "value").IsValid, "absent and not cannot be mixed");
            Assert.AreEqual("damaris_branch!=undecided", undecided.ToString());

            Assert.IsFalse(undecided.IsMet(save), "a new game has not decided on Damaris");
            Assert.IsTrue(notWon.IsMet(save), "an absent flag is not the forbidden value");

            save.DamarisBranch = DamarisChoice.Refused;
            save.Flags["duel_thrasybios_street_trial"] = DuelStoryFlags.Lost;
            Assert.IsTrue(undecided.IsMet(save));
            Assert.IsTrue(notWon.IsMet(save), "losing keeps the trial waiting");

            save.DamarisBranch = DamarisChoice.Accepted;
            save.Flags["duel_thrasybios_street_trial"] = DuelStoryFlags.Won;
            Assert.IsTrue(undecided.IsMet(save));
            Assert.IsFalse(notWon.IsMet(save));
        }
    }
}
