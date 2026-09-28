using System;
using Game.Core;

namespace Game.Net
{
    public sealed class LocalSettingsStore
    {
        private readonly SaveDatabase database;

        public LocalSettingsStore(SaveDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public GameSettings Load() =>
            database.TryReadPreference(SaveDatabase.SettingsKey, out var json) ? SettingsJson.Deserialize(json) : new GameSettings();

        public void Save(GameSettings settings) => database.WritePreference(SaveDatabase.SettingsKey, SettingsJson.Serialize(settings));
    }
}
