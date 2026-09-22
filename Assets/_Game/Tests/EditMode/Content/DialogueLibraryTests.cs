using System.Collections.Generic;
using System.IO;
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
        private const string MenexenoAgoraDialogue = "menexeno_agora";
        private const string HeadedHomeFlag = "headed_home";
        private const string PreDuelAdvice = "terminar de falar";
        private static readonly string[] MenexenoDoorDialogues = { "menexeno_door_offer", "menexeno_door_glance", "menexeno_door_lesson", "menexeno_door_learned" };
        private const string LegacyEntryJson =
            "{\"lines\":[{\"speaker\":\"kleia\",\"text\":\"old line\",\"conditions\":[{\"flag\":\"met\",\"value\":\"\",\"absent\":true}],\"effects\":[{\"flag\":\"met\",\"value\":\"true\"}]}]}";

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
        public void Library_RoundTripsChoicesWithNestedBranches()
        {
            var library = ScriptableObject.CreateInstance<DialogueLibrarySO>();
            try
            {
                var script = new DialogueScript("offer", new[]
                {
                    new ScriptLine(string.Empty, "before"),
                    new ScriptLine("thrasybios", "offer", choices: new[]
                    {
                        new DialogueChoice("Aceitar", new[] { new StoryEffect("lessons", "accepted") }, new[]
                        {
                            new ScriptLine("kleia", "accepted", new[] { StoryCondition.Has("lessons") }),
                            new ScriptLine("thrasybios", "pay now", choices: new[]
                            {
                                new DialogueChoice("Pagar", new[] { new StoryEffect("paid") }, new[] { new ScriptLine("thrasybios", "thanks") }),
                                new DialogueChoice("Depois")
                            })
                        }),
                        new DialogueChoice("Recusar", new[] { new StoryEffect("lessons", "refused") }, new[] { new ScriptLine("kleia", "refused") })
                    }),
                    new ScriptLine(string.Empty, "after")
                });
                library.SetEntries(new Dictionary<string, DialogueSpeaker>(), new[] { script });

                Assert.IsTrue(library.TryGet("offer", out var restored));
                AssertSameLines(script, restored);
                CollectionAssert.IsEmpty(restored.Validate());

                var save = new SaveData();
                var run = restored.Begin(save);
                var shown = new List<string>();
                foreach (var answer in new[] { 0, 0 })
                {
                    while (!run.IsAwaitingChoice && run.TryAdvance(out var line))
                    {
                        shown.Add(line.Text);
                    }

                    run.Choose(answer);
                }

                while (run.TryAdvance(out var rest))
                {
                    shown.Add(rest.Text);
                }

                CollectionAssert.AreEqual(new[] { "before", "offer", "accepted", "pay now", "thanks", "after" }, shown);
                Assert.AreEqual("accepted", save.Flags["lessons"]);
                Assert.AreEqual(StoryEffect.DefaultValue, save.Flags["paid"]);
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void EntrySavedBeforeChoicesExisted_StillLoadsAsPlainLines()
        {
            var legacy = JsonUtility.FromJson<DialogueEntry>(LegacyEntryJson);

            var script = legacy.ToScript("legacy");

            Assert.AreEqual(1, script.Lines.Count);
            Assert.AreEqual("old line", script.Lines[0].Text);
            Assert.IsFalse(script.Lines[0].HasChoices);
            CollectionAssert.IsEmpty(script.AllChoices());
            CollectionAssert.IsEmpty(script.Validate());
            Assert.IsTrue(script.Begin(new SaveData()).TryAdvance(out _));
        }

        [Test]
        public void DialogueSource_ReadsChoicesAndRejectsBrokenOnes()
        {
            var path = Path.Combine(Path.GetTempPath(), $"gft_dialogues_{System.Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(path, ChoiceSource("Aceitar", "kleia"));
                var (_, scripts) = DataTools.LoadDialogueSource(path);
                var offer = scripts.Single().Lines.Single();
                Assert.AreEqual(2, offer.Choices.Count);
                Assert.AreEqual("Aceitar", offer.Choices[0].Text);
                Assert.AreEqual("lessons=accepted", offer.Choices[0].Effects.Single().ToString());
                Assert.AreEqual("kleia", offer.Choices[0].Lines.Single().SpeakerId);
                CollectionAssert.IsEmpty(offer.Choices[1].Lines);

                File.WriteAllText(path, ChoiceSource(" ", "kleia"));
                Assert.Throws<System.InvalidOperationException>(() => DataTools.LoadDialogueSource(path), "an option needs text");

                File.WriteAllText(path, ChoiceSource("Aceitar", "stranger"));
                var error = Assert.Throws<System.InvalidOperationException>(() => DataTools.LoadDialogueSource(path));
                StringAssert.Contains("unknown speaker stranger", error.Message, "the speakers inside an option are checked too");
            }
            finally
            {
                File.Delete(path);
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
            Assert.IsTrue(kerameikos.Actors.Skip(1).All(actor => !string.IsNullOrEmpty(actor.Dialogue) || kerameikos.Triggers.Any(trigger => trigger.IsCutscene && System.Math.Abs(trigger.X - actor.X) + System.Math.Abs(trigger.Y - actor.Y) == 1)),
                "every workshop NPC talks, or stands beside the cutscene that speaks for him");
            Assert.IsNotEmpty(kerameikos.Triggers, "leaving the workshop plays Kleia's first thought");
            Assert.IsTrue(agora.Actors.Where(actor => DataTools.OpponentIds().Contains(actor.Character)).All(actor => string.IsNullOrEmpty(actor.Dialogue)), "opponents answer with a battle, not a chat");
        }

        [Test]
        public void DialogueText_UsesOnlyGlyphsBakedIntoThePixelFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontTools.PixelFontAssetPath);
            Assert.IsNotNull(font, "run FontTools.BuildPixelFont to create the pixel font asset");

            var library = LoadLibrary();
            var text = string.Concat(library.Ids.SelectMany(id => library.TryGet(id, out var script) ? script.AllLines().Select(line => line.Text).Concat(script.AllChoices().Select(choice => choice.Text)) : Enumerable.Empty<string>()))
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
        public void MenexenoAtTheDoor_SetsHisConditionHidesHisPastAndUnlocksMaieuticsInOrder()
        {
            var library = LoadLibrary();
            var save = new SaveData();
            save.Flags[DuelStoryFlags.FlagFor("thrasybios")] = DuelStoryFlags.Won;
            var spoken = new List<ScriptLine>();
            var flagsAfter = new List<string[]>();

            foreach (var id in MenexenoDoorDialogues)
            {
                Assert.IsTrue(library.TryGet(id, out var script), id);
                spoken.AddRange(PlayThrough(script, save));
                flagsAfter.Add(new[] { "menexeno_condition", "menexeno_avoids_past", ActionUnlocks.MaieuticsFlag }.Where(flag => StoryCondition.Has(flag).IsMet(save)).ToArray());
            }

            Assert.IsTrue(spoken.Any(line => line.SpeakerId == "menexeno" && line.Text.Contains("dinheiro")), "he states his condition");
            CollectionAssert.AreEqual(new[] { "menexeno_condition" }, flagsAfter[0], "the offer only sets his condition");
            CollectionAssert.AreEqual(new[] { "menexeno_condition", "menexeno_avoids_past" }, flagsAfter[1], "his glance plants the clue");
            CollectionAssert.DoesNotContain(flagsAfter[2], ActionUnlocks.MaieuticsFlag, "the lesson ends before Kleia names it");
            CollectionAssert.Contains(flagsAfter[3], ActionUnlocks.MaieuticsFlag);
            Assert.IsTrue(spoken.Last().IsNarration && spoken.Last().Text.Contains("MAIÊUTICA"), "the scene closes on what she learned");
        }

        [Test]
        public void AgoraAfterThrasybios_PointsKleiaBackToTheWorkshop()
        {
            var library = LoadLibrary();
            Assert.IsTrue(library.TryGet("agora_after_thrasybios", out var script));

            var lines = PlayThrough(script, new SaveData());
            Assert.IsTrue(lines.Any(line => line.Text.Contains("oficina")), "she thinks of home");
            Assert.IsTrue(lines.Any(line => line.Text.Contains("Cerâmico")), "the line names the way back");
        }

        [Test]
        public void MenexenoInTheAgora_DropsHisPreDuelAdviceOnceKleiaIsHeadedHomeAndAfterTheLesson()
        {
            var library = LoadLibrary();
            Assert.IsTrue(library.TryGet(MenexenoAgoraDialogue, out var script));
            var save = new SaveData();
            PlayThrough(script, save);

            var beforeDuel = Said(script, save);
            Assert.IsTrue(beforeDuel.Any(text => text.Contains(PreDuelAdvice)), "before the duel he tells her to let Thrasybios finish");

            save.Flags[DuelStoryFlags.FlagFor("thrasybios")] = DuelStoryFlags.Won;
            save.Flags[HeadedHomeFlag] = StoryEffect.DefaultValue;
            var headedHome = Said(script, save);
            Assert.IsFalse(headedHome.Any(text => text.Contains(PreDuelAdvice)), "the duel is over");
            Assert.IsTrue(headedHome.Any(text => text.Contains("para casa")), "he sends her home, where he will follow");

            save.Flags[ActionUnlocks.MaieuticsFlag] = StoryEffect.DefaultValue;
            save.Flags["menexeno_avoids_past"] = StoryEffect.DefaultValue;
            var taught = Said(script, save);
            Assert.IsFalse(taught.Any(text => text.Contains(PreDuelAdvice) || text.Contains("para casa")));
            Assert.IsTrue(taught.Any(text => text.Contains("Não cobre")), "after the lesson he repeats his condition");

            var metAtTheDoor = new SaveData();
            foreach (var id in MenexenoDoorDialogues)
            {
                Assert.IsTrue(library.TryGet(id, out var door), id);
                PlayThrough(door, metAtTheDoor);
            }

            Assert.IsFalse(Said(script, metAtTheDoor).Any(text => text.Contains("Procura alguma coisa")), "Kleia already knows him from the door");
        }

        private static List<string> Said(DialogueScript script, SaveData save) => PlayThrough(script, save).Select(line => line.Text).ToList();

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
        private static string ChoiceSource(string firstOption, string branchSpeaker) =>
            "{\"speakers\":{\"kleia\":{\"name\":\"Kleia\"}},\"dialogues\":[{\"id\":\"offer\",\"lines\":[{\"speaker\":\"kleia\",\"text\":\"Aceita?\",\"choices\":["
            + $"{{\"text\":\"{firstOption}\",\"effects\":[{{\"flag\":\"lessons\",\"value\":\"accepted\"}}],\"lines\":[{{\"speaker\":\"{branchSpeaker}\",\"text\":\"Sim.\"}}]}},"
            + "{\"text\":\"Recusar\"}]}]}]}";

        private static DialogueLibrarySO LoadLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<DialogueLibrarySO>(DataTools.DialogueLibraryPath);
            Assert.IsNotNull(library, $"run DataTools.Regenerate to create {DataTools.DialogueLibraryPath}");
            return library;
        }

        private static void AssertSameLines(DialogueScript expected, DialogueScript actual)
        {
            AssertSameLines(expected.Lines, actual.Lines, expected.Id);
        }

        private static void AssertSameLines(IReadOnlyList<ScriptLine> expected, IReadOnlyList<ScriptLine> actual, string where)
        {
            Assert.AreEqual(expected.Count, actual.Count, where);
            for (var index = 0; index < expected.Count; index++)
            {
                var want = expected[index];
                var got = actual[index];
                Assert.AreEqual(want.SpeakerId, got.SpeakerId, where);
                Assert.AreEqual(want.Text, got.Text, where);
                CollectionAssert.AreEqual(want.Conditions.Select(condition => condition.ToString()), got.Conditions.Select(condition => condition.ToString()), where);
                CollectionAssert.AreEqual(want.Effects.Select(effect => effect.ToString()), got.Effects.Select(effect => effect.ToString()), where);
                Assert.AreEqual(want.Choices.Count, got.Choices.Count, $"{where} line {index}");
                for (var option = 0; option < want.Choices.Count; option++)
                {
                    var at = $"{where} line {index} option {option}";
                    Assert.AreEqual(want.Choices[option].Text, got.Choices[option].Text, at);
                    CollectionAssert.AreEqual(want.Choices[option].Effects.Select(effect => effect.ToString()), got.Choices[option].Effects.Select(effect => effect.ToString()), at);
                    AssertSameLines(want.Choices[option].Lines, got.Choices[option].Lines, at);
                }
            }
        }
    }
}
