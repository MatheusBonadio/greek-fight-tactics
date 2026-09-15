using System;
using System.IO;
using Game.Core;
using Game.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.EditMode.Net
{
    public sealed class SettingsTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-settings-{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Volume_MovesInTenPercentStepsAndStaysInRange()
        {
            var settings = new GameSettings();
            Assert.AreEqual(GameSettings.DefaultVolume, settings.MasterVolume, 0.0001f);

            settings.AdjustVolume(1);
            Assert.AreEqual(0.9f, settings.MasterVolume, 0.0001f);
            settings.AdjustVolume(5);
            Assert.AreEqual(1f, settings.MasterVolume);
            settings.AdjustVolume(-20);
            Assert.AreEqual(0f, settings.MasterVolume);

            settings.MasterVolume = 0.34f;
            Assert.AreEqual(3, settings.VolumeStep);
        }

        [Test]
        public void TextSpeed_CyclesBothWaysAndMapsToTypewriterSpeed()
        {
            var settings = new GameSettings();
            Assert.AreEqual(GameSettings.NormalCharactersPerSecond, settings.CharactersPerSecond);

            settings.CycleTextSpeed(1);
            Assert.AreEqual(TextSpeed.Fast, settings.TextSpeed);
            Assert.AreEqual(GameSettings.FastCharactersPerSecond, settings.CharactersPerSecond);

            settings.CycleTextSpeed(1);
            Assert.AreEqual(TextSpeed.Slow, settings.TextSpeed);
            settings.CycleTextSpeed(-1);
            Assert.AreEqual(TextSpeed.Fast, settings.TextSpeed);
        }

        [Test]
        public void Json_RoundTripsEveryOption()
        {
            var settings = new GameSettings { MasterVolume = 0.3f, TextSpeed = TextSpeed.Slow, AssistedMode = true };

            var json = JObject.Parse(SettingsJson.Serialize(settings));
            Assert.AreEqual("slow", (string)json[SettingsJson.TextSpeedField]);

            var restored = SettingsJson.Deserialize(json.ToString());
            Assert.AreEqual(0.3f, restored.MasterVolume, 0.0001f);
            Assert.AreEqual(TextSpeed.Slow, restored.TextSpeed);
            Assert.IsTrue(restored.AssistedMode);
        }

        [TestCase("not json")]
        [TestCase("{\"master_volume\":\"loud\",\"text_speed\":\"warp\",\"assisted_mode\":\"yes\"}")]
        [TestCase("{\"text_speed\":7}")]
        public void Json_FallsBackToDefaultsForInvalidFields(string json)
        {
            var settings = SettingsJson.Deserialize(json);

            Assert.AreEqual(GameSettings.DefaultVolume, settings.MasterVolume, 0.0001f);
            Assert.AreEqual(TextSpeed.Normal, settings.TextSpeed);
            Assert.IsFalse(settings.AssistedMode);
        }

        [Test]
        public void Store_ReturnsDefaultsWithoutAFileAndPersistsChanges()
        {
            var store = new LocalSettingsStore(directory);
            Assert.AreEqual(TextSpeed.Normal, store.Load().TextSpeed);

            store.Save(new GameSettings { TextSpeed = TextSpeed.Fast, AssistedMode = true });

            var loaded = new LocalSettingsStore(directory).Load();
            Assert.AreEqual(TextSpeed.Fast, loaded.TextSpeed);
            Assert.IsTrue(loaded.AssistedMode);
        }

        [TestCase(GameMode.BattleOutro, GameMode.Exploration, true)]
        [TestCase(GameMode.Dialogue, GameMode.Exploration, true)]
        [TestCase(GameMode.Exploration, GameMode.BattleIntro, false)]
        [TestCase(GameMode.Paused, GameMode.Exploration, false)]
        [TestCase(GameMode.BattleTurn, GameMode.BattleResolve, false)]
        public void Autosave_HappensWhenReturningToExplorationAfterBattleOrDialogue(GameMode from, GameMode to, bool expected)
        {
            Assert.AreEqual(expected, AutosavePolicy.ShouldSave(from, to));
        }
    }
}
