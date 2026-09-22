using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Content
{
    public sealed class DialogueLibraryTests
    {
        private const string AristeuDialogue = "aristeu_workshop";
        private const string AristeuSpeaker = "aristeu";
        private const string AristeuErrand = "aristeu_errand";
        private const string MenexenoDoorDialogue = "menexeno_workshop_door";

        [Test]
        public void GeneratedLibrary_HoldsEverySourceDialogueLineByLine()
        {
            var library = LoadLibrary();
            var (speakers, scripts) = DataTools.LoadDialogueSource(DataTools.DialogueSourcePath);

            CollectionAssert.AreEquivalent(scripts.Select(script => script.Id), library.Ids);
            CollectionAssert.AreEquivalent(speakers.Keys, library.Speakers.Keys);
            foreach (var expected in scripts)
            {
                Assert.IsTrue(library.TryGet(expected.Id, out var actual), expected.Id);
                AssertSameLines(expected, actual);
                CollectionAssert.IsEmpty(actual.Validate());
            }
        }

        [Test]
        public void Library_RoundTripsScriptsAndNamesSpeakers()
        {
            var library = ScriptableObject.CreateInstance<DialogueLibrarySO>();
            try
            {
                var script = new DialogueScript("talk", new[]
                {
                    new ScriptLine(string.Empty, "narration"),
                    new ScriptLine("kleia", "line", new[] { StoryCondition.Lacks("met"), StoryCondition.Is(SaveFlags.Act, "1") }, new[] { new StoryEffect("met"), new StoryEffect(SaveFlags.Reputation, "2") })
                });
                library.SetEntries(new Dictionary<string, DialogueSpeaker> { ["kleia"] = new("Kleia", "kleia") }, new[] { script });

                Assert.IsTrue(library.Contains("talk"));
                Assert.IsFalse(library.TryGet("missing", out _));
                Assert.IsTrue(library.TryGet("talk", out var restored));
                AssertSameLines(script, restored);
                Assert.AreEqual("Kleia", library.SpeakerName("kleia"));
                Assert.AreEqual(string.Empty, library.SpeakerName(string.Empty));
                Assert.AreEqual("#eudoro", library.SpeakerName("eudoro"));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void Maps_UseOnlyDialoguesAndPortraitsThatExist()
        {
            var library = LoadLibrary();
            foreach (var entry in SandboxTools.Maps)
            {
                var map = MapLayout.Load(entry.MapPath);
                CollectionAssert.IsSubsetOf(map.DialogueIds.ToList(), library.Ids.ToList(), $"{entry.LocationId} talks through a dialogue that is not in {DataTools.DialogueSourcePath}");
            }

            foreach (var speaker in library.Speakers.Where(pair => pair.Value.HasPortrait))
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTools.SheetPathFor(speaker.Value.Portrait)), $"speaker {speaker.Key} has no sheet for its portrait");
            }
        }

        [Test]
        public void Maps_GiveTheNpcsOnTheAgoraAndTheKerameikosSomethingToSay()
        {
            var agora = MapLayout.Load(SandboxTools.MapPath);
            var kerameikos = MapLayout.Load(SandboxTools.KerameikosMapPath);

            Assert.IsTrue(agora.Actors.Any(actor => actor.Character == "menexeno" && !string.IsNullOrEmpty(actor.Dialogue)));
            Assert.IsTrue(agora.Actors.Any(actor => actor.Character == "eudoro" && !string.IsNullOrEmpty(actor.Dialogue)));
            Assert.IsTrue(kerameikos.Actors.Skip(1).All(actor => !string.IsNullOrEmpty(actor.Dialogue)));
            Assert.IsNotEmpty(kerameikos.Triggers, "leaving the workshop plays Kleia's first thought");
            Assert.IsTrue(agora.Actors.Where(actor => DataTools.OpponentIds().Contains(actor.Character)).All(actor => string.IsNullOrEmpty(actor.Dialogue)), "opponents answer with a battle, not a chat");
        }

        [Test]
        public void DialogueText_UsesOnlyGlyphsBakedIntoThePixelFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontTools.PixelFontAssetPath);
            Assert.IsNotNull(font, "run FontTools.BuildPixelFont to create the pixel font asset");

            var library = LoadLibrary();
            var text = string.Concat(library.Ids.SelectMany(id => library.TryGet(id, out var script) ? script.Lines.Select(line => line.Text) : Enumerable.Empty<string>()))
                + string.Concat(library.Speakers.Values.Select(speaker => speaker.DisplayName));
            var glyphs = new string(text.Where(character => !char.IsWhiteSpace(character)).Distinct().ToArray());

            Assert.IsTrue(font.HasCharacters(glyphs, out uint[] missing, false, false),
                $"missing glyphs: {string.Join(", ", (missing ?? new uint[0]).Select(code => $"U+{code:X4}"))}");
        }

        [Test]
        public void Aristeu_SendsKleiaToTheAgoraBecauseOfHisArthriticHands()
        {
            var library = LoadLibrary();
            Assert.IsTrue(library.TryGet(AristeuDialogue, out var script));
            Assert.AreEqual("Aristeu", library.SpeakerName(AristeuSpeaker));
            Assert.AreEqual(AristeuSpeaker, library.Speakers[AristeuSpeaker].Portrait);

            var save = new SaveData();
            var first = PlayThrough(script, save);
            Assert.IsTrue(first.Any(line => line.SpeakerId == AristeuSpeaker));
            Assert.IsTrue(first.Any(line => line.IsNarration && line.Text.Contains("dedos")), "the scene shows his crooked fingers");
            Assert.IsTrue(StoryCondition.Has(AristeuErrand).IsMet(save));

            var again = PlayThrough(script, save);
            Assert.IsNotEmpty(again);
            Assert.Less(again.Count, first.Count, "the second talk only reminds her of the errand");
        }

        [Test]
        public void MenexenoAtTheDoor_SetsHisConditionHidesHisPastAndUnlocksMaieuticsOnce()
        {
            var library = LoadLibrary();
            Assert.IsTrue(library.TryGet(MenexenoDoorDialogue, out var script));

            var save = new SaveData();
            save.Flags[DuelStoryFlags.FlagFor("thrasybios")] = DuelStoryFlags.Won;
            var first = PlayThrough(script, save);

            Assert.IsTrue(first.Any(line => line.SpeakerId == "menexeno" && line.Text.Contains("dinheiro")), "he states his condition");
            foreach (var flag in new[] { ActionUnlocks.MaieuticsFlag, "menexeno_condition", "menexeno_avoids_past" })
            {
                Assert.IsTrue(StoryCondition.Has(flag).IsMet(save), flag);
            }

            var again = PlayThrough(script, save);
            Assert.IsNotEmpty(again);
            CollectionAssert.IsEmpty(again.Select(line => line.Text).Intersect(first.Select(line => line.Text)), "the lesson is not repeated");
        }

        private static List<ScriptLine> PlayThrough(DialogueScript script, SaveData save)
        {
            var run = script.Begin(save);
            var shown = new List<ScriptLine>();
            while (run.TryAdvance(out var line))
            {
                shown.Add(line);
            }

            return shown;
        }

        private static DialogueLibrarySO LoadLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<DialogueLibrarySO>(DataTools.DialogueLibraryPath);
            Assert.IsNotNull(library, $"run DataTools.Regenerate to create {DataTools.DialogueLibraryPath}");
            return library;
        }

        private static void AssertSameLines(DialogueScript expected, DialogueScript actual)
        {
            Assert.AreEqual(expected.Lines.Count, actual.Lines.Count, expected.Id);
            for (var index = 0; index < expected.Lines.Count; index++)
            {
                var want = expected.Lines[index];
                var got = actual.Lines[index];
                Assert.AreEqual(want.SpeakerId, got.SpeakerId);
                Assert.AreEqual(want.Text, got.Text);
                CollectionAssert.AreEqual(want.Conditions.Select(condition => condition.ToString()), got.Conditions.Select(condition => condition.ToString()));
                CollectionAssert.AreEqual(want.Effects.Select(effect => effect.ToString()), got.Effects.Select(effect => effect.ToString()));
            }
        }
    }
}
