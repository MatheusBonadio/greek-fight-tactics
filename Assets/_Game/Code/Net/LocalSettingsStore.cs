using System;
using System.IO;
using Game.Core;

namespace Game.Net
{
    public sealed class LocalSettingsStore
    {
        public const string FileName = "settings.json";

        private readonly string directory;

        public LocalSettingsStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Settings directory is required.", nameof(directory));
            }

            this.directory = directory;
        }

        public string FilePath => Path.Combine(directory, FileName);

        public GameSettings Load() => File.Exists(FilePath) ? SettingsJson.Deserialize(File.ReadAllText(FilePath)) : new GameSettings();

        public void Save(GameSettings settings)
        {
            var json = SettingsJson.Serialize(settings);
            Directory.CreateDirectory(directory);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            File.Move(temporary, FilePath);
        }
    }
}
