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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class WorkshopStoryPlayTests
    {
        private const float TimeoutSeconds = 10f;
        private const float BattleTimeoutSeconds = 90f;
        private const int MaxConfirms = 64;
        private const int QuietFrames = 30;
        private const string FirstStepsDialogue = "kerameikos_first_steps";
        private const string FogDialogue = "kerameikos_tutorial_fog";
        private const string TalkDialogue = "kerameikos_tutorial_talk";
        private const string AristeuDialogue = "aristeu_workshop";
        private const string MenexenoDialogue = "menexeno_workshop_door";
        private const string ThrasybiosId = "thrasybios";
        private const string AristeuName = "Aristeu";
        private const string MenexenoName = "Menexeno";

        private static readonly string[] TutorialFlags = { "tutorial_first_steps", "tutorial_fog", "tutorial_talk" };
        private static readonly string[] MenexenoFlags = { ActionUnlocks.MaieuticsFlag, "menexeno_condition", "menexeno_avoids_past" };
        private static readonly Coord Workbench = new(4, 11);
        private static readonly Coord Shelves = new(2, 9);
        private static readonly Coord FarCorner = new(6, 9);
        private static readonly Coord Street = new(4, 5);
        private static readonly Coord MenexenoDoor = new(4, 6);
        private static readonly Coord MenexenoPost = new(3, 6);

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
        public IEnumerator Tutorial_TeachesMovingTheFogAndTalkingOnceEach()
        {
            var map = default(Workshop);
            yield return LoadWorkshop(loaded => map = loaded);
            var player = map.Sandbox.Player.Walker;

            yield return WaitFor(() => map.Dialogues.IsTalking);
            Assert.AreEqual(FirstStepsDialogue, map.Dialogues.ActiveDialogueId, "the workshop opens with the first steps");
            Assert.AreEqual(Workbench, player.Cell);
            yield return PlayToTheEnd(map.Dialogues, new List<DialogueLine>());
            yield return Frames(QuietFrames);
            Assert.IsFalse(map.Dialogues.IsTalking, "no other step plays before Kleia walks");

            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(Shelves));
            yield return WaitFor(() => map.Dialogues.IsTalking);
            Assert.AreEqual(FogDialogue, map.Dialogues.ActiveDialogueId, "the first step into the fog explains it");
            Assert.AreNotEqual(Shelves, player.Cell, "the fog lesson stops Kleia on the step that revealed the room");
            yield return PlayToTheEnd(map.Dialogues, new List<DialogueLine>());

            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(Shelves));
            yield return WaitFor(() => map.Dialogues.IsTalking);
            Assert.AreEqual(TalkDialogue, map.Dialogues.ActiveDialogueId, "finishing a walk points Kleia to her father");
            Assert.AreEqual(Shelves, player.Cell, "the talking hint waits for the walk to end");
            yield return PlayToTheEnd(map.Dialogues, new List<DialogueLine>());

            foreach (var destination in new[] { Workbench, FarCorner })
            {
                Assert.IsTrue(map.Sandbox.TryMovePlayerTo(destination), $"no path to {destination}");
                yield return WaitFor(() => !player.IsMoving || map.Dialogues.IsTalking);
                yield return Frames(QuietFrames);
                Assert.IsFalse(map.Dialogues.IsTalking, $"a tutorial step played again on the way to {destination}");
            }

            Assert.IsTrue(map.Services.TryLoadActiveSave(out var saved));
            foreach (var flag in TutorialFlags)
            {
                Assert.IsTrue(StoryCondition.Has(flag).IsMet(saved), $"the save forgot {flag}");
            }

            yield return LoadWorkshop(loaded => map = loaded);
            yield return Frames(QuietFrames);
            Assert.IsFalse(map.Dialogues.IsTalking, "reloading the workshop replayed the tutorial");
        }

        [UnityTest]
        public IEnumerator Aristeu_AsksKleiaToCarryTheAmphoraeBecauseOfHisHands()
        {
            var map = default(Workshop);
            yield return LoadWorkshop(loaded => map = loaded, false);
            var player = map.Sandbox.Player.Walker;
            var aristeu = map.Dialogues.Talkers.Single(link => link.Dialogue == AristeuDialogue).Actor;
            Assert.IsNotNull(aristeu.Walker, "Aristeu is always home");

            map.Sandbox.Cursor.TryHover(IsoMath.GridToWorld(aristeu.Walker.Cell), Vector2.zero);
            map.Sandbox.ConfirmCursor();
            yield return WaitFor(() => map.Dialogues.IsTalking);
            Assert.AreEqual(AristeuDialogue, map.Dialogues.ActiveDialogueId);
            Assert.AreEqual(1, Distance(player.Cell, aristeu.Walker.Cell), "Kleia talks from the next cell");
            Assert.AreEqual(FacingRules.FromStep(aristeu.Walker.Cell, player.Cell, aristeu.Walker.Facing), aristeu.Walker.Facing, "Aristeu turns to Kleia");

            var first = new List<DialogueLine>();
            yield return PlayToTheEnd(map.Dialogues, first);
            var spoken = first.First(line => line.SpeakerName == AristeuName);
            Assert.IsNotNull(spoken.Portrait, "Aristeu speaks with his portrait");
            Assert.AreSame(map.Dialogues.PortraitFor("aristeu"), spoken.Portrait);
            Assert.IsTrue(first.Any(line => string.IsNullOrEmpty(line.SpeakerName) && line.Text.Contains("dedos")), "the scene shows his crooked fingers");
            Assert.IsTrue(first.Any(line => line.Text.Contains("ágora")), "he sends Kleia to the agora");
            Assert.IsTrue(map.Services.TryLoadActiveSave(out var saved));
            Assert.IsTrue(StoryCondition.Has("aristeu_errand").IsMet(saved), "the errand is remembered");

            yield return null;
            map.Sandbox.Cursor.TryHover(IsoMath.GridToWorld(aristeu.Walker.Cell), Vector2.zero);
            map.Sandbox.ConfirmCursor();
            yield return WaitFor(() => map.Dialogues.IsTalking);
            var again = new List<DialogueLine>();
            yield return PlayToTheEnd(map.Dialogues, again);
            Assert.IsNotEmpty(again);
            Assert.Less(again.Count, first.Count, "the second talk only reminds her of the errand");
        }

        [UnityTest]
        public IEnumerator Menexeno_WaitsAtTheDoorOnlyAfterKleiaBeatsThrasybiosAndTeachesMaieutics()
        {
            var map = default(Workshop);
            yield return LoadWorkshop(loaded => map = loaded, false);
            var menexeno = map.Dialogues.Talkers.Single(link => link.Dialogue == MenexenoDialogue).Actor;
            Assert.IsNull(menexeno.Walker, "Menexeno has not followed Kleia yet");
            Assert.IsFalse(menexeno.gameObject.activeSelf);
            map.Sandbox.Player.Walker.Teleport(Street, Facing.SouthWest);
            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(MenexenoDoor));
            yield return WaitFor(() => !map.Sandbox.Player.Walker.IsMoving || map.Dialogues.IsTalking);
            yield return Frames(QuietFrames);
            Assert.IsFalse(map.Dialogues.IsTalking, "the door scene played before the Thrasybios duel");

            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            var agora = Object.FindAnyObjectByType<GridSandboxController>();
            var battle = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var services = Object.FindAnyObjectByType<GameServices>();
            battle.ThinkSeconds = 0f;
            yield return WaitFor(() => services.IsBooted && agora.ProgressLoaded);
            var thrasybios = battle.Opponents.Single(entry => entry.Opponent.Id == ThrasybiosId);
            agora.Player.Walker.Teleport(CellNoticedOnlyBy(agora.Grid, battle, thrasybios), Facing.NorthEast);
            yield return WaitFor(() => battle.Phase != BattleFlowPhase.Exploring);
            Assert.AreSame(thrasybios, battle.Engaged);

            var sawLockedMaieutics = false;
            yield return PlayUntilExploring(battle, session =>
            {
                var maieutics = session.Menu.Entries.Single(entry => entry.Action == ActionId.Maieutics);
                sawLockedMaieutics |= !maieutics.IsAvailable && maieutics.Reason == RejectionReason.ActionLocked;
                return RefuteOrClose(session);
            });
            Assert.IsTrue(sawLockedMaieutics, "maieutics was on the menu before Menexeno taught it");
            Assert.AreEqual(BattleStatus.PlayerWon, battle.LastResult, "naming every fallacy beats Thrasybios");
            Assert.IsTrue(services.TryLoadActiveSave(out var afterDuel));
            Assert.AreEqual(DuelStoryFlags.Won, afterDuel.Flags[DuelStoryFlags.FlagFor(ThrasybiosId)], "the duel result is a story flag");
            CollectionAssert.Contains(ActionUnlocks.LockedIn(afterDuel, ActionMenuModel.PlayerActions), ActionId.Maieutics);

            var exit = agora.Exits.Single(link => link.Location == LocationScenes.Kerameikos);
            agora.Player.Walker.Teleport(FreeNeighbour(agora, exit.Cell), Facing.SouthWest);
            yield return null;
            Assert.IsTrue(agora.TryMovePlayerTo(exit.Cell));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == LocationScenes.KerameikosScene);
            Assert.AreEqual(LocationScenes.KerameikosScene, SceneManager.GetActiveScene().name, "the road did not lead home");
            yield return null;
            map = Find();
            yield return WaitFor(() => map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Dialogues.IsStoryLoaded);

            menexeno = map.Dialogues.Talkers.Single(link => link.Dialogue == MenexenoDialogue).Actor;
            Assert.IsNotNull(menexeno.Walker, "Menexeno followed Kleia home after the duel");
            Assert.IsTrue(menexeno.gameObject.activeSelf);
            Assert.AreEqual(MenexenoPost, menexeno.Walker.Cell);
            yield return Frames(QuietFrames);
            Assert.IsFalse(map.Dialogues.IsTalking, "arriving from the agora does not replay the tutorial");

            var player = map.Sandbox.Player.Walker;
            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(MenexenoDoor));
            yield return WaitFor(() => map.Dialogues.IsTalking);
            Assert.AreEqual(MenexenoDialogue, map.Dialogues.ActiveDialogueId, "the door scene starts on the doorstep");
            Assert.AreEqual(MenexenoDoor, player.Cell);
            Assert.AreEqual(FacingRules.FromStep(player.Cell, menexeno.Walker.Cell, player.Facing), player.Facing, "Kleia faces Menexeno");
            Assert.AreEqual(FacingRules.FromStep(menexeno.Walker.Cell, player.Cell, menexeno.Walker.Facing), menexeno.Walker.Facing, "Menexeno faces Kleia");

            var scene = new List<DialogueLine>();
            yield return PlayToTheEnd(map.Dialogues, scene);
            Assert.IsTrue(scene.Any(line => line.SpeakerName == MenexenoName && line.Text.Contains("dinheiro")), "he states his condition");
            Assert.IsTrue(map.Services.TryLoadActiveSave(out var taught));
            foreach (var flag in MenexenoFlags)
            {
                Assert.IsTrue(StoryCondition.Has(flag).IsMet(taught), $"the door scene did not save {flag}");
            }

            CollectionAssert.IsEmpty(ActionUnlocks.LockedIn(taught, ActionMenuModel.PlayerActions), "maieutics is unlocked for the next battle");

            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(Street));
            yield return WaitFor(() => !player.IsMoving || map.Dialogues.IsTalking);
            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(MenexenoDoor));
            yield return WaitFor(() => !player.IsMoving || map.Dialogues.IsTalking);
            yield return Frames(QuietFrames);
            Assert.IsFalse(map.Dialogues.IsTalking, "the door scene plays only once");
        }

        private readonly struct Workshop
        {
            public readonly GridSandboxController Sandbox;
            public readonly ExplorationDialogueDirector Dialogues;
            public readonly StoryCueDirector Cues;
            public readonly GameServices Services;

            public Workshop(GridSandboxController sandbox, ExplorationDialogueDirector dialogues, StoryCueDirector cues, GameServices services)
            {
                Sandbox = sandbox;
                Dialogues = dialogues;
                Cues = cues;
                Services = services;
            }
        }

        private static IEnumerator LoadWorkshop(Action<Workshop> loaded, bool withTutorial = true)
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.KerameikosScene, LoadSceneMode.Single);
            var map = Find();
            Assert.IsNotNull(map.Cues, "the workshop must contain the tutorial cue director");
            map.Cues.enabled = withTutorial;
            yield return WaitFor(() => map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Dialogues.IsStoryLoaded);
            Assert.IsTrue(map.Dialogues.IsStoryLoaded, "the workshop never loaded the story");
            loaded(map);
        }

        private static Workshop Find() => new(
            Object.FindAnyObjectByType<GridSandboxController>(),
            Object.FindAnyObjectByType<ExplorationDialogueDirector>(),
            Object.FindAnyObjectByType<StoryCueDirector>(),
            Object.FindAnyObjectByType<GameServices>());

        private static BattleMenuChoice RefuteOrClose(BattleSession session)
        {
            bool Available(ActionId action) => session.Menu.Entries.Any(entry => entry.Action == action && entry.IsAvailable);

            var argument = session.State.CurrentArgument;
            if (argument.HasValue && argument.Value.IsRefutable && Available(ActionId.Refute))
            {
                return new BattleMenuChoice(ActionId.Refute, argument.Value.FallacyId);
            }

            return Available(ActionId.Circulate) ? new BattleMenuChoice(ActionId.Circulate) : new BattleMenuChoice(ActionId.Concede);
        }

        private static IEnumerator PlayUntilExploring(SandboxBattleDirector director, Func<BattleSession, BattleMenuChoice> choose)
        {
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                switch (director.Phase)
                {
                    case BattleFlowPhase.Dialogue:
                        director.AdvanceDialogue();
                        break;
                    case BattleFlowPhase.PlayerChoosing:
                        director.SubmitChoice(choose(director.Session));
                        break;
                    case BattleFlowPhase.PlayerTargeting:
                        if (!director.TryChooseDestination(director.Session.CirculateDestination()))
                        {
                            director.CancelTargeting();
                            director.SubmitChoice(new BattleMenuChoice(ActionId.Concede));
                        }

                        break;
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "the battle did not finish in time");
        }

        private static Coord CellNoticedOnlyBy(GridModel grid, SandboxBattleDirector director, MapOpponent entry)
        {
            var center = entry.Actor.Walker.Cell;
            for (var distance = 1; distance <= entry.Opponent.AggroRadius; distance++)
            {
                for (var dx = -distance; dx <= distance; dx++)
                {
                    var dy = distance - Math.Abs(dx);
                    foreach (var cell in new[] { new Coord(center.X + dx, center.Y + dy), new Coord(center.X + dx, center.Y - dy) })
                    {
                        var free = grid.TryGet(cell, out var tile) && tile.IsPassable && !tile.IsOccupied;
                        var quiet = director.Opponents.Where(other => other != entry).All(other => !AggroRule.IsTriggered(cell, other.Actor.Walker.Cell, other.Opponent.AggroRadius));
                        if (free && quiet)
                        {
                            return cell;
                        }
                    }
                }
            }

            Assert.Fail($"no free cell only {entry.Opponent.Id} notices");
            return center;
        }

        private static Coord FreeNeighbour(GridSandboxController map, Coord cell)
        {
            var player = map.Player.Walker;
            return new[] { new Coord(cell.X + 1, cell.Y), new Coord(cell.X, cell.Y + 1), new Coord(cell.X - 1, cell.Y), new Coord(cell.X, cell.Y - 1) }
                .First(candidate => map.Grid.TryGet(candidate, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == player.ActorId)
                    && map.Exits.All(link => link.Cell != candidate));
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

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = TimeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
