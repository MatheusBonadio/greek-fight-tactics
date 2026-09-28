using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core;
using SQLite;

namespace Game.Net
{
    public sealed class SaveDatabase
    {
        public const string FileName = "gft.db";
        public const int SchemaVersion = 1;
        public const string ActiveSlotKey = "active_slot";
        public const string SettingsKey = "settings";
        public const string LegacySaveFolder = "saves";
        public const string LegacySaveExtension = ".json";
        public const string LegacyActiveSlotFileName = "active_slot.txt";
        public const string LegacySettingsFileName = "settings.json";

        private const string CreateSlotsTable =
            "CREATE TABLE IF NOT EXISTS save_slots (slot INTEGER PRIMARY KEY, payload TEXT NOT NULL, updated_at INTEGER NOT NULL)";

        private const string CreatePreferencesTable =
            "CREATE TABLE IF NOT EXISTS preferences (key TEXT PRIMARY KEY, value TEXT NOT NULL)";

        private const string SelectSlot = "SELECT payload FROM save_slots WHERE slot = ?";
        private const string UpsertSlot =
            "INSERT INTO save_slots (slot, payload, updated_at) VALUES (?, ?, ?) ON CONFLICT(slot) DO UPDATE SET payload = excluded.payload, updated_at = excluded.updated_at";
        private const string DeleteSlotRow = "DELETE FROM save_slots WHERE slot = ?";
        private const string SelectPreference = "SELECT value FROM preferences WHERE key = ?";
        private const string UpsertPreference =
            "INSERT INTO preferences (key, value) VALUES (?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        private const string ReadSchemaVersion = "PRAGMA user_version";

        private readonly string directory;

        public SaveDatabase(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Data directory is required.", nameof(directory));
            }

            this.directory = directory;
        }

        public string FilePath => Path.Combine(directory, FileName);

        public int ReadVersion() => Use(connection => connection.ExecuteScalar<int>(ReadSchemaVersion));

        public bool TryReadSlot(int index, out string payload)
        {
            RequireSlot(index);
            payload = Use(connection => connection.ExecuteScalar<string>(SelectSlot, index));
            return payload != null;
        }

        public IReadOnlyDictionary<int, string> ReadSlots() => Use(connection =>
        {
            var payloads = new Dictionary<int, string>();
            for (var index = 0; index < SaveSlots.Count; index++)
            {
                var payload = connection.ExecuteScalar<string>(SelectSlot, index);
                if (payload != null)
                {
                    payloads[index] = payload;
                }
            }

            return payloads;
        });

        public void WriteSlot(int index, string payload)
        {
            RequireSlot(index);
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            Use(connection => connection.Execute(UpsertSlot, index, payload, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        public bool DeleteSlot(int index)
        {
            RequireSlot(index);
            return Use(connection => connection.Execute(DeleteSlotRow, index)) > 0;
        }

        public bool TryReadPreference(string key, out string value)
        {
            value = Use(connection => connection.ExecuteScalar<string>(SelectPreference, RequireKey(key)));
            return value != null;
        }

        public void WritePreference(string key, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Use(connection => connection.Execute(UpsertPreference, RequireKey(key), value));
        }

        private T Use<T>(Func<SQLiteConnection, T> work)
        {
            Directory.CreateDirectory(directory);
            using var connection = new SQLiteConnection(FilePath);
            Prepare(connection);
            return work(connection);
        }

        private void Prepare(SQLiteConnection connection)
        {
            if (connection.ExecuteScalar<int>(ReadSchemaVersion) >= SchemaVersion)
            {
                return;
            }

            connection.RunInTransaction(() =>
            {
                connection.Execute(CreateSlotsTable);
                connection.Execute(CreatePreferencesTable);
                ImportLegacyFiles(connection);
                connection.Execute($"PRAGMA user_version = {SchemaVersion.ToString(CultureInfo.InvariantCulture)}");
            });
        }

        private void ImportLegacyFiles(SQLiteConnection connection)
        {
            var saves = Path.Combine(directory, LegacySaveFolder);
            for (var index = 0; index < SaveSlots.Count; index++)
            {
                var file = Path.Combine(saves, SaveSlots.NameFor(index) + LegacySaveExtension);
                if (File.Exists(file))
                {
                    var writtenAt = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds();
                    connection.Execute(UpsertSlot, index, File.ReadAllText(file), writtenAt);
                }
            }

            var activeSlot = Path.Combine(saves, LegacyActiveSlotFileName);
            if (File.Exists(activeSlot))
            {
                connection.Execute(UpsertPreference, ActiveSlotKey, File.ReadAllText(activeSlot).Trim());
            }

            var settings = Path.Combine(directory, LegacySettingsFileName);
            if (File.Exists(settings))
            {
                connection.Execute(UpsertPreference, SettingsKey, File.ReadAllText(settings));
            }
        }

        private static void RequireSlot(int index)
        {
            if (!SaveSlots.IsValid(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Save slots go from 0 to {SaveSlots.Count - 1}.");
            }
        }

        private static string RequireKey(string key) =>
            string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Preference key is required.", nameof(key)) : key;
    }
}
