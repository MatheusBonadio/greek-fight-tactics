using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class DuelTollTests
    {
        private const int Full = 60;

        [Test]
        public void ARestedKleia_WalksIntoTheDuelWhole()
        {
            var save = new SaveData();

            Assert.IsTrue(StoryStamina.IsRested(save));
            Assert.AreEqual(Full, StoryStamina.Carried(save, Full));
            Assert.AreEqual(Full, StoryStamina.Carried(null, Full));
        }

        [Test]
        public void WhatIsLeftOfADuel_IsWhatSheCarriesIntoTheNextOne()
        {
            var save = new SaveData();

            StoryStamina.Toll(DuelStoryFlags.Won, 45, Full).TryApply(save, out _);

            Assert.AreEqual(15, StoryStamina.LostOf(save));
            Assert.AreEqual(45, StoryStamina.Carried(save, Full));
            Assert.IsFalse(StoryStamina.IsRested(save));
        }

        [Test]
        public void Fleeing_CostsMoreThanTheDuelTookFromHer()
        {
            var save = new SaveData();

            StoryStamina.Toll(DuelStoryFlags.Withdrew, 45, Full).TryApply(save, out _);

            Assert.AreEqual(45 - StoryStamina.WithdrawToll, StoryStamina.Carried(save, Full));
        }

        [Test]
        public void Losing_LeavesHerBruisedButNeverBeaten()
        {
            var save = new SaveData();

            StoryStamina.Toll(DuelStoryFlags.Lost, 0, Full).TryApply(save, out _);

            Assert.AreEqual(StoryStamina.Bruised, StoryStamina.Carried(save, Full));
            Assert.AreEqual(StoryStamina.Bruised, StoryStamina.Left(DuelStoryFlags.Withdrew, StoryStamina.Bruised, Full), "fleeing on her last breath cannot take her below it");
        }

        [Test]
        public void SleepingAtHome_MakesHerWholeAgain()
        {
            var save = new SaveData();
            StoryStamina.Toll(DuelStoryFlags.Lost, 0, Full).TryApply(save, out _);

            StoryStamina.Rest(save);

            Assert.IsTrue(StoryStamina.IsRested(save));
            Assert.AreEqual(Full, StoryStamina.Carried(save, Full));
            Assert.IsFalse(save.Flags.ContainsKey(StoryStamina.LostFlag));
        }

        [Test]
        public void TheRestedEffect_UndoesAnEarlierTollOfTheSameSession()
        {
            var save = new SaveData();
            StoryStamina.Toll(DuelStoryFlags.Lost, 0, Full).TryApply(save, out _);

            StoryStamina.Rested().TryApply(save, out _);

            Assert.IsTrue(StoryStamina.IsRested(save));
        }

        [Test]
        public void Reputation_GrowsWithVictoriesAndFallsWithDefeatsAndFlights()
        {
            var save = new SaveData { Reputation = 20 };

            Assert.AreEqual(25, ReputationRules.After(save, DuelStoryFlags.Won));
            Assert.AreEqual(10, ReputationRules.After(save, DuelStoryFlags.Lost));
            Assert.AreEqual(10, ReputationRules.After(save, DuelStoryFlags.Withdrew));
            Assert.AreEqual(20, ReputationRules.After(save, DuelStoryFlags.Stalemate), "a debate nobody finished changes nothing");
        }

        [Test]
        public void Reputation_StaysBetweenNobodyAndEverybody()
        {
            var unknown = new SaveData();
            var famous = new SaveData { Reputation = ReputationRules.Highest };

            Assert.AreEqual(ReputationRules.Lowest, ReputationRules.After(unknown, DuelStoryFlags.Lost));
            Assert.AreEqual(ReputationRules.Highest, ReputationRules.After(famous, DuelStoryFlags.Won));
        }

        [Test]
        public void TheReputationEffect_WritesThroughTheSameGateAsTheStory()
        {
            var save = new SaveData { Reputation = 20 };

            Assert.IsTrue(ReputationRules.Toll(save, DuelStoryFlags.Lost).TryApply(save, out var error), error);

            Assert.AreEqual(10, save.Reputation);
            Assert.IsTrue(SaveFlags.TryGet(save, SaveFlags.Reputation, out var written));
            Assert.AreEqual("10", written);
        }
    }
}
