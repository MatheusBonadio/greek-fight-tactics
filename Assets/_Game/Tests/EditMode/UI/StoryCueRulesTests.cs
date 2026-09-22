using Game.Core;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class StoryCueRulesTests
    {
        private const string Once = "saw_hint";
        private const string Needed = "met_father";

        [Test]
        public void Cue_IsDueOnItsOwnEventUntilItsOnceFlagIsSet()
        {
            var cue = new StoryCueLink(ExplorationCue.FogRevealed, "hint", Once, new[] { StoryCondition.Has(Needed) });
            var story = new SaveData();

            Assert.IsFalse(cue.IsDue(ExplorationCue.FogRevealed, story), "waits for its condition");
            story.Flags[Needed] = StoryEffect.DefaultValue;
            Assert.IsTrue(cue.IsDue(ExplorationCue.FogRevealed, story));
            Assert.IsFalse(cue.IsDue(ExplorationCue.WalkFinished, story), "listens to one event only");
            Assert.IsFalse(cue.IsDue(ExplorationCue.FogRevealed, null));

            story.Flags[Once] = StoryEffect.DefaultValue;
            Assert.IsFalse(cue.IsDue(ExplorationCue.FogRevealed, story), "plays once");
        }

        [Test]
        public void Trigger_FiresOnlyWhileItsConditionsHoldAndItsFlagIsAbsent()
        {
            var trigger = new DialogueTriggerLink(new Coord(4, 6), "door", Once, new[] { new StoryCondition("duel_thrasybios", "won"), StoryCondition.Lacks("maieutics_unlocked") });
            var story = new SaveData();

            Assert.IsFalse(trigger.IsDue(story));
            story.Flags["duel_thrasybios"] = "lost";
            Assert.IsFalse(trigger.IsDue(story), "a lost duel does not bring the scene");
            story.Flags["duel_thrasybios"] = "won";
            Assert.IsTrue(trigger.IsDue(story));
            story.Flags["maieutics_unlocked"] = StoryEffect.DefaultValue;
            Assert.IsFalse(trigger.IsDue(story), "talking to him first also closes the scene");
        }

        [Test]
        public void CutsceneTrigger_KeepsItsCellFlagAndConditions()
        {
            var trigger = DialogueTriggerLink.ForCutscene(new Coord(3, 5), "door_scene", Once, new[] { StoryCondition.Has(Needed) });
            var story = new SaveData();

            Assert.IsTrue(trigger.IsCutscene);
            Assert.AreEqual("door_scene", trigger.Cutscene);
            Assert.IsTrue(string.IsNullOrEmpty(trigger.Dialogue));
            Assert.AreEqual(new Coord(3, 5), trigger.Cell);
            Assert.IsFalse(trigger.IsDue(story));
            story.Flags[Needed] = StoryEffect.DefaultValue;
            Assert.IsTrue(trigger.IsDue(story));
            story.Flags[Once] = StoryEffect.DefaultValue;
            Assert.IsFalse(trigger.IsDue(story));
            Assert.IsFalse(new DialogueTriggerLink(new Coord(1, 1), "door", Once).IsCutscene);
        }

        [Test]
        public void IdleCue_WaitsForItsConditionsLikeEveryOtherCue()
        {
            var cue = new StoryCueLink(ExplorationCue.Idle, "go_home", Once, new[] { new StoryCondition("duel_thrasybios", "won") });
            var story = new SaveData();

            Assert.IsFalse(cue.IsDue(ExplorationCue.Idle, story));
            story.Duels["thrasybios"] = new DuelRecord(true, DuelVictory.Refute);
            Assert.IsTrue(cue.IsDue(ExplorationCue.Idle, story), "a duel won before the flag existed also points Kleia home");
            Assert.IsFalse(cue.IsDue(ExplorationCue.MapStart, story));
            story.Flags[Once] = StoryEffect.DefaultValue;
            Assert.IsFalse(cue.IsDue(ExplorationCue.Idle, story));
        }

        [Test]
        public void TriggerWithoutConditions_BehavesAsBefore()
        {
            var trigger = new DialogueTriggerLink(new Coord(1, 1), "door", Once);
            var story = new SaveData();

            Assert.IsTrue(trigger.IsDue(story));
            CollectionAssert.IsEmpty(trigger.Conditions);
            story.Flags[Once] = StoryEffect.DefaultValue;
            Assert.IsFalse(trigger.IsDue(story));
        }

        [Test]
        public void Presence_NeedsEveryCondition()
        {
            var presence = new ActorPresenceLink(null, new[] { StoryCondition.Has(Needed), StoryCondition.Lacks(Once) });
            var story = new SaveData();

            Assert.IsFalse(presence.IsMet(story));
            Assert.IsFalse(presence.IsMet(null));
            story.Flags[Needed] = StoryEffect.DefaultValue;
            Assert.IsTrue(presence.IsMet(story));
            story.Flags[Once] = StoryEffect.DefaultValue;
            Assert.IsFalse(presence.IsMet(story));
            Assert.IsTrue(StoryCondition.AllMet(null, story), "no conditions always hold");
        }
    }
}
