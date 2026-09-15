using System;

namespace Game.Core
{
    public static class SaveSlots
    {
        public const int Count = 3;
        public const int DefaultIndex = 0;
        public const string SlotPrefix = "slot";

        public static bool IsValid(int index) => index >= 0 && index < Count;

        public static string NameFor(int index) =>
            IsValid(index) ? SlotPrefix + index : throw new ArgumentOutOfRangeException(nameof(index), index, $"Save slots go from 0 to {Count - 1}.");
    }
}
