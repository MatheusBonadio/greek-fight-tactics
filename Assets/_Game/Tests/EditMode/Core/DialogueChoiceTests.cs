using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class DialogueChoiceTests
    {
        private const string LessonsFlag = "thrasybios_lessons";
        private const string MetFlag = "met_thrasybios";
        private const string Accepted = "accepted";
        private const string Refused = "refused";

        [Test]
        public void ChoiceLine_HoldsTheConversationUntilAnOptionIsChosen()
        {
            var run = Offer().Begin(new SaveData());

            Assert.IsTrue(run.TryAdvance(out _));
            Assert.IsTrue(run.TryAdvance(out var offer));
            Assert.IsTrue(offer.HasChoices);
            Assert.IsTrue(run.IsAwaitingChoice);
            Assert.IsFalse(run.IsFinished, "a pending choice is not the end of the conversation");
            Assert.Throws<InvalidOperationException>(() => run.TryAdvance(out _), "the conversation cannot skip the choice");
        }

        [TestCase(0, "Aceitar", Accepted, "accept reply")]
        [TestCase(1, "Recusar", Refused, "refuse reply")]
        public void ChoosingAnOption_WritesItsFlagsPlaysItsBranchAndResumesAfterTheChoice(int option, string text, string value, string reply)
        {
            var save = new SaveData();
            var run = Offer().Begin(save);
            var shown = Advance(run);
            Assert.IsFalse(save.Flags.ContainsKey(LessonsFlag), "nothing is recorded before Kleia answers");

            var chosen = run.Choose(option);
            shown.AddRange(Advance(run));

            Assert.AreEqual(text, chosen.Text);
            Assert.AreEqual(value, save.Flags[LessonsFlag]);
            CollectionAssert.AreEqual(new[] { "laughter", "offer", reply, "after" }, shown);
            CollectionAssert.AreEqual(new[] { chosen }, run.Chosen);
            CollectionAssert.Contains(run.AppliedEffects.Select(effect => effect.ToString()).ToList(), $"{LessonsFlag}={value}");
            Assert.IsTrue(run.IsFinished);
            Assert.IsFalse(run.IsAwaitingChoice);
        }

        [Test]
        public void Choose_RejectsAnAnswerWithoutAQuestionAndAnOptionOutOfRange()
        {
            var run = Offer().Begin(new SaveData());
            Assert.Throws<InvalidOperationException>(() => run.Choose(0));

            Advance(run);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Choose(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Choose(2));
            Assert.IsTrue(run.IsAwaitingChoice, "a wrong index keeps the question open");

            run.Choose(1);
            Assert.Throws<InvalidOperationException>(() => run.Choose(0), "the same question is answered once");
        }

        [Test]
        public void BranchLines_AreJudgedWhenTheOptionIsChosen()
        {
            var script = new DialogueScript("gate", new[]
            {
                new ScriptLine("thrasybios", "question", choices: new[]
                {
                    new DialogueChoice("yes", new[] { new StoryEffect(MetFlag) }, new[]
                    {
                        new ScriptLine("thrasybios", "sees the flag the option just wrote", new[] { StoryCondition.Has(MetFlag) }),
                        new ScriptLine("thrasybios", "hidden", new[] { StoryCondition.Lacks(MetFlag) })
                    }),
                    new DialogueChoice("no")
                })
            });
            var run = script.Begin(new SaveData());

            Advance(run);
            run.Choose(0);

            CollectionAssert.AreEqual(new[] { "sees the flag the option just wrote" }, Advance(run));
        }

        [Test]
        public void NestedChoices_PlayOneAfterTheOther()
        {
            var script = new DialogueScript("nested", new[]
            {
                new ScriptLine("kleia", "first", choices: new[]
                {
                    new DialogueChoice("a", lines: new[]
                    {
                        new ScriptLine("kleia", "second", choices: new[] { new DialogueChoice("c", new[] { new StoryEffect("picked_c") }), new DialogueChoice("d", new[] { new StoryEffect("picked_d") }) })
                    }),
                    new DialogueChoice("b")
                }),
                new ScriptLine("kleia", "end")
            });
            var save = new SaveData();
            var run = script.Begin(save);

            Advance(run);
            run.Choose(0);
            CollectionAssert.AreEqual(new[] { "second" }, Advance(run));
            Assert.IsTrue(run.IsAwaitingChoice);
            run.Choose(1);
            CollectionAssert.AreEqual(new[] { "end" }, Advance(run));

            Assert.IsTrue(save.Flags.ContainsKey("picked_d"));
            Assert.IsFalse(save.Flags.ContainsKey("picked_c"));
            CollectionAssert.AreEqual(new[] { "first", "second", "end", "a", "b", "c", "d" }, script.AllLines().Select(line => line.Text).Concat(script.AllChoices().Select(choice => choice.Text)));
        }

        [Test]
        public void LinesWithoutChoices_KeepTheirOldBehaviour()
        {
            var line = new ScriptLine("kleia", "plain");

            Assert.IsFalse(line.HasChoices);
            CollectionAssert.IsEmpty(line.Choices);
            var run = new DialogueScript("plain", new[] { line }).Begin(new SaveData());
            Assert.IsTrue(run.TryAdvance(out _));
            Assert.IsFalse(run.IsAwaitingChoice);
            Assert.IsTrue(run.IsFinished);
        }

        [Test]
        public void Validate_AcceptsAWellFormedChoice()
        {
            CollectionAssert.IsEmpty(Offer().Validate());
        }

        [Test]
        public void Validate_ReportsTooFewOptionsEmptyOrLongTextBadEffectsAndBrokenBranchLines()
        {
            var tooLong = new string('a', DialogueChoice.MaxTextLength + 1);
            var tooMany = Enumerable.Range(0, DialogueChoice.MaxOptions + 1).Select(index => new DialogueChoice($"option {index}")).ToArray();
            var script = new DialogueScript("broken", new[]
            {
                new ScriptLine("kleia", "one option", choices: new[] { new DialogueChoice("alone") }),
                new ScriptLine("kleia", "too many", choices: tooMany),
                new ScriptLine("kleia", "bad options", choices: new[]
                {
                    new DialogueChoice(" "),
                    new DialogueChoice(tooLong),
                    new DialogueChoice("bad flag", new[] { new StoryEffect("Not A Flag") }),
                    new DialogueChoice("bad branch", lines: new[] { new ScriptLine("kleia", string.Empty) })
                })
            });

            var errors = script.Validate();

            Assert.AreEqual(6, errors.Count, string.Join("\n", errors));
            Assert.IsTrue(errors.Any(error => error.Contains("option 3 line 0")), string.Join("\n", errors));
        }

        private static DialogueScript Offer() => new("offer", new[]
        {
            new ScriptLine(string.Empty, "laughter"),
            new ScriptLine("thrasybios", "offer", choices: new[]
            {
                new DialogueChoice("Aceitar", new[] { new StoryEffect(LessonsFlag, Accepted) }, new[] { new ScriptLine("kleia", "accept reply") }),
                new DialogueChoice("Recusar", new[] { new StoryEffect(LessonsFlag, Refused) }, new[] { new ScriptLine("kleia", "refuse reply") })
            }),
            new ScriptLine(string.Empty, "after")
        });

        private static List<string> Advance(DialogueRun run)
        {
            var texts = new List<string>();
            while (!run.IsAwaitingChoice && run.TryAdvance(out var line))
            {
                texts.Add(line.Text);
            }

            return texts;
        }
    }
}
