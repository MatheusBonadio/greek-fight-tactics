using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class StreetTrialPlayTests
    {
        private const string Boss = "thrasybios_street_trial";
        private const string Tutor = "thrasybios";
        private const string Summons = "agora_street_trial_summons";
        private const string MenexenoGone = "agora_menexeno_gone";
        private const string LostFlag = "lost_street_trial";

        private static readonly Coord TrialPost = new(16, 8);
        private const float TimeoutSeconds = 10f;
        private const float BattleTimeoutSeconds = 90f;
        private const int MaxConfirms = 128;

        private static readonly string[] PressureCuesAlreadySeen = { "heard_asebeia", "saw_rumor_spread", "worried_for_home" };

        private readonly List<DuelPhaseCue> phaseCues = new();
        private readonly List<string> scriptsSeen = new();
        private IsolatedDataDirectory data;
        private SandboxBattleDirector director;
        private GridSandboxController sandbox;
        private ExplorationDialogueDirector story;
        private GameServices services;

        [UnitySetUp]
        public IEnumerator CallKleiaToTheStoaSteps()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return null;

            director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            services = Object.FindAnyObjectByType<GameServices>();
            director.ThinkSeconds = 0f;
            if (director.Crowd)
            {
                director.Crowd.ArrivalCapSeconds = 0f;
            }

            yield return WaitFor(() => sandbox.ProgressLoaded && story.IsStoryLoaded && services.IsBooted, TimeoutSeconds);
            Assert.IsTrue(sandbox.ProgressLoaded && story.IsStoryLoaded);
            Assert.IsFalse(Trial.IsPlaced, "a new game has no street trial yet");
            Assert.IsTrue(Market.IsPlaced, "Thrasybios starts in the market");

            Assert.IsTrue(story.Remember(new StoryEffect(ActionUnlocks.MaieuticsFlag)));
            Assert.IsTrue(story.Remember(new StoryEffect(Market.Opponent.Tutorial.DoneFlag)));
            Assert.IsTrue(story.Remember(new StoryEffect(DuelStoryFlags.FlagFor(Tutor), DuelStoryFlags.Won)));
            foreach (var cue in PressureCuesAlreadySeen)
            {
                Assert.IsTrue(story.Remember(new StoryEffect(cue)), $"Damaris only comes after the {cue} cue of the pressure");
            }

            Assert.IsFalse(Trial.IsPlaced, "an undecided Damaris keeps the trial away");
            Assert.IsTrue(story.Remember(new StoryEffect(SaveFlags.DamarisBranch, DamarisChoice.Refused.ToString().ToLowerInvariant())), "the Damaris scene of AT1-07 records the choice this way");

            yield return WaitFor(() => story.IsTalking, TimeoutSeconds);
            Assert.AreEqual(Summons, story.ActiveDialogueId, "once Damaris is decided, Thrasybios calls Kleia out");
            yield return FinishConversation();

            Assert.IsTrue(Trial.IsPlaced, "the trial waits on the stoa steps");
            Assert.IsFalse(Market.IsPlaced, "Thrasybios left the market for the steps");
            Assert.IsTrue(sandbox.IsArriving(Trial.Actor), "he climbs the steps in front of everyone instead of appearing there");
            Assert.IsNull(director.ChallengerAt(Trial.Actor.Walker.Cell), "nobody is challenged halfway up the steps");
            yield return WaitFor(() => !Trial.Actor.Walker.IsMoving, TimeoutSeconds);
            Assert.AreEqual(TrialPost, Trial.Actor.Walker.Cell, "he walks past the columns, where none of them hides him");
            yield return WaitFor(() => Trial.Actor.Walker.Facing == Trial.Actor.StartFacing, TimeoutSeconds);
            Assert.AreEqual(Trial.Actor.StartFacing, Trial.Actor.Walker.Facing, "he turns back to the square once he is in place");
            phaseCues.Clear();
            scriptsSeen.Clear();
            director.PhaseStarted += phaseCues.Add;
        }

        [UnityTearDown]
        public IEnumerator RestoreData()
        {
            if (director)
            {
                director.PhaseStarted -= phaseCues.Add;
            }

            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Trial_ChangesItsMusicAndPostureEveryPhaseAndOnlyItsOwnCounterWinsEach()
        {
            var phases = Trial.Opponent.Phases.Phases;
            var menexeno = story.Talkers.Single(talker => talker.Dialogue == "menexeno_agora").Actor;
            Assert.IsNotNull(menexeno.Walker, "Menexeno watches the trial from the stoa");

            yield return DuelChallenge.Engage(sandbox, director, Trial, TimeoutSeconds);
            Assert.AreEqual(Trial.Opponent.Phases.Intro, director.ActiveScriptId, "the trial opens with its own scene");
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);

            var state = director.Session.State;
            Assert.IsNotNull(director.PhasedDuel);
            Assert.IsFalse(state.Rules.AudienceVictory);
            AssertPhase(0, phases[0]);
            Assert.AreEqual(Posture.Idle, Trial.Actor.Posture);
            Assert.AreEqual(ActorView.IdleState, Trial.Actor.PlayingState);

            yield return Answer(new BattleMenuChoice(ActionId.Refute, state.CurrentArgument.Value.FallacyId));
            var deflected = state.Log.Last(entry => entry.ActorId == state.Player.Id);
            Assert.IsTrue(deflected.Deflected, "naming the ad hominem does not answer it");
            Assert.AreEqual(0, deflected.TargetCoherenceDelta);
            Assert.Greater(state.Opponent.Coherence, phases[0].UntilCoherence, "being cornered between the columns wears him down, but never past the floor of the phase");
            Assert.AreEqual(1, phaseCues.Count, "a wrong answer keeps the phase");

            yield return Answer(new BattleMenuChoice(ActionId.Concede));
            Assert.AreEqual(phases[0].UntilCoherence, LastPlayerEntry(state).OpponentCoherence, "conceding the fact takes his weapon and ends the phase at its floor");
            Assert.IsTrue(LastPlayerEntry(state).Hit);
            AssertPhase(1, phases[1]);
            Assert.AreEqual(Posture.Action, Trial.Actor.Posture);
            Assert.AreEqual(BattleAnimations.Talk, Trial.Actor.PlayingState, "he gestures with his talk strip, drawn like the rest of him, never with the old placeholder sheet");
            Assert.AreEqual("false_dilemma", state.CurrentArgument.Value.FallacyId);

            yield return Answer(new BattleMenuChoice(ActionId.Refute, state.CurrentArgument.Value.FallacyId));
            Assert.AreEqual(phases[1].UntilCoherence, LastPlayerEntry(state).OpponentCoherence, "the third way refutes the dilemma");
            AssertPhase(2, phases[2]);
            Assert.AreEqual(phases[2].Tint, Trial.Actor.Tint, "he goes pale when he invokes the Thirty");
            var fear = state.CurrentArgument.Value;
            Assert.AreEqual("appeal_to_emotion", fear.FallacyId);

            yield return Answer(new BattleMenuChoice(ActionId.Refute, fear.FallacyId));
            Assert.IsTrue(state.Log.Last(entry => entry.ActorId == state.Player.Id).Deflected, "refuting fear does not work");
            yield return Answer(new BattleMenuChoice(ActionId.Maieutics, premiseIndex: state.CurrentArgument.Value.WeakPremise));

            Assert.AreEqual(Trial.Opponent.Phases.Victory, director.ActiveScriptId, "the question makes him confess");
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.Exploring);
            Assert.AreEqual(BattleStatus.PlayerWon, director.LastResult);
            CollectionAssert.AreEqual(phases.Select(phase => phase.Music), phaseCues.Select(cue => cue.MusicId), "one music cue per phase, in order");
            CollectionAssert.AreEqual(phases.Select(phase => phase.Id), phaseCues.Select(cue => cue.PhaseId));
            Assert.IsTrue(phaseCues.All(cue => cue.OpponentId == Boss));
            Assert.IsNull(director.CurrentPhaseCue, "the phase cue ends with the duel");
            Assert.AreEqual(DuelStoryFlags.Won, story.Story.Flags[DuelStoryFlags.FlagFor(Boss)]);
            Assert.IsTrue(services.TryLoadActiveSave(out var saved));
            Assert.IsTrue(saved.Duels.TryGetValue(Boss, out var record) && record.Won && record.Victory == DuelVictory.Maieutics);

            Assert.IsFalse(Trial.IsPlaced, "Thrasybios leaves the steps");
            Assert.IsNull(menexeno.Walker, "Menexeno disappears without a word");
            yield return WaitFor(() => story.IsTalking, TimeoutSeconds);
            Assert.AreEqual(MenexenoGone, story.ActiveDialogueId, "Kleia notices he is gone");
            yield return FinishConversation();

            var archive = sandbox.Exits.Single(exit => exit.Location == LocationScenes.Archive);
            Assert.IsTrue(sandbox.IsExitOpen(archive), "looking for him opens the archive door");
            Assert.IsTrue(sandbox.Grid[archive.Cell].Revealed && sandbox.Grid[archive.Cell].IsPassable);
            Assert.AreSame(archive.Marker, sandbox.ExitsTilemap.GetTile(new Vector3Int(archive.Cell.X, archive.Cell.Y, 0)));
        }

        [UnityTest]
        public IEnumerator LosingTheTrial_IsNotGameOverAndTheRetryOpensWithOtherWords()
        {
            yield return DuelChallenge.Engage(sandbox, director, Trial, TimeoutSeconds);
            RecordScript();
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            var firstOpening = introLines.ToList();
            CollectionAssert.IsNotEmpty(firstOpening);

            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.PlayerChoosing)
                {
                    director.SubmitChoice(new BattleMenuChoice(ActionId.Refute, director.Session.State.CurrentArgument.Value.FallacyId));
                }
                else if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    RecordScript();
                    director.AdvanceDialogue();
                }

                yield return null;
            }

            Assert.AreEqual(BattleStatus.PlayerLost, director.LastResult, "answering the personal attack with REFUTE keeps backfiring");
            CollectionAssert.Contains(scriptsSeen, Trial.Opponent.Phases.Defeat, "losing plays the threat to her father instead of the plain defeat line");
            Assert.AreEqual(1, phaseCues.Count, "she never left the first phase");
            Assert.IsTrue(StoryCondition.Has(LostFlag).IsMet(story.Story));
            Assert.AreEqual(DuelStoryFlags.Lost, story.Story.Flags[DuelStoryFlags.FlagFor(Boss)]);
            yield return Frames(10);
            Assert.IsTrue(Trial.IsPlaced, "defeat is not game over: he waits on the steps");
            Assert.IsFalse(director.IsDefeated(Trial));
            Assert.AreEqual(Posture.Idle, Trial.Actor.Posture, "the posture resets after the duel");

            introLines.Clear();
            yield return DuelChallenge.Engage(sandbox, director, Trial, TimeoutSeconds);
            Assert.AreEqual(Trial.Opponent.Phases.Intro, director.ActiveScriptId);
            RecordScript();
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            CollectionAssert.IsNotEmpty(introLines);
            CollectionAssert.IsEmpty(introLines.Intersect(firstOpening), "the retry opens with different dialogue");
            Assert.AreEqual(0, director.PhaseIndex, "the retry starts over from the first phase");
            Assert.Greater(director.Session.State.Opponent.Coherence, Trial.Opponent.Phases.Phases[0].UntilCoherence);

            director.SubmitChoice(new BattleMenuChoice(ActionId.Withdraw));
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.Exploring);
            Assert.AreEqual(BattleStatus.PlayerWithdrew, director.LastResult);
            Assert.IsTrue(Trial.IsPlaced);
        }

        private readonly List<string> introLines = new();

        private MapOpponent Trial => director.Opponents.Single(entry => entry.Opponent.Id == Boss);

        private static BattleLogEntry LastPlayerEntry(BattleState state) => state.Log.Last(entry => entry.ActorId == state.Player.Id && entry.Rejection == RejectionReason.None);

        private MapOpponent Market => director.Opponents.Single(entry => entry.Opponent.Id == Tutor);

        private void AssertPhase(int index, Game.Content.OpponentPhase phase)
        {
            Assert.AreEqual(index + 1, phaseCues.Count, $"phase {phase.Id} raised its cue");
            var cue = phaseCues[index];
            Assert.AreEqual(phase.Id, cue.PhaseId);
            Assert.AreEqual(phase.Music, cue.MusicId);
            Assert.AreEqual(phase.Posture, cue.Posture);
            Assert.AreEqual(index, director.PhaseIndex);
            Assert.AreEqual(cue.PhaseId, director.CurrentPhaseCue?.PhaseId);
            CollectionAssert.Contains(scriptsSeen, phase.Dialogue, $"phase {phase.Id} opens with its scene");
        }

        private IEnumerator Answer(BattleMenuChoice choice)
        {
            Assert.AreEqual(BattleFlowPhase.PlayerChoosing, director.Phase);
            director.SubmitChoice(choice);
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing || director.Phase == BattleFlowPhase.Exploring || (director.Session != null && director.Session.IsOver && director.IsPlayingScript));
        }

        private IEnumerator AdvanceUntil(Func<bool> done)
        {
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (!done() && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    RecordScript();
                    director.AdvanceDialogue();
                }

                yield return null;
            }

            Assert.IsTrue(done(), $"the trial got stuck in {director.Phase}");
            RecordScript();
        }

        private void RecordScript()
        {
            var active = director.ActiveScriptId;
            if (active != null && (scriptsSeen.Count == 0 || scriptsSeen[^1] != active))
            {
                scriptsSeen.Add(active);
            }

            var opening = director.Engaged?.Opponent.Phases.Intro;
            if (active != null && active == opening && director.Hud.Dialogue.IsOpen && (introLines.Count == 0 || introLines[^1] != director.Hud.Dialogue.Line.Text))
            {
                introLines.Add(director.Hud.Dialogue.Line.Text);
            }
        }

        private IEnumerator FinishConversation()
        {
            for (var confirms = 0; story.IsTalking && confirms < MaxConfirms; confirms++)
            {
                story.Advance();
                yield return null;
            }

            Assert.IsFalse(story.IsTalking, "the conversation never closed");
            yield return null;
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
