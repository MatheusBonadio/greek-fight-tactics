using System;
using System.IO;
using Game.Core;

namespace Game.Net
{
    public sealed class LocalSaveStore
    {
        public const string DefaultSlot = "slot0";
        public const string FileExtension = ".json";

        private readonly string directory;

        public LocalSaveStore(string directory, string slot = DefaultSlot)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Save directory is required.", nameof(directory));
            }

            if (string.IsNullOrWhiteSpace(slot) || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"Invalid save slot '{slot}'.", nameof(slot));
            }

            this.directory = directory;
            Slot = slot;
        }

        public string Slot { get; }

        public string FilePath => Path.Combine(directory, Slot + FileExtension);

        public bool Exists => File.Exists(FilePath);

        public void Save(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            Directory.CreateDirectory(directory);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, SaveJson.Serialize(save));
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            File.Move(temporary, FilePath);
        }

        public bool TryLoad(out SaveData save, out string error)
        {
            save = null;
            if (!Exists)
            {
                error = $"no save at {FilePath}";
                return false;
            }

            return SaveJson.TryDeserialize(File.ReadAllText(FilePath), out save, out error);
        }

        public bool Delete()
        {
            if (!Exists)
            {
                return false;
            }

            File.Delete(FilePath);
            return true;
        }
    }
}
