using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class SaveData
    {
        public Act ActFlag { get; set; } = Act.One;

        public byte[] FogBitmask { get; set; } = Array.Empty<byte>();

        public DamarisChoice DamarisBranch { get; set; } = DamarisChoice.Undecided;

        public int Reputation { get; set; }

        public Dictionary<string, int> Inventory { get; } = new(StringComparer.Ordinal);

        public Coord Position { get; set; }

        public string Location { get; set; } = string.Empty;

        public long PlaytimeSeconds { get; set; }

        public long Timestamp { get; set; }

        public Dictionary<string, DuelRecord> Duels { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Flags { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, byte[]> FogByLocation { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Compendium { get; } = new(StringComparer.Ordinal);

        public bool TryGetFog(string locationId, out byte[] bitmask)
        {
            if (!string.IsNullOrEmpty(locationId) && FogByLocation.TryGetValue(locationId, out bitmask))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(locationId) && string.Equals(Location, locationId, StringComparison.Ordinal) && FogBitmask.Length > 0)
            {
                bitmask = FogBitmask;
                return true;
            }

            bitmask = Array.Empty<byte>();
            return false;
        }
    }
}
