using Game.Core;
using Game.Net;
using NUnit.Framework;

namespace Game.Tests.EditMode.Net
{
    public sealed class StoryFlagSaveTests
    {
        private const string MetFlag = "met_menexeno";
        private const string SaveWithoutFlags = "{\"actFlag\": 1, \"reputation\": 4, \"position\": {\"x\": 2, \"y\": 3}, \"timestamp\": 10}";

        [Test]
        public void SaveWrittenBeforeStoryFlags_StillLoadsAndReadsEveryFlagAsUnset()
        {
            Assert.IsTrue(SaveJson.TryDeserialize(SaveWithoutFlags, out var save, out var error), error);

            CollectionAssert.IsEmpty(save.Flags);
            Assert.IsTrue(StoryCondition.Lacks(MetFlag).IsMet(save));
            Assert.IsTrue(StoryCondition.Is(SaveFlags.Reputation, "4").IsMet(save));
            Assert.IsTrue(StoryCondition.Is(SaveFlags.Position, "2,3").IsMet(save));
        }

        [Test]
        public void FlagsWrittenByADialogue_SurviveTheSaveFile()
        {
            Assert.IsTrue(SaveJson.TryDeserialize(SaveWithoutFlags, out var save, out _));
            var script = new DialogueScript("menexeno", new[]
            {
                new ScriptLine("menexeno", "first", new[] { StoryCondition.Lacks(MetFlag) }, new[] { new StoryEffect(MetFlag), new StoryEffect(SaveFlags.Reputation, "5") }),
                new ScriptLine("menexeno", "again", new[] { StoryCondition.Has(MetFlag) })
            });

            var run = script.Begin(save);
            while (run.TryAdvance(out _))
            {
            }

            Assert.IsTrue(SaveJson.TryDeserialize(SaveJson.Serialize(save), out var reloaded, out var error), error);
            Assert.AreEqual(StoryEffect.DefaultValue, reloaded.Flags[MetFlag]);
            Assert.AreEqual(5, reloaded.Reputation);
            Assert.IsTrue(script.Begin(reloaded).TryAdvance(out var line));
            Assert.AreEqual("again", line.Text);
        }
    }
}
