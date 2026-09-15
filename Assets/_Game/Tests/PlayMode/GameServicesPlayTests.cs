using System;
using System.Collections;
using System.IO;
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

            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator OfflineBoot_LoadsTheArgumentCacheAndSkipsOnlineServices()
        {
            var seeded = new ArgumentDiskCache(Path.Combine(directory, GameServices.ArgumentCacheFolder));
            seeded.Put("cached-key", "{\"speech\":\"cached\"}");
            seeded.SaveAsync().Wait();

            var services = CreateServices();
            yield return WaitForBoot(services);

            Assert.AreEqual(1, services.ArgumentCache.Count);
            Assert.IsTrue(services.ArgumentCache.TryGet("cached-key", out _));
            Assert.IsNotNull(services.LocalSaves);
            Assert.IsNull(services.Auth);
            Assert.IsNull(services.Arguments);
            Assert.IsNull(services.Saves);
        }

        [UnityTest]
        public IEnumerator SaveArgumentCache_PersistsNewEntriesForTheNextBoot()
        {
            var services = CreateServices();
            yield return WaitForBoot(services);

            services.ArgumentCache.Put("fresh-key", "{\"speech\":\"fresh\"}");
            var save = services.SaveArgumentCache();
            yield return new WaitUntil(() => save.IsCompleted);

            Assert.IsNull(save.Exception);
            Assert.AreEqual(1, new ArgumentDiskCache(Path.Combine(directory, GameServices.ArgumentCacheFolder)).Load());
        }

        [UnityTest]
        public IEnumerator SandboxScene_BootsServicesOfflineAndServesFallbackArguments()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("GridSandbox", UnityEngine.SceneManagement.LoadSceneMode.Single);
            yield return null;

            var services = Object.FindAnyObjectByType<GameServices>();
            Assert.IsNotNull(services, "GridSandbox scene must contain GameServices");
            yield return WaitForBoot(services);

            var source = services.CreateArgumentSource(402);
            Assert.AreEqual(services.Arguments != null, source.IsOnline);
            source.Register(7, new OpponentContext("thrasybios", "Thrasybios", Game.Core.Profile.Sophist, Game.Core.Act.One, new[] { "ad_hominem", "false_dilemma" }));

            var debater = new Game.Battle.Debater(7, Game.Battle.Side.Opponent, Game.Core.Profile.Sophist, 50, 5, new Game.Core.Coord(1, 1), Game.Grid.Facing.SouthWest);
            Assert.IsTrue(source.TryNext(debater, out var argument));
            Assert.IsNotEmpty(argument.Speech);
        }

        [UnityTest]
        public IEnumerator Settings_LoadOnBootApplyTheVolumeAndPersistChanges()
        {
            new LocalSettingsStore(directory).Save(new Game.Core.GameSettings { MasterVolume = 0.3f, AssistedMode = true });

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
            Assert.AreEqual(Game.Core.TextSpeed.Fast, new LocalSettingsStore(directory).Load().TextSpeed);
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
        }

        private GameServices CreateServices()
        {
            host = new GameObject(nameof(GameServices));
            host.SetActive(false);
            var services = host.AddComponent<GameServices>();
            services.Configure(null, null, null, directory);
            host.SetActive(true);
            return services;
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
