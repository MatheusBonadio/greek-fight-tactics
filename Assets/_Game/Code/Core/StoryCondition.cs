using System;
using System.Collections.Generic;

namespace Game.Core
{
    public readonly struct StoryCondition
    {
        public readonly string Flag;
        public readonly string Value;
        public readonly bool Absent;

        public StoryCondition(string flag, string value = null, bool absent = false)
        {
            Flag = flag;
            Value = string.IsNullOrEmpty(value) ? null : value;
            Absent = absent;
        }

        public static StoryCondition Has(string flag) => new(flag);

        public static StoryCondition Lacks(string flag) => new(flag, null, true);

        public static StoryCondition Is(string flag, string value) => new(flag, value);

        public bool IsValid => SaveFlags.IsValidName(Flag) && !(Absent && (Value != null || SaveFlags.IsKnownName(Flag)));

        public bool IsMet(SaveData save)
        {
            var present = SaveFlags.TryGet(save, Flag, out var actual) && !string.IsNullOrEmpty(actual);
            if (Absent)
            {
                return !present;
            }

            return present && (Value == null || string.Equals(actual, Value, StringComparison.Ordinal));
        }

        public static bool AllMet(IEnumerable<StoryCondition> conditions, SaveData save)
        {
            if (conditions == null)
            {
                return true;
            }

            foreach (var condition in conditions)
            {
                if (!condition.IsMet(save))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString() => Absent ? $"!{Flag}" : Value == null ? Flag : $"{Flag}={Value}";
    }
}
