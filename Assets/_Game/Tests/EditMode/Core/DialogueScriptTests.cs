using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class DialogueScriptTests
    {
        private const string MetFlag = "met_menexeno";
        private const string EudoroFlag = "met_eudoro";

        [Test]
        public void Conditions_ReadCustomFlagsAndTheKnownSaveFields()
        {
            var save = new SaveData { ActFlag = Act.Two, DamarisBranch = DamarisChoice.Refused };
            save.Flags[MetFlag] = "true";

            Assert.IsTrue(StoryCondition.Has(MetFlag).IsMet(save));
            Assert.IsFalse(StoryCondition.Lacks(MetFlag).IsMet(save));
            Assert.IsTrue(StoryCondition.Lacks(EudoroFlag).IsMet(save));
            Assert.IsFalse(StoryCondition.Has(EudoroFlag).IsMet(save));
            Assert.IsTrue(StoryCondition.Is(MetFlag, "true").IsMet(save));
            Assert.IsFalse(StoryCondition.Is(MetFlag, "false").IsMet(save));
            Assert.IsTrue(StoryCondition.Is(SaveFlags.Act, "2").IsMet(save));
            Assert.IsTrue(StoryCondition.Is(SaveFlags.DamarisBranch, "refused").IsMet(save));
            Assert.IsFalse(StoryCondition.Is(SaveFlags.DamarisBranch, "accepted").IsMet(save));
        }

        [Test]
        public void Conditions_TreatAnEmptyFlagAsAbsent()
        {
            var save = new SaveData();
            save.Flags[MetFlag] = string.Empty;

            Assert.IsFalse(StoryCondition.Has(MetFlag).IsMet(save));
            Assert.IsTrue(StoryCondition.Lacks(MetFlag).IsMet(save));
        }

        [TestCase("met_menexeno", null, false, true)]
        [TestCase("met_menexeno", "yes", false, true)]
        [TestCase("met_menexeno", null, true, true)]
        [TestCase("met_menexeno", "yes", true, false)]
        [TestCase("act", "2", false, true)]
        [TestCase("act", null, true, false)]
        [TestCase("Met Menexeno", null, false, false)]
        [TestCase(null, null, false, false)]
        public void Conditions_AreValidOnlyForSaveFlagNamesAndConsistentChecks(string flag, string value, bool absent, bool valid)
        {
            Assert.AreEqual(valid, new StoryCondition(flag, value, absent).IsValid);
        }

        [Test]
        public void Effects_WriteThroughTheSaveFlagValidation()
        {
            var save = new SaveData();

            Assert.IsTrue(new StoryEffect(MetFlag).TryApply(save, out _));
            Assert.AreEqual(StoryEffect.DefaultValue, save.Flags[MetFlag]);
            Assert.IsTrue(new StoryEffect(SaveFlags.Reputation, "3").TryApply(save, out _));
            Assert.AreEqual(3, save.Reputation);
            Assert.IsTrue(new StoryEffect(SaveFlags.Act, "2").TryApply(save, out _));
            Assert.AreEqual(Act.Two, save.ActFlag);

            Assert.IsFalse(new StoryEffect("Bad Flag").TryApply(save, out var badName));
            Assert.IsNotEmpty(badName);
            Assert.IsFalse(new StoryEffect(SaveFlags.Act, "7").TryApply(save, out var badAct));
            Assert.IsNotEmpty(badAct);
            Assert.AreEqual(Act.Two, save.ActFlag);
        }

        [Test]
        public void FirstConversation_ShowsTheGreetingAndRecordsTheMeeting()
        {
            var save = new SaveData();
            var run = Menexeno().Begin(save);

            CollectionAssert.AreEqual(new[] { "greeting", "question", "answer" }, Texts(run));
            Assert.AreEqual(StoryEffect.DefaultValue, save.Flags[MetFlag]);
            CollectionAssert.AreEqual(new[] { MetFlag }, run.AppliedEffects.Select(effect => effect.Flag));
        }

        [Test]
        public void LaterConversations_SkipTheGreetingAndBranchOnOtherFlags()
        {
            var save = new SaveData();
            Texts(Menexeno().Begin(save));

            CollectionAssert.AreEqual(new[] { "again" }, Texts(Menexeno().Begin(save)));

            save.Flags[EudoroFlag] = "true";
            CollectionAssert.AreEqual(new[] { "again", "about eudoro" }, Texts(Menexeno().Begin(save)));
        }

        [Test]
        public void Conditions_AreJudgedWhenTheConversationStartsAndEffectsLandAsEachLineShows()
        {
            var save = new SaveData();
            var run = Menexeno().Begin(save);

            Assert.AreEqual(3, run.Lines.Count, "the repeat lines stay hidden even after the greeting sets the flag");
            Assert.IsTrue(run.TryAdvance(out _));
            Assert.IsTrue(run.TryAdvance(out _));
            Assert.IsFalse(save.Flags.ContainsKey(MetFlag), "the flag is written by the line that carries it, not before");
            Assert.IsTrue(run.TryAdvance(out var answer));
            Assert.AreEqual("answer", answer.Text);
            Assert.AreSame(answer, run.Current);
            Assert.IsTrue(save.Flags.ContainsKey(MetFlag));
            Assert.IsFalse(run.TryAdvance(out _));
            Assert.IsTrue(run.IsFinished);
            Assert.IsNull(run.Current);
        }

        [Test]
        public void Conversation_WithNoAvailableLine_IsEmptyAndFinished()
        {
            var script = new DialogueScript("gate", new[] { new ScriptLine("guard", "only in act two", new[] { StoryCondition.Is(SaveFlags.Act, "2") }) });
            var run = script.Begin(new SaveData());

            Assert.IsTrue(run.IsEmpty);
            Assert.IsTrue(run.IsFinished);
            Assert.IsFalse(run.TryAdvance(out _));
        }

        [Test]
        public void Narration_HasNoSpeaker()
        {
            Assert.IsTrue(new ScriptLine(null, "the market hums").IsNarration);
            Assert.IsTrue(new ScriptLine(string.Empty, "the market hums").IsNarration);
            Assert.IsFalse(new ScriptLine("kleia", "hello").IsNarration);
        }

        [Test]
        public void Validate_AcceptsAWellFormedScript()
        {
            CollectionAssert.IsEmpty(Menexeno().Validate());
        }

        [Test]
        public void Validate_ReportsEmptyTextBadFlagsAndContradictoryConditions()
        {
            var script = new DialogueScript("broken", new[]
            {
                new ScriptLine("kleia", " "),
                new ScriptLine("kleia", "bad condition", new[] { new StoryCondition(MetFlag, "true", true) }),
                new ScriptLine("kleia", "bad effect", effects: new[] { new StoryEffect("Not A Flag") }),
                new ScriptLine("kleia", "bad act", effects: new[] { new StoryEffect(SaveFlags.Act, "9") })
            });

            var errors = script.Validate();

            Assert.AreEqual(4, errors.Count, string.Join("\n", errors));
            Assert.IsNotEmpty(new DialogueScript(string.Empty, new ScriptLine[0]).Validate());
        }

        private static DialogueScript Menexeno() => new("menexeno", new[]
        {
            new ScriptLine(string.Empty, "greeting", new[] { StoryCondition.Lacks(MetFlag) }),
            new ScriptLine("kleia", "question", new[] { StoryCondition.Lacks(MetFlag) }),
            new ScriptLine("menexeno", "answer", new[] { StoryCondition.Lacks(MetFlag) }, new[] { new StoryEffect(MetFlag) }),
            new ScriptLine("menexeno", "again", new[] { StoryCondition.Has(MetFlag) }),
            new ScriptLine("menexeno", "about eudoro", new[] { StoryCondition.Has(MetFlag), StoryCondition.Has(EudoroFlag) })
        });

        private static List<string> Texts(DialogueRun run)
        {
            var texts = new List<string>();
            while (run.TryAdvance(out var line))
            {
                texts.Add(line.Text);
            }

            return texts;
        }
    }
}
