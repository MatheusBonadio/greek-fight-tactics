using System;
using Game.Core;

namespace Game.Net
{
    public sealed class LocalSaveStore
    {
        private readonly SaveDatabase database;

        public LocalSaveStore(SaveDatabase database, int slot = SaveSlots.DefaultIndex)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            Slot = SaveSlots.IsValid(slot)
                ? slot
                : throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Save slots go from 0 to {SaveSlots.Count - 1}.");
        }

        public int Slot { get; }

        public string DatabasePath => database.FilePath;

        public bool Exists => database.TryReadSlot(Slot, out _);

        public void Save(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            database.WriteSlot(Slot, SaveJson.Serialize(save));
        }

        public bool TryLoad(out SaveData save, out string error)
        {
            save = null;
            if (!database.TryReadSlot(Slot, out var payload))
            {
                error = $"no save in slot {Slot} of {database.FilePath}";
                return false;
            }

            return SaveJson.TryDeserialize(payload, out save, out error);
        }

        public bool Delete() => database.DeleteSlot(Slot);
    }
}
