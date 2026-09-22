using System;
using System.Globalization;

namespace Game.Core
{
    public static class ReputationRules
    {
        public const int WinGain = 5;
        public const int LossPenalty = 10;
        public const int WithdrawPenalty = 10;
        public const int Lowest = 0;
        public const int Highest = 100;

        public static int Change(string result) => result switch
        {
            DuelStoryFlags.Won => WinGain,
            DuelStoryFlags.Lost => -LossPenalty,
            DuelStoryFlags.Withdrew => -WithdrawPenalty,
            _ => 0
        };

        public static int After(SaveData save, string result) =>
            Math.Clamp((save?.Reputation ?? Lowest) + Change(result), Lowest, Highest);

        public static StoryEffect Toll(SaveData save, string result) =>
            new(SaveFlags.Reputation, After(save, result).ToString(CultureInfo.InvariantCulture));
    }
}
