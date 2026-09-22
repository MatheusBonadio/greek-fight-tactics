using System;

namespace Game.Core
{
    public static class DuelStoryFlags
    {
        public const string Prefix = "duel_";
        public const string Won = "won";
        public const string Lost = "lost";
        public const string Withdrew = "withdrew";
        public const string Stalemate = "stalemate";

        public static string FlagFor(string opponentId)
        {
            if (string.IsNullOrWhiteSpace(opponentId))
            {
                throw new ArgumentException("Opponent id is required.", nameof(opponentId));
            }

            return Prefix + opponentId;
        }

        public static bool TryReadRecordedVictory(SaveData save, string flag, out string value)
        {
            value = null;
            if (save == null || flag == null || !flag.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (!save.Duels.TryGetValue(flag.Substring(Prefix.Length), out var record) || record == null || !record.Won)
            {
                return false;
            }

            value = Won;
            return true;
        }
    }
}
