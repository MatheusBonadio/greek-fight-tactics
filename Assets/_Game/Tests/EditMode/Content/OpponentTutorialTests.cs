using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Net;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Content
{
    public sealed class OpponentTutorialTests
    {
        private const string Thrasybios = "thrasybios";
        private const string TaughtFallacy = "equivocation";
        private const string LessonsFlag = "thrasybios_lessons";
        private const string CrowdSpeaker = "crowd";

        [Test]
        public void Thrasybios_TeachesEquivocationFromHisOwnPool()
        {
            var tutorial = Load(Thrasybios).Tutorial;

            Assert.IsTrue(tutorial.IsDefined);
            Assert.AreEqual(TaughtFallacy, tutorial.FallacyId, "GDD I.2: the amphora demonstration is an equivocation");
            CollectionAssert.Contains(Load(Thrasybios).FallacyPool.Select(fallacy => fallacy.Id).ToList(), tutorial.FallacyId);
            Assert.IsTrue(SaveFlags.IsCustomName(tutorial.DoneFlag));
        }

        [Test]
        public void Thrasybios_HasADialogueForEveryStepOfTheLesson()
        {
            var tutorial = Load(Thrasybios).Tutorial;
            var library = Library();

            foreach (var cue in Enum.GetValues(typeof(TutorialCue)).Cast<TutorialCue>().Where(cue => cue != TutorialCue.None))
            {
                var id = tutorial.DialogueFor(cue);
                Assert.IsNotNull(id, $"the tutorial has no dialogue for {cue}");
                Assert.IsTrue(library.TryGet(id, out var script), $"{cue} plays {id}, which is not in {DataTools.DialogueSourcePath}");
                Assert.IsFalse(script.Begin(new SaveData()).IsEmpty, $"{id} shows nothing in a new game");
            }

            Assert.IsNull(tutorial.DialogueFor(TutorialCue.None));
        }

        [Test]
        public void OnlyThrasybios_OpensWithATutorial()
        {
            foreach (var id in DataTools.OpponentIds().Where(id => id != Thrasybios))
            {
                Assert.IsFalse(Load(id).Tutorial.IsDefined, $"{id} must fight normally");
                Assert.IsFalse(Load(id).Tutorial.IsPending(new SaveData()));
            }
        }

        [Test]
        public void Tutorial_IsPendingUntilItsFlagIsSaved()
        {
            var tutorial = Load(Thrasybios).Tutorial;
            var save = new SaveData();

            Assert.IsTrue(tutorial.IsPending(save));
            Assert.IsFalse(tutorial.IsPending(null));
            Assert.IsTrue(new StoryEffect(tutorial.DoneFlag).TryApply(save, out _));
            Assert.IsFalse(tutorial.IsPending(save), "later fights with Thrasybios are normal");
        }

        [Test]
        public void TutorialArgument_OpensWithTheAmphoraDemonstration()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var fallback = AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(DataTools.FallbackLibraryPath);
            Assert.IsTrue(fallback.TryGetJson(Load(Thrasybios).Tutorial.FallacyId, out var json));

            var arguments = new List<Argument>();
            ArgumentJson.ParseValidList(json, new ArgumentValidator(catalog.RefutableIds), arguments);

            Assert.IsNotEmpty(arguments);
            Assert.AreEqual(TaughtFallacy, arguments[0].FallacyId);
            StringAssert.Contains("ânfora", arguments[0].Speech, "the lesson refutes the argument Thrasybios uses on Kleia's customer");
        }

        [Test]
        public void VictoryScene_LaughsAtKleiaAndRecordsTheLessonOffer()
        {
            var library = Library();
            Assert.IsTrue(library.TryGet(Load(Thrasybios).Tutorial.DialogueFor(TutorialCue.Victory), out var scene));

            Assert.IsTrue(scene.AllLines().Any(line => line.SpeakerId == CrowdSpeaker), "the crowd has to laugh in the scene");
            var offer = scene.AllLines().Single(line => line.HasChoices);
            Assert.AreEqual(2, offer.Choices.Count);
            var values = offer.Choices.Select(choice => choice.Effects.Single(effect => effect.Flag == LessonsFlag).Value).ToList();
            CollectionAssert.AreEquivalent(new[] { "accepted", "refused" }, values);
            Assert.IsTrue(offer.Choices.All(choice => choice.Lines.Count > 0), "each answer gets its own reply");

            foreach (var option in offer.Choices.Select((choice, index) => index))
            {
                var save = new SaveData();
                var run = scene.Begin(save);
                while (!run.IsAwaitingChoice && run.TryAdvance(out _))
                {
                }

                Assert.IsTrue(run.IsAwaitingChoice);
                run.Choose(option);
                while (run.TryAdvance(out _))
                {
                }

                Assert.AreEqual(values[option], save.Flags[LessonsFlag]);
            }
        }

        private static OpponentSO Load(string id)
        {
            var opponent = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(id));
            Assert.IsNotNull(opponent, $"run DataTools.Regenerate to create {DataTools.OpponentPath(id)}");
            return opponent;
        }

        private static DialogueLibrarySO Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<DialogueLibrarySO>(DataTools.DialogueLibraryPath);
            Assert.IsNotNull(library, $"run DataTools.Regenerate to create {DataTools.DialogueLibraryPath}");
            return library;
        }
    }
}
