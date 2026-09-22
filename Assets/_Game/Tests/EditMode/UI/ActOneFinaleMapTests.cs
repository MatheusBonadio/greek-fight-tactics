using System.Linq;
using Game.Battle;
using Game.Core;
using Game.EditorTools;
using Game.Grid;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class ActOneFinaleMapTests
    {
        private const string Boss = "thrasybios_street_trial";
        private const string Tutor = "thrasybios";
        private const string Menexeno = "menexeno";
        private const string CalledFlag = "street_trial_called";
        private const string SearchingFlag = "searching_menexeno";
        private const string StoaTile = "floor_marble";

        [Test]
        public void StreetTrial_TakesTheStoaStepsOnlyOnceDamarisIsDecidedAndMaieuticsIsTaught()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var boss = map.Actors.Single(actor => actor.Character == Boss);
            var tutor = map.Actors.Single(actor => actor.Character == Tutor);
            var summons = map.Cues.Single(cue => cue.Flag == CalledFlag);
            var summonsConditions = summons.StoryConditions.Select(condition => condition.ToString()).ToList();

            Assert.AreEqual(Tutor, boss.SheetId, "the trial is Thrasybios himself");
            Assert.AreEqual(StoaTile, map.FloorAt(boss.X, boss.Y));
            Assert.GreaterOrEqual(map.LevelAt(boss.X, boss.Y), 2, "he waits at the top of the stoa steps");
            Assert.Greater(map.LevelAt(boss.X, boss.Y), map.LevelAt(tutor.X, tutor.Y), "the steps stand above the market");
            Assert.AreEqual(ExplorationCue.Idle, summons.On);
            CollectionAssert.Contains(summonsConditions, StoryCondition.Not(SaveFlags.DamarisBranch, DamarisChoice.Undecided.ToString().ToLowerInvariant()).ToString(), "the trial waits for the Damaris choice");
            CollectionAssert.Contains(summonsConditions, StoryCondition.Has(ActionUnlocks.MaieuticsFlag).ToString(), "the last phase needs maieutics");
            CollectionAssert.Contains(summonsConditions, StoryCondition.Is(DuelStoryFlags.FlagFor(Tutor), DuelStoryFlags.Won).ToString(), "the lesson comes first");

            var save = new SaveData();
            Assert.IsFalse(StoryCondition.AllMet(summons.StoryConditions, save));
            save.Flags[DuelStoryFlags.FlagFor(Tutor)] = DuelStoryFlags.Won;
            save.Flags[ActionUnlocks.MaieuticsFlag] = StoryEffect.DefaultValue;
            Assert.IsFalse(StoryCondition.AllMet(summons.StoryConditions, save), "an undecided Damaris keeps the trial away");
            save.DamarisBranch = DamarisChoice.Accepted;
            Assert.IsTrue(StoryCondition.AllMet(summons.StoryConditions, save));
            save.DamarisBranch = DamarisChoice.Refused;
            Assert.IsTrue(StoryCondition.AllMet(summons.StoryConditions, save), "both branches lead to the trial");

            Assert.IsTrue(StoryCondition.AllMet(tutor.StoryConditions, save), "Thrasybios sells lessons in the market until he calls Kleia out");
            Assert.IsFalse(StoryCondition.AllMet(boss.StoryConditions, save));
            save.Flags[CalledFlag] = StoryEffect.DefaultValue;
            Assert.IsFalse(StoryCondition.AllMet(tutor.StoryConditions, save), "then he leaves the market");
            Assert.IsTrue(StoryCondition.AllMet(boss.StoryConditions, save), "and waits on the steps");
        }

        [Test]
        public void WinningTheTrial_TakesMenexenoAwayAndOpensTheArchiveDoor()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var menexeno = map.Actors.Single(actor => actor.Character == Menexeno);
            var gone = map.Cues.Single(cue => cue.Flag == SearchingFlag);
            var archive = map.Exits.Single(exit => exit.Location == SandboxTools.ArchiveLocationId);
            var link = new MapExitLink().Gated(SandboxTools.ActOf(archive.Location), archive.StoryConditions);
            var save = new SaveData();

            Assert.IsTrue(StoryCondition.AllMet(menexeno.StoryConditions, save), "Menexeno watches from the stoa before the trial");
            Assert.IsFalse(link.IsOpenFor(save), "the archive stays shut during the first act");
            Assert.AreEqual(Act.One, SandboxTools.ActOf(archive.Location), "the archive belongs to the first act");

            DuelStoryResults.ResultOf(Boss, BattleStatus.PlayerLost).TryApply(save, out _);
            Assert.IsTrue(StoryCondition.AllMet(menexeno.StoryConditions, save), "losing the trial keeps him there");
            Assert.IsFalse(StoryCondition.AllMet(gone.StoryConditions, save));

            DuelStoryResults.ResultOf(Boss, BattleStatus.PlayerWon).TryApply(save, out _);
            Assert.IsFalse(StoryCondition.AllMet(menexeno.StoryConditions, save), "after the win he vanishes without a word");
            Assert.IsTrue(StoryCondition.AllMet(gone.StoryConditions, save), "and Kleia notices");
            save.Flags[gone.Flag] = StoryEffect.DefaultValue;
            Assert.IsTrue(link.IsOpenFor(save), "searching for him opens the archive");
            Assert.AreEqual(1, System.Math.Abs(archive.X - menexeno.X) + System.Math.Abs(archive.Y - menexeno.Y), "the archive door is right behind the place he used to stand");
        }

        [Test]
        public void EveryExitIntoTheSecondAct_StaysClosedWhileActFlagIsOne()
        {
            var secondActLocations = SandboxTools.Maps.Where(map => MapLayout.Load(map.MapPath).Act == Act.Two).Select(map => map.LocationId).ToList();
            CollectionAssert.AreEqual(new[] { SandboxTools.AcademyLocationId }, secondActLocations, "the Academy is the first place of the second act");

            var gates = 0;
            foreach (var entry in SandboxTools.Maps)
            {
                var map = MapLayout.Load(entry.MapPath);
                foreach (var exit in map.Exits.Where(exit => secondActLocations.Contains(exit.Location)))
                {
                    var link = new MapExitLink().Gated(SandboxTools.ActOf(exit.Location), exit.StoryConditions);
                    Assert.IsTrue(link.IsGated, $"the exit from {entry.LocationId} to {exit.Location} is gated");
                    Assert.IsFalse(link.IsOpenFor(new SaveData { ActFlag = Act.One }), $"ActFlag = 1 keeps {exit.Location} out of reach from {entry.LocationId}");
                    Assert.IsTrue(link.IsOpenFor(new SaveData { ActFlag = Act.Two }), $"ActFlag = 2 opens {exit.Location} from {entry.LocationId}");
                    gates++;
                }
            }

            Assert.AreEqual(1, gates, "the road from the agora is the only way into the Academy");
            Assert.AreNotEqual(LocationScenes.AcademyScene, LocationScenes.StartScene);
        }

        [Test]
        public void AcademyRoad_LeavesTheAgoraBetweenTheHousesAndIsAnnouncedInTheSecondAct()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var road = map.Exits.Single(exit => exit.Location == SandboxTools.AcademyLocationId);
            var announcement = map.Cues.Single(cue => cue.Dialogue == "agora_academy_road");

            Assert.AreEqual(map.Height - 1, road.Y, "the road leaves by the back edge");
            Assert.AreEqual(Facing.NorthWest, map.HeadingOf(road));
            Assert.IsFalse(string.IsNullOrWhiteSpace(road.Label));
            Assert.IsFalse(announcement.StoryConditions.Any(condition => !condition.IsMet(new SaveData { ActFlag = Act.Two })), "the road is announced as soon as the second act starts");
            Assert.IsFalse(StoryCondition.AllMet(announcement.StoryConditions, new SaveData()));
        }

        [Test]
        public void Archive_StagesTheJurorListAndTheBronzeSealOnOneTimeline()
        {
            var map = MapLayout.Load(SandboxTools.ArchiveMapPath);
            var scene = map.Cutscenes.Single();
            var trigger = map.Triggers.Single();
            var academic = map.Actors.Single(actor => actor.Character == "academic");
            var dialogues = scene.Steps.Where(step => step.Action == CutsceneAction.Dialogue).Select(step => step.Dialogue).ToList();

            Assert.AreEqual(scene.Id, trigger.Cutscene);
            CollectionAssert.AreEqual(new[] { "archive_juror_list", "archive_bronze_seal", "archive_act_two" }, dialogues, "the revelation comes before the seal");
            Assert.AreEqual(CutsceneAction.Leave, scene.Steps.Single(step => step.Actor == academic.Character && step.Action == CutsceneAction.Leave).Action, "the academic walks out after handing over the seal");
            CollectionAssert.AreEqual(new[] { StoryCondition.Lacks("bronze_seal").ToString() }, academic.StoryConditions.Select(condition => condition.ToString()), "he only waits in the archive until the seal is handed over");
            Assert.AreEqual(Act.One, map.Act, "the second act starts when the seal is handed over, not when Kleia enters");
            Assert.IsTrue(map.Exits.All(exit => exit.Location == SandboxTools.AgoraLocationId));
        }
    }
}
