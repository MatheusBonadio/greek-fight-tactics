using System;
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
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class CompendiumPlayTests
    {
        private const string SandboxScene = "GridSandbox";
        private const float StartTimeoutSeconds = 5f;
        private const float BattleTimeoutSeconds = 90f;
        private const int ApproachDistance = 2;
        private const int CompendiumRow = 3;

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
        public IEnumerator RefutingCorrectly_ListsTheFallacyInThePauseCompendiumAndKeepsItInTheSave()
        {
            var services = default(GameServices);
            yield return LoadSandbox(loaded => services = loaded);
            Assert.IsEmpty(services.Compendium.UnlockedIds);

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            var story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            director.ThinkSeconds = 0f;
            var thrasybios = director.Opponents[0];
            yield return WaitFor(() => story.IsStoryLoaded, StartTimeoutSeconds);
            Assert.IsTrue(story.Remember(new StoryEffect(thrasybios.Opponent.Tutorial.DoneFlag)), "the lesson with Thrasybios could not be marked as done");
            sandbox.Player.Walker.Teleport(FreeCellNear(sandbox.Grid, thrasybios.Actor.Walker.Cell), Facing.NorthEast);
            yield return null;
            DuelChallenge.Confirm(sandbox, thrasybios.Actor.Walker.Cell);
            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreNotEqual(BattleFlowPhase.Exploring, director.Phase, "confirming on Thrasybios must start the battle");
            Assert.IsNull(director.Tutorial, "after the lesson one refutation does not end the duel, so Kleia can still withdraw");

            string refuted = null;
            yield return PlayUntilRefutedThenWithdraw(director, id => refuted = id);
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "the battle did not finish in time");
            Assert.IsNotNull(refuted, "the player never had a refutable argument to answer");
            Assert.IsTrue(services.Catalog.TryGet(refuted, out var fallacy));
            CollectionAssert.AreEqual(new[] { refuted }, services.Compendium.UnlockedIds);

            var pause = Object.FindAnyObjectByType<PauseMenuController>();
            pause.Input.Panel.HandleCancel();
            Assert.IsTrue(pause.IsPaused);
            Assert.IsTrue(pause.PauseMenu.Model.Entries[CompendiumRow].Enabled);
            Assert.AreEqual(SystemMenus.Compendium, pause.PauseMenu.Model.Entries[CompendiumRow].Id);

            pause.PauseMenu.HandlePoint(CompendiumRow);
            pause.PauseMenu.HandleConfirm();
            Assert.IsTrue(pause.Compendium.gameObject.activeInHierarchy);
            Assert.AreSame(pause.Compendium, pause.Input.Panel);
            Assert.AreEqual(refuted, pause.Compendium.SelectedId);
            Assert.AreEqual(fallacy.DisplayName, pause.Compendium.Rows[0].text);
            Assert.AreEqual(fallacy.DisplayName, pause.Compendium.Title.text);
            Assert.AreEqual(fallacy.Description, pause.Compendium.Description.text);

            pause.Compendium.HandleCancel();
            Assert.IsFalse(pause.Compendium.gameObject.activeSelf);
            Assert.AreSame(pause.PauseMenu, pause.Input.Panel);
            pause.PauseMenu.HandleCancel();
            Assert.IsFalse(pause.IsPaused);

            Assert.IsTrue(services.TryLoadActiveSave(out var save), "the battle outro must autosave");
            CollectionAssert.Contains(save.Compendium, refuted);

            yield return LoadSandbox(loaded => services = loaded);
            CollectionAssert.AreEqual(new[] { refuted }, services.Compendium.UnlockedIds);
        }

        private static IEnumerator LoadSandbox(Action<GameServices> loaded)
        {
            yield return SceneManager.LoadSceneAsync(SandboxScene, LoadSceneMode.Single);
            yield return null;

            var services = Object.FindAnyObjectByType<GameServices>();
            Assert.IsNotNull(services, "GridSandbox scene must contain GameServices");
            yield return WaitFor(() => services.IsBooted, StartTimeoutSeconds);
            Assert.IsTrue(services.IsBooted);
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            yield return WaitFor(() => sandbox.ProgressLoaded, StartTimeoutSeconds);
            Assert.IsTrue(sandbox.ProgressLoaded, "the map never loaded the saved progress");
            loaded(services);
        }

        private static IEnumerator PlayUntilRefutedThenWithdraw(SandboxBattleDirector director, Action<string> refuted)
        {
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            var hit = false;
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                switch (director.Phase)
                {
                    case BattleFlowPhase.Dialogue:
                        director.AdvanceDialogue();
                        break;
                    case BattleFlowPhase.PlayerChoosing:
                        var choice = hit ? new BattleMenuChoice(ActionId.Withdraw) : PickChoice(director.Session);
                        if (choice.Action == ActionId.Refute)
                        {
                            hit = true;
                            refuted(choice.FallacyId);
                        }

                        director.SubmitChoice(choice);
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
        }

        private static BattleMenuChoice PickChoice(BattleSession session)
        {
            bool Available(ActionId action) => session.Menu.Entries.Any(entry => entry.Action == action && entry.IsAvailable);

            var argument = session.State.CurrentArgument;
            if (AnswerRule.Expected(session.State) != ActionId.Refute)
            {
                return new BattleMenuChoice(ActionId.Concede);
            }

            if (Available(ActionId.Refute))
            {
                return new BattleMenuChoice(ActionId.Refute, argument.Value.FallacyId);
            }

            return Available(ActionId.Circulate) ? new BattleMenuChoice(ActionId.Circulate) : new BattleMenuChoice(ActionId.Concede);
        }

        private static Coord FreeCellNear(GridModel grid, Coord center)
        {
            for (var radius = 1; radius <= ApproachDistance; radius++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var dy = radius - Math.Abs(dx);
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
