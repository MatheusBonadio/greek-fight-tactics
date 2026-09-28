using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly SaveDatabase database;

        public SaveSlotCatalog(SaveDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public LocalSaveStore StoreFor(int index) => new(database, index);

        public IReadOnlyList<SaveSlotSummary> ReadAll()
        {
            var payloads = database.ReadSlots();
            return Enumerable.Range(0, SaveSlots.Count)
                .Select(index => Summarize(index, payloads.TryGetValue(index, out var payload) ? payload : null))
                .ToList();
        }

        public SaveSlotSummary Read(int index) => Summarize(index, database.TryReadSlot(index, out var payload) ? payload : null);

        public bool AnyUsable() => ReadAll().Any(summary => summary.IsUsable);

        public int ReadActiveSlot() =>
            database.TryReadPreference(SaveDatabase.ActiveSlotKey, out var value) &&
            int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
            SaveSlots.IsValid(index)
                ? index
                : SaveSlots.DefaultIndex;

        public void WriteActiveSlot(int index)
        {
            if (!SaveSlots.IsValid(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown save slot.");
            }

            database.WritePreference(SaveDatabase.ActiveSlotKey, index.ToString(CultureInfo.InvariantCulture));
        }

        private static SaveSlotSummary Summarize(int index, string payload)
        {
            if (payload == null)
            {
                return SaveSlotSummary.Empty(index);
            }

            return SaveJson.TryDeserialize(payload, out var save, out _) ? SaveSlotSummary.From(index, save) : SaveSlotSummary.Corrupt(index);
        }
    }
}
