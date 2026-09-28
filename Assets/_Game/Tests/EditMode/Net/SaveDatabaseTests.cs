using System;
using System.IO;
using Game.Core;
using Game.Net;
using NUnit.Framework;
using SQLite;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class SaveDatabaseTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-database-{Guid.NewGuid():N}");
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
        public void TheFirstUse_CreatesTheDatabaseAtTheCurrentSchema()
        {
            var database = new SaveDatabase(directory);

            Assert.AreEqual(SaveDatabase.SchemaVersion, database.ReadVersion());
            Assert.IsTrue(File.Exists(Path.Combine(directory, SaveDatabase.FileName)));
            Assert.IsFalse(database.TryReadSlot(0, out _));
            Assert.IsFalse(database.TryReadPreference(SaveDatabase.SettingsKey, out _));
        }

        [Test]
        public void ASave_IsOneRowOfTheSlotsTable()
        {
            var store = new LocalSaveStore(new SaveDatabase(directory), 1);
            store.Save(SampleSave(timestamp: 77));

            using var connection = new SQLiteConnection(Path.Combine(directory, SaveDatabase.FileName));
            Assert.AreEqual(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM save_slots"));
            var payload = connection.ExecuteScalar<string>("SELECT payload FROM save_slots WHERE slot = 1");
            Assert.IsTrue(SaveJson.TryDeserialize(payload, out var stored, out var error), error);
            Assert.AreEqual(77, stored.Timestamp);
            Assert.Greater(connection.ExecuteScalar<long>("SELECT updated_at FROM save_slots WHERE slot = 1"), 0);
        }

        [Test]
        public void WritingASlotTwice_ReplacesItAndLeavesTheOthersAlone()
        {
            var database = new SaveDatabase(directory);
            database.WriteSlot(0, "first");
            database.WriteSlot(2, "other");
            database.WriteSlot(0, "second");

            Assert.IsTrue(database.TryReadSlot(0, out var slot0));
            Assert.AreEqual("second", slot0);
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, database.ReadSlots().Keys);
            Assert.IsTrue(database.DeleteSlot(2));
            Assert.IsFalse(database.DeleteSlot(2));
            CollectionAssert.AreEquivalent(new[] { 0 }, database.ReadSlots().Keys);
        }

        [Test]
        public void TheDatabaseFile_IsNeverLeftOpen()
        {
            var database = new SaveDatabase(directory);
            new LocalSaveStore(database).Save(SampleSave());
            new SaveSlotCatalog(database).WriteActiveSlot(1);
            new LocalSettingsStore(database).Save(new GameSettings { AssistedMode = true });

            Assert.DoesNotThrow(() => Directory.Delete(directory, true), "a connection kept the file locked after the write");
        }

        [Test]
        public void OldJsonSaves_AreImportedOnceAndKeptAsABackup()
        {
            var saves = Directory.CreateDirectory(Path.Combine(directory, SaveDatabase.LegacySaveFolder)).FullName;
            File.WriteAllText(Path.Combine(saves, "slot0.json"), SaveJson.Serialize(SampleSave(timestamp: 10)));
            File.WriteAllText(Path.Combine(saves, "slot2.json"), "{ not json");
            File.WriteAllText(Path.Combine(saves, SaveDatabase.LegacyActiveSlotFileName), "2");
            File.WriteAllText(Path.Combine(directory, SaveDatabase.LegacySettingsFileName), "{\"master_volume\":0.3,\"text_speed\":\"fast\"}");

            var database = new SaveDatabase(directory);
            var catalog = new SaveSlotCatalog(database);
            var slots = catalog.ReadAll();

            Assert.IsTrue(slots[0].IsUsable);
            Assert.AreEqual(10, slots[0].Timestamp);
            Assert.IsFalse(slots[1].Exists);
            Assert.IsTrue(slots[2].IsCorrupt, "an unreadable old save stays unreadable instead of vanishing");
            Assert.AreEqual(2, catalog.ReadActiveSlot());
            var settings = new LocalSettingsStore(database).Load();
            Assert.AreEqual(0.3f, settings.MasterVolume, 0.0001f);
            Assert.AreEqual(TextSpeed.Fast, settings.TextSpeed);
            Assert.IsTrue(File.Exists(Path.Combine(saves, "slot0.json")), "the old file stays on disk as a backup");

            File.WriteAllText(Path.Combine(saves, "slot0.json"), SaveJson.Serialize(SampleSave(timestamp: 99)));
            File.WriteAllText(Path.Combine(saves, "slot1.json"), SaveJson.Serialize(SampleSave(timestamp: 5)));

            var reopened = new SaveSlotCatalog(new SaveDatabase(directory)).ReadAll();
            Assert.AreEqual(10, reopened[0].Timestamp, "the import runs once, so an old file never overwrites newer progress");
            Assert.IsFalse(reopened[1].Exists);
        }

        [Test]
        public void SlotsAndKeys_OutsideTheCatalogAreRefused()
        {
            var database = new SaveDatabase(directory);

            Assert.Throws<ArgumentOutOfRangeException>(() => database.WriteSlot(SaveSlots.Count, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => database.TryReadSlot(-1, out _));
            Assert.Throws<ArgumentException>(() => database.WritePreference(" ", "x"));
            Assert.Throws<ArgumentNullException>(() => database.WriteSlot(0, null));
            Assert.Throws<ArgumentException>(() => new SaveDatabase(""));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalSaveStore(database, SaveSlots.Count));
        }
    }
}
