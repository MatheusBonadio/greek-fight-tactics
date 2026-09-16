using System;
using System.Collections;
using Game.Core;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class SystemMenuPlayTests
    {
        private const float TimeoutSeconds = 5f;
        private const string SandboxScene = "GridSandbox";
        private const int NewGameRow = 1;
        private const int OptionsRow = 2;
        private const int CreditsRow = 3;
        private const int ResumeRow = 0;
        private const int SaveRow = 1;
        private const int CompendiumRow = 3;

        private GameServices services;
        private GameSettings originalSettings;
        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreSettingsAndTime()
        {
            data?.Dispose();
            data = null;
            if (services && originalSettings != null)
            {
                services.UpdateSettings(originalSettings);
            }

            services = null;
            originalSettings = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenu_OffersContinueOnlyWithASaveAndOpensOptionsAndCredits()
        {
            var controller = default(MainMenuController);
            yield return LoadMainMenu(loaded => controller = loaded);

            Assert.AreEqual(services.HasContinue, controller.MainMenu.Model.Entries[0].Enabled);
            Assert.AreSame(controller.MainMenu, controller.Input.Panel);

            controller.MainMenu.HandlePoint(OptionsRow);
            controller.MainMenu.HandleConfirm();
            Assert.IsTrue(controller.Options.gameObject.activeSelf);
            Assert.AreSame(controller.Options, controller.Input.Panel);

            var before = services.Settings.VolumeStep;
            controller.Options.HandleNavigate(before == GameSettings.VolumeSteps ? Vector2.left : Vector2.right);
            Assert.AreNotEqual(before, services.Settings.VolumeStep);
            Assert.AreEqual(services.Settings.MasterVolume, AudioListener.volume, 0.0001f);

            controller.Options.HandleCancel();
            Assert.IsTrue(controller.MainMenu.gameObject.activeSelf);
            Assert.AreSame(controller.MainMenu, controller.Input.Panel);

            controller.MainMenu.HandlePoint(CreditsRow);
            controller.MainMenu.HandleConfirm();
            Assert.IsTrue(controller.Credits.gameObject.activeSelf);
            controller.Credits.HandleCancel();
            Assert.IsTrue(controller.MainMenu.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator MainMenu_NewGameAsksForASlotAndCancelReturns()
        {
            var controller = default(MainMenuController);
            yield return LoadMainMenu(loaded => controller = loaded);

            controller.MainMenu.HandlePoint(NewGameRow);
            controller.MainMenu.HandleConfirm();

            Assert.IsTrue(controller.Slots.gameObject.activeSelf);
            Assert.AreEqual(SaveSlotsMode.Save, controller.Slots.Model.Mode);
            Assert.AreEqual(SaveSlots.Count, controller.Slots.Model.Slots.Count);
            Assert.AreSame(controller.Slots, controller.Input.Panel);

            controller.Slots.HandleCancel();
            Assert.IsTrue(controller.MainMenu.gameObject.activeSelf);
            Assert.AreEqual(PauseMenuController.DefaultMainMenuScene, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTimeUntilResumed()
        {
            var pause = default(PauseMenuController);
            yield return LoadSandboxPause(loaded => pause = loaded);

            pause.Input.Panel.HandleCancel();

            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(pause.Panel.activeSelf);
            Assert.AreSame(pause.PauseMenu, pause.Input.Panel);
            Assert.IsTrue(pause.PauseMenu.Model.Entries[SaveRow].Enabled);
            Assert.IsFalse(pause.PauseMenu.Model.Entries[CompendiumRow].Enabled);

            pause.PauseMenu.HandlePoint(SaveRow);
            pause.PauseMenu.HandleConfirm();
            Assert.IsTrue(pause.Slots.gameObject.activeSelf);
            pause.Slots.HandleCancel();
            Assert.AreSame(pause.PauseMenu, pause.Input.Panel);

            pause.PauseMenu.HandlePoint(ResumeRow);
            pause.PauseMenu.HandleConfirm();

            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(pause.Panel.activeSelf);
            Assert.AreSame(pause, pause.Input.Panel);
        }

        [UnityTest]
        public IEnumerator Pause_CapturesTheExplorationWithLocationAndPlaytime()
        {
            var pause = default(PauseMenuController);
            yield return LoadSandboxPause(loaded => pause = loaded);
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();

            Assert.IsNotNull(pause.SaveProvider);
            var save = pause.SaveProvider();

            Assert.AreEqual(sandbox.Player.Walker.Cell, save.Position);
            Assert.AreEqual(sandbox.LocationId, save.Location);
            Assert.IsNotEmpty(save.FogBitmask);
            Assert.GreaterOrEqual(save.PlaytimeSeconds, 0);
            Assert.Greater(save.Timestamp, 0);
        }

        private IEnumerator LoadMainMenu(Action<MainMenuController> loaded)
        {
            yield return SceneManager.LoadSceneAsync(PauseMenuController.DefaultMainMenuScene, LoadSceneMode.Single);
            var controller = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(controller, "MainMenu scene must contain a MainMenuController");

            yield return WaitFor(() => controller.IsReady);
            Assert.IsTrue(controller.IsReady);
            services = controller.Services;
            originalSettings = services.Settings.Clone();
            loaded(controller);
        }

        private IEnumerator LoadSandboxPause(Action<PauseMenuController> loaded)
        {
            yield return SceneManager.LoadSceneAsync(SandboxScene, LoadSceneMode.Single);
            yield return null;
            var pause = Object.FindAnyObjectByType<PauseMenuController>();
            Assert.IsNotNull(pause, "GridSandbox scene must contain the pause menu");

            yield return WaitFor(() => pause.Services && pause.Services.IsBooted);
            services = pause.Services;
            originalSettings = services.Settings.Clone();
            loaded(pause);
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
