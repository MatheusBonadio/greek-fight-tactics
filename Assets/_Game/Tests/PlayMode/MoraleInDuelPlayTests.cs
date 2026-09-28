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

namespace Game.Tests.PlayMode
{
    public sealed class MoraleInDuelPlayTests
    {
        private const string SandboxScene = "GridSandbox";
        private const float StartTimeoutSeconds = 10f;

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
        public IEnumerator TheVeryFirstDuel_AlreadyShowsTheCrowdWeighingOnTheHud()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            director.AutosaveEnabled = false;
            director.CrowdSeed = 402;
            var crowdView = Object.FindAnyObjectByType<BattleCrowd>();
            if (crowdView)
            {
                crowdView.ArrivalCapSeconds = 0f;
            }

            var target = director.Opponents.First(entry => entry.IsPlaced);
            yield return StartDuelBeside(director, sandbox, target);
            Assert.IsNotNull(director.Tutorial, "the first duel of a new game is the guided lesson");

            var state = director.Session.State;
            var rules = state.Rules;
            var crowd = CrowdCells(state).ToList();
            Assert.IsNotEmpty(crowd, "the arena seats a crowd on the battle grid");

            var openingDistance = crowd.Min(cell => Manhattan(cell, state.Player.Position));
            TestContext.WriteLine($"{crowd.Count} listeners; the closest sits {openingDistance} cells from Kleia at the opening");
            var opening = MoraleRule.ShiftFor(state, state.Player);
            TestContext.WriteLine($"opening morale: audience {state.PlayerAudience}%, shift {opening}, label \"{director.Hud.PlayerPanel.MoraleLabel.text}\"");

            var reachable = new List<Coord>();
            new PathFinder().Reachable(state.Grid, state.Player.Position, rules.MoveRange, reachable);
            var best = reachable
                .Select(cell => (Cell: cell, Listeners: crowd.Count(seat => Manhattan(seat, cell) <= rules.MoraleRadius)))
                .OrderByDescending(step => step.Listeners)
                .First();
            TestContext.WriteLine($"the best cell within {rules.MoveRange} steps is {best.Cell} with {best.Listeners} listeners in reach");

            Assert.Greater(best.Listeners, 0,
                $"circulating never brings Kleia within {rules.MoraleRadius} cells of the crowd, so the morale indicator can never light up");

            yield return Circulate(director, best.Cell);

            var shift = MoraleRule.ShiftFor(director.Session.State, director.Session.State.Player);
            TestContext.WriteLine($"after circulating: audience {director.Session.State.PlayerAudience}%, morale {shift}, label \"{director.Hud.PlayerPanel.MoraleLabel.text}\"");
            Assert.AreNotEqual(0, shift, "standing beside the crowd must weigh on the blow");
            Assert.AreNotEqual(0, opening, "the lesson opens with the crowd already weighing");
            Assert.IsNotEmpty(director.Hud.PlayerPanel.MoraleLabel.text, "the panel must show the morale once the crowd is close");
            Assert.AreEqual(director.Hud.PlayerPanel.MoraleText(shift), director.Hud.PlayerPanel.MoraleLabel.text);
        }

        private static IEnumerable<Coord> CrowdCells(BattleState state)
        {
            for (var x = 0; x < state.Grid.Width; x++)
            {
                for (var y = 0; y < state.Grid.Height; y++)
                {
                    var cell = new Coord(x, y);
                    if (state.Grid.TryGet(cell, out var tile) && tile.IsOccupied && state.IsInCrowd(tile.OccupantId))
                    {
                        yield return cell;
                    }
                }
            }
        }

        private static IEnumerator Circulate(SandboxBattleDirector director, Coord destination)
        {
            var deadline = Time.realtimeSinceStartup + StartTimeoutSeconds;
            while (director.Phase != BattleFlowPhase.PlayerChoosing && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    director.AdvanceDialogue();
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.PlayerChoosing, director.Phase, "Kleia never got her turn");
            director.SubmitChoice(new BattleMenuChoice(ActionId.Circulate, destination: destination));
            yield return WaitFor(() => director.Session.State.Player.Position == destination, StartTimeoutSeconds);
            Assert.AreEqual(destination, director.Session.State.Player.Position, "Kleia never reached the cell beside the crowd");
            director.Hud.Sync();
        }

        private static IEnumerator StartDuelBeside(SandboxBattleDirector director, GridSandboxController sandbox, MapOpponent target)
        {
            Assert.IsTrue(DuelChallenge.TryFindFreeCellBeside(sandbox, target.Actor.Walker.Cell, out var beside), "no free cell beside the challenger");
            sandbox.Player.Walker.Teleport(beside, Facing.NorthEast);
            yield return null;

            DuelChallenge.Confirm(sandbox, target.Actor.Walker.Cell);
            yield return WaitFor(() => director.Session != null && director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.IsNotNull(director.Session, "the duel never started");
        }

        private static IEnumerator LoadSandbox(System.Action<(SandboxBattleDirector, GridSandboxController)> loaded)
        {
            yield return SceneManager.LoadSceneAsync(SandboxScene, LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.IsNotNull(director, "GridSandbox scene must contain the battle director");
            director.ThinkSeconds = 0f;
            yield return WaitFor(() => sandbox.ProgressLoaded, StartTimeoutSeconds);
            var story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            yield return WaitFor(() => story.IsStoryLoaded, StartTimeoutSeconds);
            story.Remember(new StoryEffect(ActionUnlocks.MaieuticsFlag));
            loaded((director, sandbox));
        }

        private static int Manhattan(Coord from, Coord to) =>
            System.Math.Abs(from.X - to.X) + System.Math.Abs(from.Y - to.Y);

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
