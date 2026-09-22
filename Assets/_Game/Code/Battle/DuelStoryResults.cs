using System;
using Game.Core;

namespace Game.Battle
{
    public static class DuelStoryResults
    {
        public static string ValueFor(BattleStatus status) => status switch
        {
            BattleStatus.PlayerWon => DuelStoryFlags.Won,
            BattleStatus.PlayerLost => DuelStoryFlags.Lost,
            BattleStatus.PlayerWithdrew => DuelStoryFlags.Withdrew,
            BattleStatus.TurnLimitReached => DuelStoryFlags.Stalemate,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "An ongoing battle has no result yet.")
        };

        public static StoryEffect ResultOf(string opponentId, BattleStatus status) => new(DuelStoryFlags.FlagFor(opponentId), ValueFor(status));
    }
}
