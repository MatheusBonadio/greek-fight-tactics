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

        public long Timestamp { get; set; }

        public Dictionary<string, DuelRecord> Duels { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Flags { get; } = new(StringComparer.Ordinal);
    }
}
