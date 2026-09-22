using System;
using Game.Core;

namespace Game.Battle
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

        public static string ValueFor(BattleStatus status) => status switch
        {
            BattleStatus.PlayerWon => Won,
            BattleStatus.PlayerLost => Lost,
            BattleStatus.PlayerWithdrew => Withdrew,
            BattleStatus.TurnLimitReached => Stalemate,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "An ongoing battle has no result yet.")
        };

        public static StoryEffect ResultOf(string opponentId, BattleStatus status) => new(FlagFor(opponentId), ValueFor(status));
    }
}
