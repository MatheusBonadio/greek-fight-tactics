using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class StoryPressureTests
    {
        private static readonly (string Opponent, string Result)[] ActOneDuels =
        {
            ("thrasybios", DuelStoryFlags.Won),
            ("oron", DuelStoryFlags.Lost),
            ("phaidra", DuelStoryFlags.Withdrew),
            ("kritolaos", DuelStoryFlags.Stalemate),
            ("simo", DuelStoryFlags.Won)
        };

        [Test]
        public void Pressure_GrowsWithEveryDuelConcludedWhateverItsResult()
        {
            var save = new SaveData();
            var levels = new[] { 0 }.ToList();
            foreach (var (opponent, result) in ActOneDuels)
            {
                save.Flags[DuelStoryFlags.FlagFor(opponent)] = result;
                levels.Add(StoryPressure.Level(save));
            }

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 2, 3, 3 }, levels);
            Assert.AreEqual(ActOneDuels.Length, StoryPressure.ConcludedDuels(save).Count);
        }

        [Test]
        public void Pressure_CountsARematchOnceAndReadsVictoriesSavedBeforeDuelFlags()
        {
            var save = new SaveData();
            save.Flags[DuelStoryFlags.FlagFor("oron")] = DuelStoryFlags.Withdrew;
            save.Flags[DuelStoryFlags.FlagFor("oron")] = DuelStoryFlags.Won;
            save.Duels["oron"] = new DuelRecord(true, DuelVictory.Refute);
            save.Duels["thrasybios"] = new DuelRecord(true, DuelVictory.Refute);
            save.Duels["simo"] = new DuelRecord(false, DuelVictory.Concede);
            save.Flags["duel_notes"] = "maybe";
            save.Flags[DuelStoryFlags.Prefix] = DuelStoryFlags.Won;

            CollectionAssert.AreEquivalent(new[] { "oron", "thrasybios" }, StoryPressure.ConcludedDuels(save));
            Assert.IsTrue(StoryCondition.Has(StoryPressure.RumorFlag).IsMet(save), "an old save that beat two opponents already hears the rumor");
        }

        [Test]
        public void Stages_AreReadLikeStoryFlagsWithoutBeingWrittenToTheSave()
        {
            var save = new SaveData();
            foreach (var (opponent, result) in ActOneDuels.Take(StoryPressure.DesertedDuels))
            {
                save.Flags[DuelStoryFlags.FlagFor(opponent)] = result;
            }

            Assert.IsTrue(StoryCondition.Has(StoryPressure.RumorFlag).IsMet(save));
            Assert.IsTrue(StoryCondition.Has(StoryPressure.DesertedFlag).IsMet(save));
            Assert.IsTrue(StoryCondition.Lacks(StoryPressure.ToolsFlag).IsMet(save));
            Assert.IsTrue(SaveFlags.TryGet(save, StoryPressure.DesertedFlag, out var value));
            Assert.AreEqual(StoryPressure.ReachedValue, value);
            Assert.IsFalse(SaveFlags.TryGet(save, StoryPressure.ToolsFlag, out _));
            CollectionAssert.IsEmpty(save.Flags.Keys.Where(StoryPressure.IsStageFlag), "the stages come from the duels, not from stored flags");
        }

        [Test]
        public void Stages_AreCustomFlagsInIncreasingOrder()
        {
            var thresholds = StoryPressure.Stages.Select(stage => stage.ConcludedDuels).ToList();

            CollectionAssert.IsOrdered(thresholds);
            Assert.AreEqual(thresholds.Count, thresholds.Distinct().Count(), "each duel past the first raises the pressure one stage");
            foreach (var stage in StoryPressure.Stages)
            {
                Assert.IsTrue(SaveFlags.IsCustomName(stage.Flag), stage.Flag);
                Assert.IsTrue(StoryCondition.Lacks(stage.Flag).IsValid, stage.Flag);
            }

            Assert.Greater(StoryPressure.RumorDuels, 1, "the tutorial duel alone does not start the rumor");
        }

        [Test]
        public void DamarisBranchEffect_GoesThroughTheOfferOnlyOnce()
        {
            var save = new SaveData();
            var accept = new StoryEffect(SaveFlags.DamarisBranch, "accepted");

            Assert.IsTrue(accept.TryApply(save, out var error), error);
            Assert.IsTrue(accept.TryApply(save, out error), "replaying the effect onto a later save is harmless");

            Assert.AreEqual(DamarisChoice.Accepted, save.DamarisBranch);
            Assert.AreEqual(DamarisOffer.AcceptedDrachmas, save.Inventory[DamarisOffer.DrachmasItem], "the drachmas are paid once");
            Assert.IsTrue(AbilityUnlocks.IsOwned(AbilityId.Inversion, save));
            Assert.AreEqual(DamarisOffer.MenexenoAbsentDays, StoryDays.AbsenceLeft(save, DamarisOffer.MenexenoAbsentDaysFlag));

            var refusal = new SaveData();
            Assert.IsTrue(new StoryEffect(SaveFlags.DamarisBranch, "refused").TryApply(refusal, out error), error);
            CollectionAssert.AreEqual(new[] { AbilityId.Listening }, AbilityUnlocks.OwnedIn(refusal));
            Assert.IsTrue(StoryCondition.Has(DamarisOffer.MenexenoRespectFlag).IsMet(refusal));
            Assert.IsFalse(refusal.Inventory.ContainsKey(DamarisOffer.DrachmasItem));
        }

        [Test]
        public void Abilities_AreOwnedOnlyThroughTheirStoryFlag()
        {
            CollectionAssert.IsEmpty(AbilityUnlocks.OwnedIn(new SaveData()));
            CollectionAssert.IsEmpty(AbilityUnlocks.OwnedIn(null));

            var save = new SaveData();
            save.Flags[DamarisOffer.AbilityFlag(AbilityId.Listening)] = DamarisOffer.EnabledValue;
            CollectionAssert.AreEqual(new[] { AbilityId.Listening }, AbilityUnlocks.OwnedIn(save));
            Assert.IsFalse(AbilityUnlocks.IsOwned(AbilityId.Inversion, save));
        }
    }
}
