using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Net
{
    public readonly struct SaveSlotSummary
    {
        public readonly int Index;
        public readonly bool Exists;
        public readonly bool IsCorrupt;
        public readonly Act Act;
        public readonly string Location;
        public readonly long PlaytimeSeconds;
        public readonly long Timestamp;

        private SaveSlotSummary(int index, bool exists, bool isCorrupt, Act act, string location, long playtimeSeconds, long timestamp)
        {
            Index = index;
            Exists = exists;
            IsCorrupt = isCorrupt;
            Act = act;
            Location = location;
            PlaytimeSeconds = playtimeSeconds;
            Timestamp = timestamp;
        }

        public bool IsUsable => Exists && !IsCorrupt;

        public static SaveSlotSummary Empty(int index) => new(index, false, false, Act.One, string.Empty, 0, 0);

        public static SaveSlotSummary Corrupt(int index) => new(index, true, true, Act.One, string.Empty, 0, 0);

        public static SaveSlotSummary From(int index, SaveData save) =>
            new(index, true, false, save.ActFlag, save.Location ?? string.Empty, save.PlaytimeSeconds, save.Timestamp);
    }

    public sealed class SaveSlotCatalog
    {
        public const string ActiveSlotFileName = "active_slot.txt";

        private readonly string directory;

        public SaveSlotCatalog(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Save directory is required.", nameof(directory));
            }

            this.directory = directory;
        }

        public string ActiveSlotPath => Path.Combine(directory, ActiveSlotFileName);

        public LocalSaveStore StoreFor(int index) => new(directory, SaveSlots.NameFor(index));

        public IReadOnlyList<SaveSlotSummary> ReadAll() => Enumerable.Range(0, SaveSlots.Count).Select(Read).ToList();

        public SaveSlotSummary Read(int index)
        {
            var store = StoreFor(index);
            if (!store.Exists)
            {
                return SaveSlotSummary.Empty(index);
            }

            return store.TryLoad(out var save, out _) ? SaveSlotSummary.From(index, save) : SaveSlotSummary.Corrupt(index);
        }

        public bool AnyUsable() => ReadAll().Any(summary => summary.IsUsable);

        public int ReadActiveSlot()
        {
            if (!File.Exists(ActiveSlotPath))
            {
                return SaveSlots.DefaultIndex;
            }

            return int.TryParse(File.ReadAllText(ActiveSlotPath).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && SaveSlots.IsValid(index)
                ? index
                : SaveSlots.DefaultIndex;
        }

        public void WriteActiveSlot(int index)
        {
            if (!SaveSlots.IsValid(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown save slot.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(ActiveSlotPath, index.ToString(CultureInfo.InvariantCulture));
        }
    }
}
