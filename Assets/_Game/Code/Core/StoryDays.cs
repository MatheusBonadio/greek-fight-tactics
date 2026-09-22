using System;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    public static class StoryDays
    {
        public const string DayFlag = "day";
        public const string AbsenceSuffix = "_absent_days";
        public const int FirstDay = 1;

        public static int DayOf(SaveData save) =>
            save != null && save.Flags.TryGetValue(DayFlag, out var value) && TryReadCount(value, out var day) && day >= FirstDay ? day : FirstDay;

        public static bool IsAbsenceFlag(string flag) =>
            flag != null && flag.Length > AbsenceSuffix.Length && flag.EndsWith(AbsenceSuffix, StringComparison.Ordinal);

        public static int AbsenceLeft(SaveData save, string flag) =>
            save != null && IsAbsenceFlag(flag) && save.Flags.TryGetValue(flag, out var value) && TryReadCount(value, out var days) ? Math.Max(0, days) : 0;

        public static void Pass(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            save.Flags[DayFlag] = Format(DayOf(save) + 1);
            foreach (var flag in save.Flags.Keys.Where(IsAbsenceFlag).ToList())
            {
                var left = AbsenceLeft(save, flag) - 1;
                if (left > 0)
                {
                    save.Flags[flag] = Format(left);
                }
                else
                {
                    save.Flags.Remove(flag);
                }
            }
        }

        private static bool TryReadCount(string value, out int count) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);

        private static string Format(int count) => count.ToString(CultureInfo.InvariantCulture);
    }
}
