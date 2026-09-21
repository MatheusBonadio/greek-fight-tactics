using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class DuelProgress
    {
        private readonly Dictionary<string, DuelRecord> records = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, DuelRecord> Records => records;

        public bool HasWon(string opponentId) =>
            opponentId != null && records.TryGetValue(opponentId, out var record) && record.Won;

        public void RecordVictory(string opponentId, DuelVictory victory)
        {
            if (string.IsNullOrWhiteSpace(opponentId))
            {
                throw new ArgumentException("Opponent id is required.", nameof(opponentId));
            }

            if (!HasWon(opponentId))
            {
                records[opponentId] = new DuelRecord(true, victory);
            }
        }

        public void Load(IReadOnlyDictionary<string, DuelRecord> saved)
        {
            if (saved == null)
            {
                return;
            }

            foreach (var pair in saved)
            {
                if (pair.Value != null && !HasWon(pair.Key))
                {
                    records[pair.Key] = pair.Value;
                }
            }
        }

        public void WriteTo(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            foreach (var pair in records)
            {
                if (pair.Value.Won || !save.Duels.ContainsKey(pair.Key))
                {
                    save.Duels[pair.Key] = pair.Value;
                }
            }
        }
    }
}
