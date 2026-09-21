using System;
using System.Collections;
using System.Linq;
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
