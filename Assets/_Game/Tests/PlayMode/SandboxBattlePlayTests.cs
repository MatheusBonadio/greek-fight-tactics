using System.Collections;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class SandboxBattlePlayTests
    {
        private const string SandboxScene = "GridSandbox";
        private const float StartTimeoutSeconds = 5f;
        private const float BattleTimeoutSeconds = 90f;
        private const int QuietFrames = 30;

        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreTime()
        {
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnteringTheMap_PreparesEveryOpponentsArgumentsBeforeTheBattle()
        {
            yield return SceneManager.LoadSceneAsync(SandboxScene, LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var services = Object.FindAnyObjectByType<GameServices>();
            Assert.IsNotNull(services, "GridSandbox scene must contain GameServices");
            director.AutosaveEnabled = false;

            yield return WaitFor(() => director.PreparedArguments != null, StartTimeoutSeconds);
            Assert.IsNotNull(director.PreparedArguments, "arguments must be prepared on map enter, not on battle start");
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase);
            Assert.AreEqual(services.Arguments != null, director.PreparedArguments.IsOnline);

            foreach (var entry in director.Opponents)
            {
                var walker = entry.Actor.Walker;
                var opponent = new Debater(walker.ActorId, Side.Opponent, entry.Opponent.Profile, entry.Opponent.MaxCoherence, 5, walker.Cell, walker.Facing);
                Assert.IsTrue(director.PreparedArguments.TryNext(opponent, out var argument), entry.Opponent.Id);
                Assert.IsNotEmpty(argument.Speech);
                CollectionAssert.Contains(entry.Opponent.FallacyPool.Select(fallacy => fallacy.Id), argument.FallacyId, $"{entry.Opponent.Id} argued outside its pool");
            }
        }

        [UnityTest]
        public IEnumerator WalkingUpToAnOpponent_PlaysABattleAndReturnsToExploration()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            Assert.IsFalse(director.Hud.gameObject.activeSelf);
            director.AutosaveEnabled = false;
            yield return UnlockMaieutics();

            var target = director.Opponents[0];
            sandbox.Player.Walker.Teleport(FreeCellOnlyWithin(sandbox.Grid, director, target), Facing.NorthEast);

            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreEqual(BattleFlowPhase.Dialogue, director.Phase);
            Assert.AreSame(target, director.Engaged);
            Assert.IsTrue(director.Hud.gameObject.activeSelf);
            Assert.IsFalse(sandbox.ExplorationInputEnabled);

            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            var sawPlayerTurn = false;
            var sawRange = false;
            var sawOpponentPortrait = false;
            Assert.IsNotNull(director.PlayerPortrait, "GridSandbox scene must wire Kleia's portrait");
            Assert.IsNotNull(director.OpponentPortrait, "GridSandbox scene must wire the opponent's portrait");
            Assert.IsNotNull(director.RangeView, "GridSandbox scene must wire the range highlight view");
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    var dialogue = director.Hud.Dialogue;
                    if (dialogue.Line.SpeakerName == director.Opponent.DisplayName)
                    {
                        sawOpponentPortrait |= dialogue.PortraitFrame.activeSelf && dialogue.Line.Portrait == director.OpponentPortrait;
                    }

                    director.AdvanceDialogue();
                }
                else if (director.Phase == BattleFlowPhase.PlayerChoosing)
                {
                    sawPlayerTurn = true;
                    Assert.AreNotEqual(RejectionReason.ActionLocked, MenuEntry(director, ActionId.Maieutics).Reason, "the story flag unlocked maieutics");
                    sawRange |= director.RangeView.PaintedCount > 0;
                    director.SubmitChoice(PickChoice(director.Session));
                }
                else if (director.Phase == BattleFlowPhase.PlayerTargeting)
                {
                    Assert.IsFalse(director.Hud.GetComponent<BattleMenuInput>().enabled, "the menu must not take input while choosing a destination");
                    ChooseDestinationOrConcede(director);
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "the battle did not finish in time");
            Assert.IsTrue(sawPlayerTurn);
            Assert.IsTrue(sawRange, "the selected action range was never painted on the floor");
            Assert.IsTrue(sawOpponentPortrait, "the opponent spoke without his portrait");
            Assert.AreEqual(0, director.RangeView.PaintedCount);
            Assert.AreNotEqual(BattleStatus.Ongoing, director.LastResult);
            Assert.AreEqual(GameMode.Exploration, director.StateMachine.Mode);
            Assert.IsFalse(director.Hud.gameObject.activeSelf);
            Assert.IsTrue(sandbox.ExplorationInputEnabled);
        }

        [UnityTest]
        public IEnumerator EveryOpponentOnTheMap_ProvokesItsOwnBattle()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            director.AutosaveEnabled = false;
            Assert.GreaterOrEqual(director.Opponents.Count, 6, "the agora hosts Thrasybios and the five neighbourhood duelists");

            foreach (var entry in director.Opponents)
            {
                sandbox.Player.Walker.Teleport(FreeCellOnlyWithin(sandbox.Grid, director, entry), Facing.NorthEast);
                yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
                Assert.AreNotEqual(BattleFlowPhase.Exploring, director.Phase, $"{entry.Opponent.Id} did not notice Kleia");
                Assert.AreSame(entry, director.Engaged, $"walking up to {entry.Opponent.Id} started the wrong duel");
                Assert.AreEqual(entry.Opponent.DisplayName, director.Opponent.DisplayName);
                Assert.AreEqual(entry.Opponent.MaxCoherence, director.Session.State.Opponent.MaxCoherence);
                Assert.AreEqual(entry.Opponent.InitialAudience, director.Session.State.PlayerAudience);
                Assert.AreEqual(entry.Opponent.Profile, director.Session.State.Opponent.Profile);

                yield return PlayUntilExploring(director, _ => new BattleMenuChoice(ActionId.Withdraw));
                Assert.AreEqual(BattleStatus.PlayerWithdrew, director.LastResult, entry.Opponent.Id);
            }
        }

        [UnityTest]
        public IEnumerator OpenDialogue_HoldsBackANeighbourhoodDuelistUntilItCloses()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            director.AutosaveEnabled = false;
            var dialogue = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            dialogue.AutosaveEnabled = false;
            yield return WaitFor(() => dialogue.IsStoryLoaded, StartTimeoutSeconds);
            var target = director.Opponents.First(entry => entry.Opponent.AggroRadius == 1);

            Assert.IsTrue(dialogue.TryStartDialogue(dialogue.Talkers[0].Dialogue));
            sandbox.Player.Walker.Teleport(FreeCellOnlyWithin(sandbox.Grid, director, target), Facing.NorthEast);
            yield return Frames(QuietFrames);
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, $"{target.Opponent.Id} provoked while the dialogue box was open");

            for (var confirms = 0; dialogue.IsTalking && confirms < QuietFrames * 2; confirms++)
            {
                dialogue.Advance();
                yield return null;
            }

            Assert.IsFalse(dialogue.IsTalking);
            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreSame(target, director.Engaged, $"{target.Opponent.Id} provokes once the dialogue closes");
            yield return PlayUntilExploring(director, _ => new BattleMenuChoice(ActionId.Withdraw));
        }

        [UnityTest]
        public IEnumerator WalkingPastANeighbourhoodDuelist_StopsKleiaBesideThemAndStartsTheDuel()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            director.AutosaveEnabled = false;

            var (entry, start, beside, goal) = CrossingBeside(sandbox.Grid, director);
            sandbox.Player.Walker.Teleport(start, Facing.NorthEast);
            yield return null;
            Assert.IsTrue(sandbox.TryMovePlayerTo(goal));

            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreSame(entry, director.Engaged, $"walking past {entry.Opponent.Id} did not start the duel");
            Assert.AreEqual(beside, sandbox.Player.Walker.Cell, "Kleia must stop on the first cell the duelist notices");
            Assert.IsFalse(sandbox.Player.Walker.IsMoving);

            yield return PlayUntilExploring(director, _ => new BattleMenuChoice(ActionId.Withdraw));
        }

        [UnityTest]
        public IEnumerator WinningADuel_IsSavedAndTheOpponentStaysQuietAfterReloadingTheMap()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            var services = Object.FindAnyObjectByType<GameServices>();
            var target = director.Opponents
                .Where(entry => entry.Opponent.Profile == Profile.Sophist)
                .OrderBy(entry => entry.Opponent.MaxCoherence)
                .First();
            var opponentId = target.Opponent.Id;
            Assert.IsFalse(director.IsDefeated(target));
            yield return UnlockMaieutics();

            var approach = FreeCellOnlyWithin(sandbox.Grid, director, target);
            sandbox.Player.Walker.Teleport(approach, Facing.NorthEast);
            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreSame(target, director.Engaged);

            yield return PlayUntilExploring(director, PickChoice);
            Assert.AreEqual(BattleStatus.PlayerWon, director.LastResult, "always naming the right fallacy must beat a sophist");
            Assert.IsTrue(director.IsDefeated(target));
            Assert.IsTrue(services.TryLoadActiveSave(out var saved), "winning a duel must autosave");
            Assert.IsTrue(saved.Duels.TryGetValue(opponentId, out var record), $"the save has no duel against {opponentId}");
            Assert.IsTrue(record.Won);
            var pool = target.Opponent.FallacyPool.Select(fallacy => fallacy.Id).ToList();
            CollectionAssert.IsNotEmpty(services.Compendium.UnlockedIds, $"refuting {opponentId} must unlock the fallacy in the compendium");
            CollectionAssert.IsSubsetOf(services.Compendium.UnlockedIds, pool, $"{opponentId} only argues with his own pool");
            CollectionAssert.IsSubsetOf(services.Compendium.UnlockedIds, saved.Compendium);
            var standing = sandbox.Player.Walker.Cell;

            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            target = director.Opponents.Single(entry => entry.Opponent.Id == opponentId);
            AssertBackWhereSheWon(sandbox, standing);
            Assert.IsTrue(director.IsDefeated(target), "the reloaded map forgot the won duel");

            yield return Frames(QuietFrames);
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "a defeated opponent picked a fight after the reload");

            sandbox.Player.Walker.Teleport(FarCellFrom(sandbox.Grid, director, target), Facing.NorthEast);
            yield return Frames(QuietFrames);
            sandbox.Player.Walker.Teleport(FreeCellOnlyWithin(sandbox.Grid, director, target), Facing.NorthEast);
            yield return Frames(QuietFrames);
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "a defeated opponent picked a fight when Kleia came back");
        }

        [UnityTest]
        public IEnumerator Maieutics_StaysLockedInTheMenuUntilTheStoryUnlocksIt()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            director.AutosaveEnabled = false;
            var story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            story.AutosaveEnabled = false;
            yield return WaitFor(() => story.IsStoryLoaded, StartTimeoutSeconds);
            Assert.IsFalse(ActionUnlocks.IsUnlocked(ActionId.Maieutics, story.Story), "a new game starts without maieutics");
            var target = director.Opponents[0];

            yield return EngageUntilPlayerChooses(director, sandbox, target);
            var locked = MenuEntry(director, ActionId.Maieutics);
            Assert.IsFalse(locked.IsAvailable);
            Assert.AreEqual(RejectionReason.ActionLocked, locked.Reason);
            Assert.IsTrue(director.Session.State.IsLockedForPlayer(ActionId.Maieutics));
            Assert.AreNotEqual(RejectionReason.ActionLocked, MenuEntry(director, ActionId.Refute).Reason, "only maieutics waits for the story");

            director.Hud.Menu.HandlePoint(BattleMenuLayer.Actions, ActionMenuModel.PlayerActions.ToList().IndexOf(ActionId.Maieutics));
            var reason = director.Hud.Menu.ReasonLabel.text;
            Assert.IsNotEmpty(reason, "the locked action explains itself");
            StringAssert.DoesNotStartWith(Game.Content.UiTextSO.MissingPrefix, reason, "the locked reason comes from ui_text.json");
            yield return PlayUntilExploring(director, _ => new BattleMenuChoice(ActionId.Withdraw));

            story.Remember(new StoryEffect(ActionUnlocks.MaieuticsFlag));
            sandbox.Player.Walker.Teleport(FarCellFrom(sandbox.Grid, director, target), Facing.NorthEast);
            yield return Frames(QuietFrames);
            yield return EngageUntilPlayerChooses(director, sandbox, target);
            Assert.AreNotEqual(RejectionReason.ActionLocked, MenuEntry(director, ActionId.Maieutics).Reason, "Menexeno's flag unlocks maieutics in the next battle");
            Assert.IsFalse(director.Session.State.IsLockedForPlayer(ActionId.Maieutics));
            yield return PlayUntilExploring(director, _ => new BattleMenuChoice(ActionId.Withdraw));
        }

        private static void AssertBackWhereSheWon(GridSandboxController sandbox, Coord won)
        {
            var kleia = sandbox.Player.Walker;
            var holder = sandbox.Grid[won].OccupantId;
            if (holder == TileData.NoOccupant || holder == kleia.ActorId)
            {
                Assert.AreEqual(won, kleia.Cell, "the reload must put Kleia back where she won");
                return;
            }

            var startsThere = sandbox.Actors.Any(actor => actor.Walker != null && actor.Walker.ActorId == holder && new Coord(actor.StartCell.x, actor.StartCell.y) == won);
            Assert.IsTrue(startsThere, $"actor {holder} took the cell where Kleia won, but the map does not start it there");
            Assert.AreEqual(1, PositionalModifiers.Distance(won, kleia.Cell), $"the duel ended with Kleia on the cell where actor {holder} starts, so the reload puts her beside it");
        }

        private static IEnumerator UnlockMaieutics()
        {
            var story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            yield return WaitFor(() => story.IsStoryLoaded, StartTimeoutSeconds);
            Assert.IsTrue(story.IsStoryLoaded, "the story never loaded");
            story.Remember(new StoryEffect(ActionUnlocks.MaieuticsFlag));
        }

        private static IEnumerator EngageUntilPlayerChooses(SandboxBattleDirector director, GridSandboxController sandbox, MapOpponent target)
        {
            sandbox.Player.Walker.Teleport(FreeCellOnlyWithin(sandbox.Grid, director, target), Facing.NorthEast);
            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreSame(target, director.Engaged, $"{target.Opponent.Id} did not notice Kleia");

            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (director.Phase != BattleFlowPhase.PlayerChoosing && director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    director.AdvanceDialogue();
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.PlayerChoosing, director.Phase, "Kleia never got a turn");
        }

        private static ActionMenuEntry MenuEntry(SandboxBattleDirector director, ActionId action) =>
            director.Session.Menu.Entries.Single(entry => entry.Action == action);

        private static IEnumerator LoadSandbox(System.Action<(SandboxBattleDirector, GridSandboxController)> loaded)
        {
            yield return SceneManager.LoadSceneAsync(SandboxScene, LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.IsNotNull(director, "GridSandbox scene must contain the battle director");
            director.ThinkSeconds = 0f;
            yield return WaitFor(() => sandbox.ProgressLoaded, StartTimeoutSeconds);
            Assert.IsTrue(sandbox.ProgressLoaded, "the map never loaded the saved progress");
            loaded((director, sandbox));
        }

        private static IEnumerator PlayUntilExploring(SandboxBattleDirector director, System.Func<BattleSession, BattleMenuChoice> choose)
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
                        ChooseDestinationOrConcede(director);
                        break;
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "the battle did not finish in time");
        }

        private static void ChooseDestinationOrConcede(SandboxBattleDirector director)
        {
            if (!director.TryChooseDestination(director.Session.CirculateDestination()))
            {
                director.CancelTargeting();
                director.SubmitChoice(new BattleMenuChoice(ActionId.Concede));
            }
        }

        private static BattleMenuChoice PickChoice(BattleSession session)
        {
            bool Available(ActionId action) => session.Menu.Entries.Any(entry => entry.Action == action && entry.IsAvailable);

            var argument = session.State.CurrentArgument;
            if (argument.HasValue && argument.Value.IsRefutable && Available(ActionId.Refute))
            {
                return new BattleMenuChoice(ActionId.Refute, argument.Value.FallacyId);
            }

            if (argument.HasValue && Available(ActionId.Maieutics))
            {
                return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: argument.Value.WeakPremise);
            }

            return Available(ActionId.Circulate) ? new BattleMenuChoice(ActionId.Circulate) : new BattleMenuChoice(ActionId.Concede);
        }

        private static (MapOpponent Entry, Coord Start, Coord Beside, Coord Goal) CrossingBeside(GridModel grid, SandboxBattleDirector director)
        {
            var steps = new[] { new Coord(1, 0), new Coord(-1, 0), new Coord(0, 1), new Coord(0, -1) };
            bool IsFree(Coord cell) => grid.TryGet(cell, out var tile) && tile.IsPassable && !tile.IsOccupied;
            bool IsQuiet(Coord cell) => director.Opponents.All(other => !AggroRule.IsTriggered(cell, other.Actor.Walker.Cell, other.Opponent.AggroRadius));

            foreach (var entry in director.Opponents.Where(candidate => candidate.Opponent.AggroRadius == 1))
            {
                foreach (var toward in steps)
                {
                    var beside = entry.Actor.Walker.Cell + toward;
                    var across = toward.X == 0 ? new Coord(1, 0) : new Coord(0, 1);
                    var start = beside + across;
                    var goal = new Coord(beside.X - across.X, beside.Y - across.Y);
                    if (IsFree(beside) && IsFree(start) && IsFree(goal) && IsQuiet(start) && IsQuiet(goal) && NoticedFirstBy(director, entry, beside))
                    {
                        return (entry, start, beside, goal);
                    }
                }
            }

            Assert.Fail("no neighbourhood duelist has a free cell to walk past");
            return default;
        }

        private static Coord FreeCellOnlyWithin(GridModel grid, SandboxBattleDirector director, MapOpponent entry) =>
            FreeCellAtDistance(grid, entry, 1, entry.Opponent.AggroRadius, "first noticed within", cell => NoticedFirstBy(director, entry, cell));

        private static bool NoticedFirstBy(SandboxBattleDirector director, MapOpponent entry, Coord cell)
        {
            var distance = PositionalModifiers.Distance(cell, entry.Actor.Walker.Cell);
            return director.Opponents
                .Where(other => other != entry)
                .All(other => !AggroRule.IsTriggered(cell, other.Actor.Walker.Cell, other.Opponent.AggroRadius) || PositionalModifiers.Distance(cell, other.Actor.Walker.Cell) > distance);
        }

        private static Coord FarCellFrom(GridModel grid, SandboxBattleDirector director, MapOpponent entry) =>
            FreeCellAtDistance(grid, entry, entry.Opponent.AggroRadius + 2, grid.Width + grid.Height, "outside", cell => OutsideOtherRadii(director, entry, cell));

        private static bool OutsideOtherRadii(SandboxBattleDirector director, MapOpponent entry, Coord cell) =>
            director.Opponents
                .Where(other => other != entry)
                .All(other => !AggroRule.IsTriggered(cell, other.Actor.Walker.Cell, other.Opponent.AggroRadius));

        private static Coord FreeCellAtDistance(GridModel grid, MapOpponent entry, int nearest, int farthest, string where, System.Func<Coord, bool> accepts)
        {
            var center = entry.Actor.Walker.Cell;
            for (var distance = nearest; distance <= farthest; distance++)
            {
                for (var dx = -distance; dx <= distance; dx++)
                {
                    var dy = distance - System.Math.Abs(dx);
                    foreach (var candidate in new[] { new Coord(center.X + dx, center.Y + dy), new Coord(center.X + dx, center.Y - dy) })
                    {
                        if (grid.TryGet(candidate, out var tile) && tile.IsPassable && !tile.IsOccupied && accepts(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }

            Assert.Fail($"no free cell {where} the aggro radius of {entry.Opponent.Id}");
            return center;
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
