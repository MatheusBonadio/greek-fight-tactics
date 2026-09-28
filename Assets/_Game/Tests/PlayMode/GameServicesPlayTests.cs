using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.Net;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class GameServicesPlayTests
    {
        private const float TimeoutSeconds = 5f;

        private readonly List<Object> createdAssets = new();
        private string directory;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-services-{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (host)
            {
                Object.DestroyImmediate(host);
            }

            foreach (var asset in createdAssets)
            {
                Object.DestroyImmediate(asset);
            }

            createdAssets.Clear();

            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        [UnityTest]
        public IEnumerator Boot_OpensTheLocalDatabaseWithoutTouchingTheNetwork()
        {
            var services = CreateServices();
            yield return WaitForBoot(services);

            Assert.IsNotNull(services.Database);
            Assert.IsNotNull(services.LocalSaves);
            Assert.IsFalse(services.HasContinue);
            Assert.AreEqual(SaveDatabase.SchemaVersion, services.Database.ReadVersion());
        }

        [UnityTest]
        public IEnumerator SandboxScene_BootsItsServicesFromTheLocalDatabase()
        {
            using var data = IsolatedDataDirectory.Enter();
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("GridSandbox", UnityEngine.SceneManagement.LoadSceneMode.Single);
            yield return null;

            var services = Object.FindAnyObjectByType<GameServices>();
            Assert.IsNotNull(services, "GridSandbox scene must contain GameServices");
            yield return WaitForBoot(services);

            Assert.IsNotNull(services.Catalog, "the scene wires the fallacy catalog for the compendium");
            StringAssert.StartsWith(data.Directory, services.Database.FilePath, "the scene never touches the player's real data");
        }

        [UnityTest]
        public IEnumerator Settings_LoadOnBootApplyTheVolumeAndPersistChanges()
        {
            new LocalSettingsStore(new SaveDatabase(directory)).Save(new Game.Core.GameSettings { MasterVolume = 0.3f, AssistedMode = true });

            var services = CreateServices();
            yield return WaitForBoot(services);

            Assert.AreEqual(0.3f, services.Settings.MasterVolume, 0.0001f);
            Assert.AreEqual(0.3f, AudioListener.volume, 0.0001f);
            Assert.IsTrue(services.Settings.AssistedMode);

            Game.Core.GameSettings notified = null;
            services.SettingsChanged += settings => notified = settings;
            var changed = new Game.Core.GameSettings { TextSpeed = Game.Core.TextSpeed.Fast };
            services.UpdateSettings(changed);

            Assert.AreSame(changed, notified);
            Assert.AreEqual(Game.Core.GameSettings.DefaultVolume, AudioListener.volume, 0.0001f);
            Assert.AreEqual(Game.Core.TextSpeed.Fast, new LocalSettingsStore(new SaveDatabase(directory)).Load().TextSpeed);
        }

        [UnityTest]
        public IEnumerator Autosave_WritesTheSingleLocalSlotAndEnablesContinue()
        {
            var services = CreateServices();
            yield return WaitForBoot(services);
            Assert.IsFalse(services.HasContinue);

            services.Autosave(new Game.Core.SaveData { Reputation = 5, Timestamp = 42 });

            Assert.IsTrue(services.HasContinue);
            Assert.IsTrue(services.LocalSaves.TryLoad(out var save, out var error), error);
            Assert.AreEqual(5, save.Reputation);
            Assert.IsTrue(File.Exists(Path.Combine(directory, SaveDatabase.FileName)), "the save lives in the SQLite database");
            Assert.IsTrue(new LocalSaveStore(new SaveDatabase(directory)).TryLoad(out var reread, out error), error);
            Assert.AreEqual(42, reread.Timestamp);
        }

        [UnityTest]
        public IEnumerator Boot_ImportsTheOldJsonSavesIntoTheDatabase()
        {
            var saves = Directory.CreateDirectory(Path.Combine(directory, SaveDatabase.LegacySaveFolder)).FullName;
            File.WriteAllText(Path.Combine(saves, "slot1.json"), "{\"actFlag\":2,\"location\":\"agora\",\"timestamp\":9}");
            File.WriteAllText(Path.Combine(saves, SaveDatabase.LegacyActiveSlotFileName), "1");

            var services = CreateServices();
            yield return WaitForBoot(services);

            Assert.AreEqual(1, services.ActiveSlot);
            Assert.IsTrue(services.HasContinue);
            Assert.IsTrue(services.TryLoadActiveSave(out var imported));
            Assert.AreEqual("agora", imported.Location);
            Assert.AreEqual(Game.Core.Act.Two, imported.ActFlag);
        }

        [UnityTest]
        public IEnumerator Slots_SelectSaveAndStartNewGamesAcrossThreeSlots()
        {
            var services = CreateServices();
            yield return WaitForBoot(services);
            Assert.AreEqual(Game.Core.SaveSlots.DefaultIndex, services.ActiveSlot);

            services.SaveToSlot(2, new Game.Core.SaveData { Location = "agora", PlaytimeSeconds = 600, Timestamp = 7 });
            Assert.AreEqual(2, services.ActiveSlot);
            Assert.AreEqual(600, services.PlaytimeSeconds);
            Assert.IsTrue(services.HasContinue);
            Assert.IsTrue(services.SlotCatalog.Read(2).IsUsable);
            Assert.IsFalse(services.SlotCatalog.Read(0).Exists);

            services.StartNewGame(1);
            Assert.AreEqual(0, services.PlaytimeSeconds);
            Assert.IsTrue(services.TryLoadActiveSave(out var fresh));
            Assert.AreEqual(string.Empty, fresh.Location);

            Object.DestroyImmediate(host);
            var reopened = CreateServices();
            yield return WaitForBoot(reopened);
            Assert.AreEqual(1, reopened.ActiveSlot);

            reopened.SelectSlot(2);
            Assert.AreEqual(600, reopened.PlaytimeSeconds);
        }

        [UnityTest]
        public IEnumerator Slots_RestoreTheCompendiumOfTheActiveSlot()
        {
            var catalog = CreateCatalog("ad_hominem", "straw_man", "false_dilemma");
            var services = CreateServices(catalog);
            yield return WaitForBoot(services);
            Assert.AreEqual(3, services.Compendium.Total);
            Assert.IsEmpty(services.Compendium.UnlockedIds);

            var saved = new Game.Core.SaveData { Timestamp = 7 };
            saved.Compendium.Add("straw_man");
            services.SaveToSlot(2, saved);
            CollectionAssert.AreEqual(new[] { "straw_man" }, services.Compendium.UnlockedIds);

            services.Compendium.Unlock("ad_hominem");
            services.StartNewGame(1);
            Assert.IsEmpty(services.Compendium.UnlockedIds);

            services.SelectSlot(2);
            CollectionAssert.AreEqual(new[] { "straw_man" }, services.Compendium.UnlockedIds);

            Object.DestroyImmediate(host);
            var reopened = CreateServices(catalog);
            yield return WaitForBoot(reopened);
            CollectionAssert.AreEqual(new[] { "straw_man" }, reopened.Compendium.UnlockedIds);
        }

        private GameServices CreateServices(FallacyCatalogSO catalog = null)
        {
            host = new GameObject(nameof(GameServices));
            host.SetActive(false);
            var services = host.AddComponent<GameServices>();
            services.Configure(catalog, directory);
            host.SetActive(true);
            return services;
        }

        private FallacyCatalogSO CreateCatalog(params string[] ids)
        {
            var entries = ids.Select(id =>
            {
                var fallacy = ScriptableObject.CreateInstance<FallacySO>();
                fallacy.Configure(id, id, id, id, FallacyCategory.Informal, null);
                return fallacy;
            }).ToList();
            var catalog = ScriptableObject.CreateInstance<FallacyCatalogSO>();
            catalog.SetEntries(entries);
            createdAssets.AddRange(entries);
            createdAssets.Add(catalog);
            return catalog;
        }

        private static IEnumerator WaitForBoot(GameServices services)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!services.IsBooted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(services.IsBooted, services.BootTask?.Exception?.ToString() ?? "boot did not finish");
        }
    }
}
