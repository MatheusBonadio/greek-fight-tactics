using System;
using System.Globalization;

namespace Game.Core
{
    public static class StoryStamina
    {
        public const string LostFlag = "coherence_lost";
        public const int Bruised = 20;
        public const int WithdrawToll = 10;

        public static int LostOf(SaveData save) =>
            save != null
            && save.Flags.TryGetValue(LostFlag, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lost)
            && lost > 0
                ? lost
                : 0;

        public static bool IsRested(SaveData save) => LostOf(save) == 0;

        public static int Carried(SaveData save, int full) => Math.Clamp(full - LostOf(save), Math.Min(Bruised, full), full);

        public static int Left(string result, int remaining, int full)
        {
            var standing = result switch
            {
                DuelStoryFlags.Lost => Math.Min(Bruised, full),
                DuelStoryFlags.Withdrew => remaining - WithdrawToll,
                _ => remaining
            };

            return Math.Clamp(standing, Math.Min(Bruised, full), full);
        }

        public static StoryEffect Toll(string result, int remaining, int full) =>
            new(LostFlag, (full - Left(result, remaining, full)).ToString(CultureInfo.InvariantCulture));

        public static StoryEffect Rested() => new(LostFlag, "0");

        public static void Rest(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            save.Flags.Remove(LostFlag);
        }
    }
}
