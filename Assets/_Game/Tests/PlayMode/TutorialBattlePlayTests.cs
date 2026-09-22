using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class TutorialBattlePlayTests
    {
        private const string Thrasybios = "thrasybios";
        private const string TaughtFallacy = "equivocation";
        private const string LessonsFlag = "thrasybios_lessons";
        private const string DemoFlag = "saw_thrasybios_demo";
        private const string CrowdName = "Plateia";
        private const float TimeoutSeconds = 10f;
        private const float BattleTimeoutSeconds = 60f;
        private const int ApproachDistance = 2;

        private readonly List<string> scriptsSeen = new();
        private readonly HashSet<string> speakersSeen = new();
        private IsolatedDataDirectory data;
        private SandboxBattleDirector director;
        private GridSandboxController sandbox;
        private ExplorationDialogueDirector story;
        private GameServices services;
        private MapOpponent thrasybios;

        [UnitySetUp]
        public IEnumerator LoadTheAgoraInANewGame()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return null;

            director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            services = Object.FindAnyObjectByType<GameServices>();
            Assert.IsNotNull(director, "the agora must contain the battle director");
            Assert.IsNotNull(story, "the agora must contain the exploration dialogue director");
            director.ThinkSeconds = 0f;
            yield return WaitFor(() => sandbox.ProgressLoaded && story.IsStoryLoaded && services.IsBooted, TimeoutSeconds);
            Assert.IsTrue(story.IsStoryLoaded);
            Assert.AreSame(story, director.Story, "the battle director plays its scenes through the map's story");

            thrasybios = director.Opponents.Single(entry => entry.Opponent.Id == Thrasybios);
            scriptsSeen.Clear();
            speakersSeen.Clear();
        }

        [UnityTearDown]
        public IEnumerator RestoreData()
        {
            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstDuel_TeachesRefuteStepByStep_AndAcceptingTheLessonsIsSaved() => PlayTheTutorial(0, false, "accepted");

        [UnityTest]
        public IEnumerator FirstDuel_TeachesRefuteStepByStep_AndRefusingTheLessonsWithTheMouseIsSaved() => PlayTheTutorial(1, true, "refused");

        [UnityTest]
        public IEnumerator LaterDuelWithThrasybios_IsANormalFight()
        {
            var tutorial = thrasybios.Opponent.Tutorial;
            Assert.IsTrue(story.Remember(new StoryEffect(tutorial.DoneFlag)));

            yield return Challenge();
            Assert.IsNull(director.Tutorial, "the lesson is over, so the duel is normal");
            Assert.IsFalse(director.IsPlayingScript);
            StringAssert.Contains(thrasybios.Opponent.DisplayName, director.Hud.Dialogue.Line.Text, "a normal duel opens with the standard challenge");
            Assert.AreEqual(new BattleRules().CounterattackDamage, director.Session.State.Rules.CounterattackDamage);

            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            CollectionAssert.IsEmpty(scriptsSeen, "no tutorial cue plays in a normal duel");
            director.SubmitChoice(new BattleMenuChoice(ActionId.Withdraw));
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.Exploring);
        }

        [UnityTest]
        public IEnumerator LeavingTheTutorial_KeepsItPendingWithoutReplayingTheDemonstration()
        {
            yield return Challenge();
            Assert.AreEqual(thrasybios.Opponent.Tutorial.DialogueFor(TutorialCue.Intro), director.ActiveScriptId);
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            director.SubmitChoice(new BattleMenuChoice(ActionId.Withdraw));
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.Exploring);

            Assert.AreEqual(BattleStatus.PlayerWithdrew, director.LastResult);
            Assert.IsTrue(thrasybios.Opponent.Tutorial.IsPending(story.Story), "leaving does not finish the lesson");
            Assert.IsTrue(StoryCondition.Has(DemoFlag).IsMet(story.Story));
            Assert.IsFalse(director.IsDefeated(thrasybios));

            yield return Frames(10);
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "standing beside Thrasybios after leaving does not start the lesson again");
            scriptsSeen.Clear();
            yield return Challenge();

            Assert.IsNotNull(director.Tutorial, "the lesson starts over");
            Assert.IsFalse(director.IsPlayingScript, "Thrasybios does not repeat the amphora demonstration");
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            CollectionAssert.AreEqual(new[] { thrasybios.Opponent.Tutorial.DialogueFor(TutorialCue.Argument) }, scriptsSeen);
            director.SubmitChoice(new BattleMenuChoice(ActionId.Withdraw));
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.Exploring);
        }

        private IEnumerator PlayTheTutorial(int option, bool withMouse, string expectedAnswer)
        {
            var tutorial = thrasybios.Opponent.Tutorial;
            Assert.IsTrue(tutorial.IsPending(story.Story), "a new game has not had the lesson yet");

            yield return Challenge();
            Assert.IsNotNull(director.Tutorial, "the first duel with Thrasybios is the tutorial");
            Assert.AreEqual(tutorial.DialogueFor(TutorialCue.Intro), director.ActiveScriptId, "the duel opens with the amphora demonstration");
            Assert.AreEqual(0, director.Session.State.Rules.CounterattackDamage);

            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            CollectionAssert.AreEqual(new[] { tutorial.DialogueFor(TutorialCue.Intro), tutorial.DialogueFor(TutorialCue.Argument) }, scriptsSeen);
            var argument = director.Session.State.CurrentArgument.Value;
            Assert.AreEqual(TaughtFallacy, argument.FallacyId, "the lesson always uses the same fallacy");
            StringAssert.Contains("ânfora", argument.Speech, "the lesson opens with the amphora argument");
            var coherence = director.Session.State.Player.Coherence;

            yield return OpenRefute();
            Assert.AreEqual(tutorial.DialogueFor(TutorialCue.Fallacies), director.ActiveScriptId, "opening the fallacy list explains it");
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            Assert.AreEqual(BattleMenuLayer.Fallacies, director.Controller.Layer, "the hint returns to the fallacy list");

            PickFallacy(director.Controller.SubmenuOptions.First(id => id != argument.FallacyId));
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            CollectionAssert.Contains(scriptsSeen, tutorial.DialogueFor(TutorialCue.Miss));
            Assert.AreEqual(coherence, director.Session.State.Player.Coherence, "a wrong answer costs nothing while learning");
            Assert.AreEqual(argument.Speech, director.Session.State.CurrentArgument.Value.Speech, "Thrasybios repeats the amphora argument until Kleia refutes it");

            yield return OpenRefute();
            Assert.AreEqual(tutorial.DialogueFor(TutorialCue.Fallacies), director.ActiveScriptId, "the fallacy hint returns until Kleia gets it right");
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            PickFallacy(argument.FallacyId);
            yield return AdvanceUntil(() => director.Hud.Dialogue.IsAwaitingChoice);

            Assert.AreEqual(BattleStatus.PlayerWon, director.Session.State.Status, "GDD I.2: one refutation wins the tutorial");
            Assert.AreEqual(1, director.Session.State.Log.Count(entry => entry.Action == ActionId.Refute && entry.Hit));
            CollectionAssert.Contains(services.Compendium.UnlockedIds, TaughtFallacy);
            Assert.AreEqual(tutorial.DialogueFor(TutorialCue.Victory), director.ActiveScriptId);
            CollectionAssert.AreEqual(
                new[] { TutorialCue.Intro, TutorialCue.Argument, TutorialCue.Fallacies, TutorialCue.Miss, TutorialCue.Fallacies, TutorialCue.Victory }.Select(tutorial.DialogueFor),
                scriptsSeen,
                "the argument is explained once, and the victory scene follows the hit");
            CollectionAssert.Contains(speakersSeen, CrowdName, "the crowd laughs at Kleia");
            Assert.IsTrue(director.Hud.Dialogue.IsAwaitingChoice, "Thrasybios waits for an answer to his offer");
            Assert.AreEqual(2, director.Hud.Dialogue.Options.Count);
            Assert.IsFalse(story.Story.Flags.ContainsKey(LessonsFlag));

            if (withMouse)
            {
                director.Hud.Dialogue.ChoiceRows[option].GetComponent<PanelRowPointer>().OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            }
            else
            {
                for (var step = 0; step < option; step++)
                {
                    director.Hud.Dialogue.HandleNavigate(Vector2.down);
                }

                director.AdvanceDialogue();
            }

            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.Exploring);
            Assert.AreEqual(BattleStatus.PlayerWon, director.LastResult);
            Assert.AreEqual(expectedAnswer, story.Story.Flags[LessonsFlag]);
            Assert.IsFalse(tutorial.IsPending(story.Story));
            Assert.IsTrue(director.IsDefeated(thrasybios));

            Assert.IsTrue(services.TryLoadActiveSave(out var saved), "the end of the tutorial autosaves");
            Assert.AreEqual(expectedAnswer, saved.Flags[LessonsFlag], "the offer is recorded for the epilogue");
            Assert.IsTrue(StoryCondition.Has(tutorial.DoneFlag).IsMet(saved));
            Assert.IsTrue(saved.Duels.TryGetValue(Thrasybios, out var record) && record.Won);
        }

        private Coord ThrasybiosCell => thrasybios.Actor.Walker.Cell;

        private IEnumerator Challenge()
        {
            if (!PathFinder.AreBeside(sandbox.Player.Walker.Cell, ThrasybiosCell))
            {
                sandbox.Player.Walker.Teleport(FreeCell(cell => PositionalModifiers.Distance(cell, ThrasybiosCell) == ApproachDistance), Facing.NorthEast);
            }

            yield return null;
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "Thrasybios never starts the duel on his own");
            DuelChallenge.Confirm(sandbox, ThrasybiosCell);
            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, TimeoutSeconds);
            Assert.AreSame(thrasybios, director.Engaged, "confirming on Thrasybios did not start the lesson");
            Assert.IsTrue(PathFinder.AreBeside(sandbox.Player.Walker.Cell, ThrasybiosCell), "Kleia walks up to Thrasybios before the duel");
            RecordDialogue();
        }

        private IEnumerator OpenRefute()
        {
            var refute = director.Session.Menu.Entries.Select((entry, index) => (entry, index)).Single(pair => pair.entry.Action == ActionId.Refute);
            Assert.IsTrue(refute.entry.IsAvailable);
            director.Hud.Menu.HandlePoint(BattleMenuLayer.Actions, refute.index);
            director.Hud.Menu.HandleConfirm();
            Assert.AreEqual(BattleMenuLayer.Fallacies, director.Controller.Layer);
            yield return null;
            RecordDialogue();
        }

        private void PickFallacy(string fallacyId)
        {
            Assert.AreEqual(BattleFlowPhase.PlayerChoosing, director.Phase);
            var index = director.Controller.SubmenuOptions.ToList().IndexOf(fallacyId);
            Assert.GreaterOrEqual(index, 0, $"{fallacyId} is not in the fallacy list");
            director.Controller.Point(index);
            director.Hud.Menu.HandleConfirm();
        }

        private IEnumerator AdvanceUntil(Func<bool> done)
        {
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (!done() && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    RecordDialogue();
                    director.AdvanceDialogue();
                }

                yield return null;
            }

            Assert.IsTrue(done(), "the battle got stuck");
            RecordDialogue();
        }

        private void RecordDialogue()
        {
            var active = director.ActiveScriptId;
            if (active != null && (scriptsSeen.Count == 0 || scriptsSeen[^1] != active))
            {
                scriptsSeen.Add(active);
            }

            if (director.Hud.Dialogue.IsOpen && !string.IsNullOrEmpty(director.Hud.Dialogue.Line.SpeakerName))
            {
                speakersSeen.Add(director.Hud.Dialogue.Line.SpeakerName);
            }
        }

        private Coord FreeCell(Func<Coord, bool> accepts)
        {
            var grid = sandbox.Grid;
            var candidates = new List<Coord>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new Coord(x, y);
                    if (grid.TryGet(cell, out var tile) && tile.IsPassable && !tile.IsOccupied && accepts(cell))
                    {
                        candidates.Add(cell);
                    }
                }
            }

            Assert.IsNotEmpty(candidates, "no free cell fits the test");
            return candidates.OrderBy(cell => PositionalModifiers.Distance(cell, ThrasybiosCell)).First();
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
