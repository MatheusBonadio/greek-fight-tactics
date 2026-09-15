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
        private const int ApproachDistance = 2;

        [UnityTearDown]
        public IEnumerator RestoreTime()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingUpToThrasybios_PlaysABattleAndReturnsToExploration()
        {
            yield return SceneManager.LoadSceneAsync(SandboxScene, LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.IsNotNull(director, "GridSandbox scene must contain the battle director");
            Assert.IsFalse(director.Hud.gameObject.activeSelf);
            director.AutosaveEnabled = false;
            director.ThinkSeconds = 0f;

            var opponentCell = director.OpponentActor.Walker.Cell;
            var approach = FreeCellNear(sandbox.Grid, opponentCell);
            sandbox.Player.Walker.Teleport(approach, Facing.NorthEast);

            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreEqual(BattleFlowPhase.Dialogue, director.Phase);
            Assert.IsTrue(director.Hud.gameObject.activeSelf);
            Assert.IsFalse(sandbox.ExplorationInputEnabled);

            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            var sawPlayerTurn = false;
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    director.AdvanceDialogue();
                }
                else if (director.Phase == BattleFlowPhase.PlayerChoosing)
                {
                    sawPlayerTurn = true;
                    director.SubmitChoice(PickChoice(director.Session));
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "the battle did not finish in time");
            Assert.IsTrue(sawPlayerTurn);
            Assert.AreNotEqual(BattleStatus.Ongoing, director.LastResult);
            Assert.AreEqual(GameMode.Exploration, director.StateMachine.Mode);
            Assert.IsFalse(director.Hud.gameObject.activeSelf);
            Assert.IsTrue(sandbox.ExplorationInputEnabled);
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

        private static Coord FreeCellNear(GridModel grid, Coord center)
        {
            for (var radius = 1; radius <= ApproachDistance; radius++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var dy = radius - System.Math.Abs(dx);
                    foreach (var candidate in new[] { new Coord(center.X + dx, center.Y + dy), new Coord(center.X + dx, center.Y - dy) })
                    {
                        if (grid.TryGet(candidate, out var tile) && tile.IsPassable && !tile.IsOccupied)
                        {
                            return candidate;
                        }
                    }
                }
            }

            Assert.Fail($"no free cell near {center}");
            return center;
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
