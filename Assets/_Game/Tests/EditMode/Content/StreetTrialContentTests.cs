using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Grid;
using Game.Tests.EditMode.Battle;
using Game.UI;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Content
{
    public sealed class StreetTrialContentTests
    {
        private const string Boss = "thrasybios_street_trial";
        private const string Tutor = "thrasybios";
        private const string LostFlag = "lost_street_trial";
        private const string CalledFlag = "street_trial_called";
        private const int PlayerId = 1;
        private const int OpponentId = 2;

        [Test]
        public void StreetTrial_IsItsOwnOpponentWearingThrasybiosSheet()
        {
            var boss = Load(Boss);
            var tutor = Load(Tutor);

            Assert.AreNotEqual(tutor.Id, boss.Id, "the tutorial duel and the old saves keep duel_thrasybios for themselves");
            Assert.AreEqual(tutor.DisplayName, boss.DisplayName);
            Assert.AreEqual(Tutor, boss.SheetId);
            Assert.AreSame(tutor.Sheet, boss.Sheet);
            Assert.IsFalse(boss.Tutorial.IsDefined, "the trial is not a lesson");
            Assert.IsTrue(boss.IsPhased);
            Assert.IsFalse(tutor.IsPhased, "the tutorial Thrasybios still fights like before");
            Assert.AreEqual(Act.One, boss.Act);
        }

        [Test]
        public void StreetTrial_RunsTheThreePhasesOfTheDesignDocument()
        {
            var phases = Load(Boss).Phases.Phases;

            CollectionAssert.AreEqual(new[] { "personal_attack", "false_dilemma", "political_fear" }, phases.Select(phase => phase.Id));
            CollectionAssert.AreEqual(new[] { ActionId.Concede, ActionId.Refute, ActionId.Maieutics }, phases.Select(phase => phase.Counter), "GDD I.8: concede, refute, then only maieutics");
            CollectionAssert.AreEqual(new[] { "ad_hominem", "false_dilemma", "appeal_to_emotion" }, phases.Select(phase => phase.Arguments.Select(argument => argument.FallacyId).Distinct().Single()));
            Assert.IsTrue(phases[1].Arguments.Any(argument => argument.Speech.Contains("Sócrates")), "the dilemma is the one of the design document");
            Assert.IsTrue(phases[2].Arguments.All(argument => argument.Speech.Contains("Trinta")), "the last phase invokes the Thirty");
            Assert.IsTrue(phases[2].Arguments.All(argument => argument.Premises[argument.WeakPremise].Contains("Thrasybios")), "the question that works is about what he himself did under the tyranny");
            Assert.IsTrue(phases.All(phase => phase.Arguments.Count >= 2), "every phase has more than one attack, so it works offline without repeating at once");
        }

        [Test]
        public void EveryPhase_ChangesTheMusicAndThePosture()
        {
            var phases = Load(Boss).Phases.Phases;
            var library = Library();

            Assert.AreEqual(phases.Count, phases.Select(phase => phase.Music).Distinct().Count(), "each phase has its own music cue");
            for (var index = 0; index < phases.Count; index++)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(phases[index].Music));
                Assert.IsTrue(library.TryGet(phases[index].Dialogue, out var script), phases[index].Dialogue);
                Assert.IsFalse(script.Begin(new SaveData()).IsEmpty);
                if (index > 0)
                {
                    var changes = phases[index].Posture != phases[index - 1].Posture || phases[index].Tint != phases[index - 1].Tint;
                    Assert.IsTrue(changes, $"{phases[index].Id} must look different from {phases[index - 1].Id}");
                }
            }

            Assert.AreEqual(Posture.Action, phases[1].Posture, "he gestures while he offers the dilemma");
            Assert.AreNotEqual(UnityEngine.Color.white, phases[2].Tint, "he goes pale when he invokes the Thirty");
        }

        [Test]
        public void StreetTrial_IsWinnableOfflineWithTheRightCountersOnlyOnTheStoaSteps()
        {
            var boss = Load(Boss);
            var map = MapLayout.Load(SandboxTools.MapPath);
            var placement = map.Actors.Single(actor => actor.Character == Boss);

            BattleState Duel(IPlayerPolicy player)
            {
                var duel = boss.Phases.ToDuel();
                var grid = GridFactoryFor(map);
                var state = BattleSession.Start(grid, new BattleSetup
                {
                    PlayerId = PlayerId,
                    PlayerCell = new Coord(placement.X - 1, placement.Y),
                    OpponentId = OpponentId,
                    OpponentCell = new Coord(placement.X, placement.Y),
                    OpponentFacing = placement.Facing,
                    OpponentProfile = boss.Profile,
                    OpponentMaxCoherence = boss.MaxCoherence,
                    InitialAudience = boss.InitialAudience,
                    Act = boss.Act,
                    Rules = duel.Rules()
                }, null, new PhasedOpponent(duel)).State;
                BattleSimulator.Run(state, player, new PhasedOpponent(duel), null);
                return state;
            }

            Assert.AreEqual(BattleStatus.PlayerWon, Duel(new CounterPlayerPolicy()).Status, "the authored arguments alone carry the trial");
            Assert.AreEqual(BattleStatus.PlayerLost, Duel(new RefuteEverythingPolicy()).Status, "refuting the personal attack loses the trial");
        }

        [Test]
        public void LosingTheTrial_KeepsThrasybiosOnTheStepsAndTheRetryOpensDifferently()
        {
            var library = Library();
            var boss = MapLayout.Load(SandboxTools.MapPath).Actors.Single(actor => actor.Character == Boss);
            var save = new SaveData();
            save.Flags[CalledFlag] = StoryEffect.DefaultValue;
            Assert.IsTrue(StoryCondition.AllMet(boss.StoryConditions, save), "once called, he waits on the steps");

            Assert.IsTrue(library.TryGet(Load(Boss).Phases.Intro, out var intro));
            var first = Said(intro, save);

            Assert.IsTrue(library.TryGet(Load(Boss).Phases.Defeat, out var defeat));
            Said(defeat, save);
            Assert.IsTrue(StoryCondition.Has(LostFlag).IsMet(save), "the defeat scene remembers the loss");
            DuelStoryResults.ResultOf(Boss, BattleStatus.PlayerLost).TryApply(save, out _);
            Assert.IsTrue(StoryCondition.AllMet(boss.StoryConditions, save), "defeat is not game over: he is still there to be challenged");

            var retry = Said(intro, save);
            Assert.IsNotEmpty(retry);
            CollectionAssert.IsEmpty(first.Intersect(retry), "the second attempt opens with different lines");

            DuelStoryResults.ResultOf(Boss, BattleStatus.PlayerWon).TryApply(save, out _);
            Assert.IsFalse(StoryCondition.AllMet(boss.StoryConditions, save), "after the win he leaves the steps");
        }

        [Test]
        public void JurorList_RevealsMenexenosVoteAndTheSealOpensTheSecondAct()
        {
            var library = Library();
            var save = new SaveData();

            Assert.IsTrue(library.TryGet("archive_juror_list", out var list));
            var read = Said(list, save);
            Assert.IsTrue(read.Any(text => text.Contains("Sócrates")), "the list is the trial of Socrates");
            Assert.IsTrue(read.Any(text => text.Contains("condenação") && text.Contains("Menexeno")), "Menexeno voted for the condemnation");
            Assert.IsTrue(read.Any(text => text.Contains("dívida")), "he was paying a debt");
            Assert.IsTrue(StoryCondition.Has("knows_menexeno_vote").IsMet(save));
            Assert.AreEqual(Act.One, save.ActFlag, "reading the list is not the end of the act");

            Assert.IsTrue(library.TryGet("archive_bronze_seal", out var seal));
            var handed = Said(seal, save);
            Assert.IsTrue(handed.Any(text => text.Contains("selo de bronze")));
            Assert.IsTrue(handed.Any(text => text.Contains("convite")), "an invitation, not an admission");
            Assert.AreEqual(Act.Two, save.ActFlag, "the seal sets ActFlag = 2");
            Assert.IsTrue(StoryCondition.Has("bronze_seal").IsMet(save));
        }

        [Test]
        public void ParsePhases_RejectsDuelsThatCannotBePlayed()
        {
            var fallacies = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath).All.ToList();
            var dialogues = new HashSet<string>(Library().Ids);

            Assert.IsTrue(DataTools.ParsePhases(Opponent(), fallacies, dialogues).IsDefined);
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(dialogue: "nowhere"), fallacies, dialogues), "unknown dialogue");
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(music: "Not Snake"), fallacies, dialogues), "music cue id");
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(fallacy: "straw_man"), fallacies, dialogues), "argument outside the pool");
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(speech: "Curto."), fallacies, dialogues), "argument that fails validation");
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(tint: "#D1C8F8"), fallacies, dialogues), "tint outside RGB555");
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(counter: "Circulate"), fallacies, dialogues), "moving does not answer");
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(floor: 60), fallacies, dialogues), "the first phase must end below the starting coherence");
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(tutorial: true), fallacies, dialogues), "a lesson cannot be phased");
            Assert.IsTrue(DataTools.ParsePhases(Opponent(counter: "Maieutics"), fallacies, dialogues).IsDefined);
            Assert.Throws<InvalidOperationException>(() => DataTools.ParsePhases(Opponent(counter: "Maieutics", weakPremise: "null"), fallacies, dialogues), "no question answers sound premises");
        }

        private static string Opponent(string dialogue = "street_trial_personal_attack", string music = "street_trial_personal_attack", string fallacy = "ad_hominem",
            string speech = "Olhem bem para ela: filha de oleiro, mãos sujas de argila. Quem nasceu no Cerâmico não ensina nada.", string tint = "#D0C8F8", string counter = "Concede", int floor = 0, bool tutorial = false,
            string weakPremise = "1") =>
            "{\"id\":\"trial\",\"display_name\":\"Trial\",\"profile\":\"Sophist\",\"act\":\"One\",\"max_coherence\":60,\"initial_audience\":30,\"fallacy_pool\":[\"ad_hominem\"]," +
            (tutorial ? "\"tutorial\":{\"fallacy\":\"ad_hominem\",\"done_flag\":\"trial_done\"}," : string.Empty) +
            "\"phased_duel\":{\"intro\":\"street_trial_intro\",\"phases\":[{\"id\":\"only\",\"counter\":\"" + counter + "\",\"until_coherence\":" + floor + ",\"dialogue\":\"" + dialogue + "\",\"music\":\"" + music + "\",\"tint\":\"" + tint + "\"," +
            "\"arguments\":[{\"speech\":\"" + speech + "\",\"fallacy_id\":\"" + fallacy + "\",\"premises\":[\"Kleia é do Cerâmico\",\"Oleiros não ensinam\"],\"weak_premise\":" + weakPremise + ",\"tone\":\"mocking\"}]}]}}";

        private static GridModel GridFactoryFor(MapLayout map)
        {
            var grid = new GridModel(map.Width, map.Height);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    grid.TrySet(new Coord(x, y), new TileData
                    {
                        Terrain = TerrainId.Marble,
                        Cost = map.IsWalkable(x, y) ? (byte)PathFinder.MinimumStepCost : TileData.ImpassableCost,
                        Level = (byte)map.LevelAt(x, y),
                        Revealed = true
                    });
                }
            }

            return grid;
        }

        private static List<string> Said(DialogueScript script, SaveData save)
        {
            var run = script.Begin(save);
            var shown = new List<string>();
            while (run.TryAdvance(out var line))
            {
                shown.Add(line.Text);
            }

            return shown;
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
