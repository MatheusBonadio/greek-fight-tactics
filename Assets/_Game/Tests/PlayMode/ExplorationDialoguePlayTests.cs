using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class ExplorationDialoguePlayTests
    {
        private const float TimeoutSeconds = 10f;
        private const int MaxConfirms = 64;
        private const int BlockedFrames = 30;
        private const string MenexenoDialogue = "menexeno_agora";
        private const string MetMenexeno = "met_menexeno";
        private const string WorkshopDialogue = "kerameikos_leaving_workshop";
        private const string LeftWorkshop = "left_workshop";
        private const string ThrasybiosId = "thrasybios";

        private static readonly Coord MenexenoApproachStart = new(11, 8);
        private static readonly Coord BesideThrasybios = new(6, 1);
        private static readonly Coord WorkshopDoor = new(4, 7);
        private static readonly Coord Yard = new(4, 5);
        private static readonly Coord Workbench = new(4, 11);
        private static readonly Coord KilnYard = new(12, 9);

        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            yield return null;
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
        public IEnumerator ConfirmOnMenexeno_WalksBesideHimPlaysTheConversationAndRemembersIt()
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            var director = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            var battle = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var services = Object.FindAnyObjectByType<GameServices>();
            Assert.IsNotNull(director, "the agora must contain the exploration dialogue director");
            battle.AutosaveEnabled = false;
            yield return WaitFor(() => director.IsStoryLoaded && services.IsBooted);
            Assert.IsTrue(director.IsStoryLoaded);

            var menexeno = director.Talkers.Single(link => link.Dialogue == MenexenoDialogue).Actor;
            var player = sandbox.Player.Walker;
            player.Teleport(MenexenoApproachStart, Facing.NorthWest);
            sandbox.Cursor.TryHover(IsoMath.GridToWorld(menexeno.Walker.Cell), Vector2.zero);
            sandbox.ConfirmCursor();
            Assert.IsTrue(director.IsApproaching, "confirming on a distant npc walks up to him");
            Assert.IsTrue(player.IsMoving);

            yield return WaitFor(() => director.IsTalking);
            Assert.IsTrue(director.IsTalking, "the conversation never started");
            Assert.AreEqual(1, Distance(player.Cell, menexeno.Walker.Cell), "Kleia talks from the next cell");
            Assert.AreEqual(FacingRules.FromStep(player.Cell, menexeno.Walker.Cell, player.Facing), player.Facing, "Kleia faces Menexeno");
            Assert.AreEqual(FacingRules.FromStep(menexeno.Walker.Cell, player.Cell, menexeno.Walker.Facing), menexeno.Walker.Facing, "Menexeno turns to Kleia");
            Assert.AreEqual(GameMode.Dialogue, sandbox.StateMachine.Mode);
            Assert.IsFalse(sandbox.StateMachine.IsExplorationInputEnabled);
            Assert.IsTrue(director.DialogueBox.IsOpen);
            Assert.AreEqual(MenexenoDialogue, director.ActiveDialogueId);

            var talkingFrom = player.Cell;
            sandbox.Cursor.TryHover(IsoMath.GridToWorld(MenexenoApproachStart), Vector2.zero);
            sandbox.ConfirmCursor();
            yield return null;
            Assert.AreEqual(talkingFrom, player.Cell, "confirming a cell while the box is open does not walk");
            Assert.IsFalse(player.IsMoving);

            var first = new List<DialogueLine>();
            yield return PlayToTheEnd(director, first);
            Assert.GreaterOrEqual(first.Count, 3);
            Assert.AreEqual(string.Empty, first[0].SpeakerName, "the conversation opens with narration");
            Assert.IsNull(first[0].Portrait);
            var menexenoLine = first.First(line => line.SpeakerName == "Menexeno");
            Assert.AreSame(director.PortraitFor("menexeno"), menexenoLine.Portrait);
            Assert.IsNotNull(menexenoLine.Portrait);
            Assert.AreEqual(GameMode.Exploration, sandbox.StateMachine.Mode);
            Assert.IsFalse(director.DialogueBox.IsOpen);
            Assert.AreEqual(StoryEffect.DefaultValue, director.Story.Flags[MetMenexeno]);
            Assert.IsTrue(services.TryLoadActiveSave(out var saved), "closing the dialogue autosaves");
            Assert.AreEqual(StoryEffect.DefaultValue, saved.Flags[MetMenexeno]);

            yield return null;
            sandbox.Cursor.TryHover(IsoMath.GridToWorld(menexeno.Walker.Cell), Vector2.zero);
            sandbox.ConfirmCursor();
            yield return WaitFor(() => director.IsTalking);
            var again = new List<DialogueLine>();
            yield return PlayToTheEnd(director, again);
            Assert.IsNotEmpty(again);
            Assert.Less(again.Count, first.Count, "the second talk skips the introduction");
            CollectionAssert.IsEmpty(again.Select(line => line.Text).Intersect(first.Select(line => line.Text)));
        }

        [UnityTest]
        public IEnumerator OpenDialogue_KeepsThrasybiosFromTakingUpAChallengeUntilItCloses()
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            var director = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            var battle = Object.FindAnyObjectByType<SandboxBattleDirector>();
            battle.AutosaveEnabled = false;
            director.AutosaveEnabled = false;
            yield return WaitFor(() => director.IsStoryLoaded && sandbox.ProgressLoaded);
            var thrasybios = battle.Opponents.Single(entry => entry.Opponent.Id == ThrasybiosId).Actor.Walker.Cell;

            Assert.IsTrue(director.TryStartDialogue(MenexenoDialogue));
            sandbox.Player.Walker.Teleport(BesideThrasybios, Facing.NorthEast);
            DuelChallenge.Confirm(sandbox, thrasybios);
            for (var frame = 0; frame < BlockedFrames; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, battle.Phase, "a challenge is ignored while the dialogue box is open");
            Assert.AreEqual(GameMode.Dialogue, sandbox.StateMachine.Mode);

            yield return PlayToTheEnd(director, new List<DialogueLine>());
            for (var frame = 0; frame < BlockedFrames; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, battle.Phase, "standing beside Thrasybios never starts the duel on its own");
            DuelChallenge.Confirm(sandbox, thrasybios);
            yield return WaitFor(() => battle.Phase != BattleFlowPhase.Exploring);
            Assert.AreNotEqual(BattleFlowPhase.Exploring, battle.Phase, "Thrasybios takes up the challenge once the dialogue closes");
            Assert.IsTrue(sandbox.StateMachine.IsInBattle);
        }

        [UnityTest]
        public IEnumerator LeavingTheWorkshop_PlaysKleiasThoughtOnlyOnce()
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.KerameikosScene, LoadSceneMode.Single);
            Object.FindAnyObjectByType<StoryCueDirector>().enabled = false;
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            var director = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            var services = Object.FindAnyObjectByType<GameServices>();
            Assert.IsNotNull(director, "the workshop must contain the exploration dialogue director");
            yield return WaitFor(() => director.IsStoryLoaded && services.IsBooted);
            var player = sandbox.Player.Walker;
            Assert.AreEqual(Workbench, player.Cell);

            Assert.IsTrue(sandbox.TryMovePlayerTo(Yard));
            yield return WaitFor(() => director.IsTalking);
            Assert.IsTrue(director.IsTalking, "stepping on the workshop door did not start the scene");
            Assert.AreEqual(WorkshopDialogue, director.ActiveDialogueId);
            Assert.AreEqual(WorkshopDoor, player.Cell, "Kleia stops on the trigger cell");
            Assert.IsFalse(player.IsMoving);

            yield return PlayToTheEnd(director, new List<DialogueLine>());
            Assert.AreEqual(StoryEffect.DefaultValue, director.Story.Flags[LeftWorkshop]);
            Assert.IsTrue(services.TryLoadActiveSave(out var saved));
            Assert.AreEqual(StoryEffect.DefaultValue, saved.Flags[LeftWorkshop]);

            foreach (var destination in new[] { Yard, Workbench, KilnYard })
            {
                Assert.IsTrue(sandbox.TryMovePlayerTo(destination), $"no path to {destination}");
                yield return WaitFor(() => !player.IsMoving || director.IsTalking);
                Assert.IsFalse(director.IsTalking, $"the scene played again on the way to {destination}");
                Assert.AreEqual(destination, player.Cell);
            }
        }

        private static IEnumerator PlayToTheEnd(ExplorationDialogueDirector director, List<DialogueLine> shown)
        {
            ScriptLine last = null;
            for (var confirms = 0; director.IsTalking && confirms < MaxConfirms; confirms++)
            {
                if (!ReferenceEquals(director.CurrentLine, last))
                {
                    last = director.CurrentLine;
                    shown.Add(director.DialogueBox.Line);
                }

                director.Advance();
                yield return null;
            }

            Assert.IsFalse(director.IsTalking, "the dialogue never finished");
        }

        private static int Distance(Coord a, Coord b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
