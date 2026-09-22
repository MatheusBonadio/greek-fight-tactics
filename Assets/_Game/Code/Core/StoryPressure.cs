using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class StoryPressure
    {
        public const string RumorFlag = "pressure_rumor";
        public const string DesertedFlag = "pressure_deserted";
        public const string ToolsFlag = "pressure_tools";
        public const int RumorDuels = 2;
        public const int DesertedDuels = 3;
        public const int ToolsDuels = 4;
        public const string ReachedValue = "true";

        public static readonly IReadOnlyList<PressureStage> Stages = new[]
        {
            new PressureStage(RumorFlag, RumorDuels),
            new PressureStage(DesertedFlag, DesertedDuels),
            new PressureStage(ToolsFlag, ToolsDuels)
        };

        public static bool IsStageFlag(string flag)
        {
            foreach (var stage in Stages)
            {
                if (stage.Flag == flag)
                {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyCollection<string> ConcludedDuels(SaveData save)
        {
            var opponents = new HashSet<string>(StringComparer.Ordinal);
            if (save == null)
            {
                return opponents;
            }

            foreach (var pair in save.Flags)
            {
                if (pair.Key.Length > DuelStoryFlags.Prefix.Length && pair.Key.StartsWith(DuelStoryFlags.Prefix, StringComparison.Ordinal) && IsConcludedResult(pair.Value))
                {
                    opponents.Add(pair.Key.Substring(DuelStoryFlags.Prefix.Length));
                }
            }

            foreach (var pair in save.Duels)
            {
                if (pair.Value != null && pair.Value.Won)
                {
                    opponents.Add(pair.Key);
                }
            }

            return opponents;
        }

        public static int Level(SaveData save)
        {
            var concluded = ConcludedDuels(save).Count;
            var level = 0;
            foreach (var stage in Stages)
            {
                if (concluded >= stage.ConcludedDuels)
                {
                    level++;
                }
            }

            return level;
        }

        public static bool TryRead(SaveData save, string flag, out string value)
        {
            value = null;
            if (save == null || !IsStageFlag(flag))
            {
                return false;
            }

            var concluded = ConcludedDuels(save).Count;
            foreach (var stage in Stages)
            {
                if (stage.Flag == flag && concluded >= stage.ConcludedDuels)
                {
                    value = ReachedValue;
                    return true;
                }
            }

            return false;
        }

        private static bool IsConcludedResult(string value) =>
            value is DuelStoryFlags.Won or DuelStoryFlags.Lost or DuelStoryFlags.Withdrew or DuelStoryFlags.Stalemate;
    }

    public readonly struct PressureStage
    {
        public readonly string Flag;
        public readonly int ConcludedDuels;

        public PressureStage(string flag, int concludedDuels)
        {
            Flag = flag;
            ConcludedDuels = concludedDuels;
        }
    }
}
