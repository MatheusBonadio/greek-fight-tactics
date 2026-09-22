using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class StoryCheckpointTests
    {
        [Test]
        public void ANewGame_HasReachedNothingAndTheFirstCheckpointComesNext()
        {
            var save = new SaveData();

            Assert.AreEqual(StoryCheckpoints.NotReached, StoryCheckpoints.IndexOf(save));
            Assert.IsNull(StoryCheckpoints.Reached(save));
            Assert.AreSame(StoryCheckpoints.All[0], StoryCheckpoints.Next(save));
        }

        [Test]
        public void EveryCheckpoint_WritesFlagsTheSaveAccepts()
        {
            var save = new SaveData();

            foreach (var checkpoint in StoryCheckpoints.All)
            {
                CollectionAssert.IsNotEmpty(checkpoint.Effects.ToList(), checkpoint.Id);
                foreach (var effect in checkpoint.Effects)
                {
                    Assert.IsTrue(effect.TryApply(save, out var error), $"{checkpoint.Id}: {effect} - {error}");
                }

                Assert.IsTrue(checkpoint.IsReached(save), $"{checkpoint.Id} is not reached by its own effects");
            }
        }

        [Test]
        public void SkippingOneAtATime_WalksTheStoryForwardWithoutSkippingAStep()
        {
            var save = new SaveData();

            for (var step = 0; step < StoryCheckpoints.All.Count; step++)
            {
                var next = StoryCheckpoints.Next(save);
                Assert.AreSame(StoryCheckpoints.All[step], next, $"step {step}");
                Assert.IsTrue(next.Apply(save));
                Assert.AreEqual(step, StoryCheckpoints.IndexOf(save));
                Assert.AreSame(next, StoryCheckpoints.Reached(save));
            }

            Assert.IsNull(StoryCheckpoints.Next(save), "the last checkpoint ends the shortcut");
        }

        [Test]
        public void TheCheckpoints_CarryTheStoryFromTheWorkshopToTheSecondAct()
        {
            var save = new SaveData();
            foreach (var checkpoint in StoryCheckpoints.All)
            {
                checkpoint.Apply(save);
                if (checkpoint.Id == "pressure")
                {
                    Assert.AreEqual(StoryPressure.ToolsDuels, StoryPressure.ConcludedDuels(save).Count, "four duels put the workshop under pressure");
                    Assert.IsTrue(StoryCondition.Has(StoryPressure.ToolsFlag).IsMet(save), "the pressure reaches the last stage, where Damaris waits");
                }
            }

            Assert.AreEqual(Act.Two, save.ActFlag, "the last checkpoint opens the second act");
            Assert.AreEqual(DamarisChoice.Refused, save.DamarisBranch);
            Assert.IsTrue(ActionUnlocks.IsUnlocked(ActionId.Maieutics, save));
            Assert.IsTrue(StoryCondition.Has("searching_menexeno").IsMet(save));
        }

        [Test]
        public void EachCheckpoint_NamesTheMapItBelongsTo()
        {
            var maps = new[] { StoryCheckpoints.Workshop, StoryCheckpoints.Agora, StoryCheckpoints.Archive };

            foreach (var checkpoint in StoryCheckpoints.All)
            {
                CollectionAssert.Contains(maps, checkpoint.Location, checkpoint.Id);
                Assert.IsNotEmpty(checkpoint.Label, checkpoint.Id);
            }

            CollectionAssert.AllItemsAreUnique(StoryCheckpoints.All.Select(checkpoint => checkpoint.Id).ToList());
        }
    }
}
