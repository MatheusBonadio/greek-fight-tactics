using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class ExplorationInputPlayTests
    {
        private const string SceneName = "GridSandbox";
        private const float TimeoutSeconds = 5f;
        private const float WalkWindowSeconds = GridWalker.SecondsPerCell * 2f;
        private const float DebugPanelInset = 20f;
        private const int MaxDialogueTaps = 16;
        private const string ChoiceDialogue = "test_choice";
        private const string ChoiceFlag = "test_choice_answer";
        private const string ThrasybiosId = "thrasybios";
        private const float DuelTimeoutSeconds = 10f;

        private static readonly Coord Target = new(1, 1);

        private readonly InputTestFixture input = new();
        private IsolatedDataDirectory data;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputActionAsset controls;
        private GridSandboxController sandbox;
        private PauseMenuController pause;
        private Coord start;

        [UnitySetUp]
        public IEnumerator LoadSandboxWithTheCursorBesideKleia()
        {
            yield return UnloadEveryScene();
            data = IsolatedDataDirectory.Enter();
            input.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            pause = Object.FindAnyObjectByType<PauseMenuController>();
            Assert.IsNotNull(sandbox, "GridSandbox scene must contain a GridSandboxController");
            Assert.IsNotNull(pause, "GridSandbox scene must contain the pause menu");

            controls = sandbox.Controls;
            ResolveBindingsAgainstCurrentDevices(controls);
            Assert.IsNotEmpty(controls.FindAction(BattleMenuInput.ConfirmAction, true).controls, "the scene's input actions must bind to the test keyboard");

            yield return WaitFor(() => pause.Services && pause.Services.IsBooted);
            Assert.IsTrue(pause.Services.IsBooted);

            start = sandbox.Player.Walker.Cell;
            Assert.AreNotEqual(Target, start);
            Assert.IsTrue(sandbox.Cursor.TryHover(IsoMath.GridToWorld(Target), Vector2.zero));
        }

        [UnityTearDown]
        public IEnumerator UnloadSandboxBeforeRestoringInput()
        {
            Time.timeScale = 1f;
            yield return UnloadEveryScene();

            input.TearDown();
            if (controls)
            {
                ResolveBindingsAgainstCurrentDevices(controls);
            }

            data?.Dispose();
            data = null;
            controls = null;
            sandbox = null;
            pause = null;
            keyboard = null;
            mouse = null;
        }

        [UnityTest]
        public IEnumerator Confirm_WhileExploring_WalksToTheCursor()
        {
            yield return Tap(keyboard.enterKey);
            yield return WaitFor(() => !sandbox.Player.Walker.IsMoving);

            Assert.AreEqual(Target, sandbox.Player.Walker.Cell);
        }

        [UnityTest]
        public IEnumerator Confirm_InsideThePauseMenu_NeverWalksBeforeOrAfterResuming()
        {
            yield return Tap(keyboard.escapeKey);
            Assert.IsTrue(pause.IsPaused);

            yield return Tap(keyboard.downArrowKey);
            yield return Tap(keyboard.downArrowKey);
            Assert.AreEqual(SystemMenus.Options, pause.PauseMenu.Model.Selected.Id);
            Assert.AreEqual(Target, sandbox.Cursor.Cell, "navigating the pause menu must not move the grid cursor");

            yield return Tap(keyboard.enterKey);
            Assert.IsTrue(pause.Options.gameObject.activeSelf);
            AssertKleiaStayed("a confirm inside the pause menu");

            yield return Tap(keyboard.escapeKey);
            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(pause.IsPaused);

            yield return WaitRealtime(WalkWindowSeconds);
            AssertKleiaStayed("resuming after a confirm inside the pause menu");
        }

        [UnityTest]
        public IEnumerator Confirm_ThatResumesTheGame_DoesNotWalk()
        {
            yield return Tap(keyboard.escapeKey);
            Assert.AreEqual(SystemMenus.Resume, pause.PauseMenu.Model.Selected.Id);

            yield return Tap(keyboard.enterKey);
            Assert.IsFalse(pause.IsPaused);

            yield return WaitRealtime(WalkWindowSeconds);
            AssertKleiaStayed("the confirm that closed the pause menu");
        }

        [UnityTest]
        public IEnumerator Click_WhilePaused_DoesNotWalkAfterResuming()
        {
            yield return Tap(keyboard.escapeKey);
            yield return Tap(mouse.leftButton);
            Assert.IsTrue(pause.IsPaused);
            AssertKleiaStayed("a click while paused");

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(pause.IsPaused);

            yield return WaitRealtime(WalkWindowSeconds);
            AssertKleiaStayed("resuming after a click while paused");
        }

        [UnityTest]
        public IEnumerator Click_OnTheDebugPanel_NeitherMovesTheCursorNorWalks()
        {
            sandbox.DebugPanelVisible = true;
            var overPanel = new Vector2(DebugPanelInset, Screen.height - DebugPanelInset);
            Assert.IsTrue(sandbox.IsOverDebugPanel(overPanel));

            input.Set(mouse.position, overPanel);
            yield return null;
            yield return Tap(mouse.leftButton);
            yield return WaitRealtime(WalkWindowSeconds);

            Assert.AreEqual(Target, sandbox.Cursor.Cell, "pointing at the debug panel must not move the grid cursor");
            AssertKleiaStayed("a click on the debug panel");

            var targetOnScreen = (Vector2)Camera.main.WorldToScreenPoint(IsoMath.GridToWorld(Target));
            Assert.IsFalse(sandbox.IsOverDebugPanel(targetOnScreen));
            input.Set(mouse.position, targetOnScreen);
            yield return null;
            yield return Tap(mouse.leftButton);
            yield return WaitFor(() => !sandbox.Player.Walker.IsMoving);

            Assert.AreEqual(Target, sandbox.Player.Walker.Cell, "a click outside the debug panel must still walk");
        }

        [UnityTest]
        public IEnumerator Click_OnAChallenger_WalksBesideHimAndStartsTheDuel()
        {
            var (battle, challenger) = default((SandboxBattleDirector, MapOpponent));
            yield return FindChallenger(found => (battle, challenger) = found);

            yield return PointWithTheMouse(challenger.Actor.Walker.Cell);
            yield return Tap(mouse.leftButton);
            Assert.IsTrue(sandbox.Player.Walker.IsMoving, "clicking a distant challenger walks up to him");
            Assert.AreEqual(BattleFlowPhase.Exploring, battle.Phase, "the duel waits until Kleia stands beside him");

            yield return WaitFor(() => battle.Phase != BattleFlowPhase.Exploring, DuelTimeoutSeconds);
            Assert.AreSame(challenger, battle.Engaged, "clicking the challenger's cell starts his duel");
            Assert.IsTrue(PathFinder.AreBeside(sandbox.Player.Walker.Cell, challenger.Actor.Walker.Cell));
            Assert.IsFalse(sandbox.Player.Walker.IsMoving);
        }

        [UnityTest]
        public IEnumerator ArrowsAndEnter_OnAChallenger_StartTheDuel()
        {
            var (battle, challenger) = default((SandboxBattleDirector, MapOpponent));
            yield return FindChallenger(found => (battle, challenger) = found);
            var below = challenger.Actor.Walker.Cell + new Coord(-1, -1);
            Assert.IsTrue(sandbox.Cursor.TryHover(IsoMath.GridToWorld(below), Vector2.zero));

            yield return Tap(keyboard.upArrowKey);
            Assert.AreEqual(challenger.Actor.Walker.Cell, sandbox.Cursor.Cell, "up moves the cursor one cell up the screen, onto the challenger");
            yield return Tap(keyboard.enterKey);

            yield return WaitFor(() => battle.Phase != BattleFlowPhase.Exploring, DuelTimeoutSeconds);
            Assert.AreSame(challenger, battle.Engaged, "enter on the challenger's cell starts his duel");
            Assert.IsTrue(PathFinder.AreBeside(sandbox.Player.Walker.Cell, challenger.Actor.Walker.Cell));
        }

        [UnityTest]
        public IEnumerator Enter_BesideAChallenger_OnlyWalksThere()
        {
            var (battle, challenger) = default((SandboxBattleDirector, MapOpponent));
            yield return FindChallenger(found => (battle, challenger) = found);
            var beside = challenger.Actor.Walker.Cell + new Coord(-1, 0);
            Assert.IsTrue(sandbox.Grid[beside].IsPassable && !sandbox.Grid[beside].IsOccupied, $"{beside} must be free to walk to");

            yield return PointWithTheMouse(beside);
            yield return Tap(keyboard.enterKey);
            yield return WaitFor(() => !sandbox.Player.Walker.IsMoving || battle.Phase != BattleFlowPhase.Exploring, DuelTimeoutSeconds);
            yield return WaitRealtime(WalkWindowSeconds);

            Assert.AreEqual(beside, sandbox.Player.Walker.Cell, "Kleia walks right up to the challenger");
            Assert.AreEqual(BattleFlowPhase.Exploring, battle.Phase, "walking up to a challenger never starts the duel on its own");
            Assert.AreEqual(GameMode.Exploration, sandbox.StateMachine.Mode);
        }

        [UnityTest]
        public IEnumerator ArrowsAndEnter_PickADialogueChoiceAndItsFlagIsSaved()
        {
            var story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            yield return WaitFor(() => story.IsStoryLoaded);
            var library = ScriptableObject.CreateInstance<DialogueLibrarySO>();
            try
            {
                library.SetEntries(
                    new Dictionary<string, DialogueSpeaker> { ["kleia"] = new("Kleia", "kleia") },
                    new[]
                    {
                        new DialogueScript(ChoiceDialogue, new[]
                        {
                            new ScriptLine("kleia", "Levo as ânforas agora ou depois?", choices: new[]
                            {
                                new DialogueChoice("Agora", new[] { new StoryEffect(ChoiceFlag, "now") }),
                                new DialogueChoice("Depois", new[] { new StoryEffect(ChoiceFlag, "later") }, new[] { new ScriptLine("kleia", "Primeiro, a ágora.") })
                            })
                        })
                    });
                story.Configure(sandbox, story.DialogueBox, library, story.Talkers.ToList(), story.Triggers.ToList(), story.Portraits.ToDictionary(pair => pair.Key, pair => pair.Value));
                Assert.IsTrue(story.TryStartDialogue(ChoiceDialogue));

                for (var taps = 0; taps < MaxDialogueTaps && !story.IsAwaitingChoice; taps++)
                {
                    yield return Tap(keyboard.enterKey);
                }

                Assert.IsTrue(story.IsAwaitingChoice, "the question never offered its options");
                Assert.AreEqual(0, story.DialogueBox.ChoiceCursor);
                yield return Tap(keyboard.downArrowKey);
                Assert.AreEqual(1, story.DialogueBox.ChoiceCursor, "the arrows move between the options");
                Assert.AreEqual(Target, sandbox.Cursor.Cell, "navigating the options must not move the grid cursor");
                yield return Tap(keyboard.enterKey);

                Assert.IsFalse(story.IsAwaitingChoice);
                Assert.AreEqual("Primeiro, a ágora.", story.CurrentLine?.Text, "the chosen option plays its own reply");
                for (var taps = 0; taps < MaxDialogueTaps && story.IsTalking; taps++)
                {
                    yield return Tap(keyboard.enterKey);
                }

                Assert.IsFalse(story.IsTalking);
                Assert.AreEqual("later", story.Story.Flags[ChoiceFlag]);
                Assert.IsTrue(pause.Services.TryLoadActiveSave(out var saved), "closing the dialogue autosaves");
                Assert.AreEqual("later", saved.Flags[ChoiceFlag]);
                yield return WaitRealtime(WalkWindowSeconds);
                AssertKleiaStayed("the enter that answered the question");
            }
            finally
            {
                Object.Destroy(library);
            }
        }

        private IEnumerator FindChallenger(Action<(SandboxBattleDirector, MapOpponent)> found)
        {
            var battle = Object.FindAnyObjectByType<SandboxBattleDirector>();
            Assert.IsNotNull(battle, "GridSandbox scene must contain the battle director");
            battle.AutosaveEnabled = false;
            yield return WaitFor(() => sandbox.ProgressLoaded);
            var challenger = battle.Opponents.Single(entry => entry.Opponent.Id == ThrasybiosId);
            Assert.IsTrue(sandbox.Grid[challenger.Actor.Walker.Cell].Revealed, "Thrasybios stands in plain sight");
            Assert.IsFalse(PathFinder.AreBeside(start, challenger.Actor.Walker.Cell), "Kleia starts away from Thrasybios");
            found((battle, challenger));
        }

        private IEnumerator PointWithTheMouse(Coord cell)
        {
            var onScreen = (Vector2)Camera.main.WorldToScreenPoint(IsoMath.GridToWorld(cell));
            Assert.IsTrue(new Rect(0f, 0f, Screen.width, Screen.height).Contains(onScreen), $"{cell} must be on screen to click it");
            input.Set(mouse.position, onScreen);
            yield return null;
            Assert.AreEqual(cell, sandbox.Cursor.Cell, "the mouse points the cursor at the cell");
        }

        private IEnumerator Tap(ButtonControl button)
        {
            input.Press(button, queueEventOnly: true);
            yield return null;
            input.Release(button, queueEventOnly: true);
            yield return null;
        }

        private void AssertKleiaStayed(string cause)
        {
            Assert.AreEqual(start, sandbox.Player.Walker.Cell, $"{cause} must not move Kleia");
            Assert.IsFalse(sandbox.Player.Walker.IsMoving, $"{cause} must not leave a walk queued");
        }

        private static void ResolveBindingsAgainstCurrentDevices(InputActionAsset asset)
        {
            asset.devices = InputSystem.devices;
            asset.devices = null;
        }

        private static IEnumerator UnloadEveryScene()
        {
            var holder = SceneManager.CreateScene($"{nameof(ExplorationInputPlayTests)} {Guid.NewGuid():N}");
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(scene => scene != holder).ToList();
            SceneManager.SetActiveScene(holder);
            foreach (var scene in loaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
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
