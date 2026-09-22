using System.Linq;

namespace Game.Battle
{
    public static class VictoryCheck
    {
        public static BattleStatus Evaluate(BattleState state)
        {
            if (state.Status == BattleStatus.PlayerWithdrew)
            {
                return BattleStatus.PlayerWithdrew;
            }

            if (state.Debaters.Where(debater => debater.Side == Side.Opponent).All(debater => debater.IsDefeated))
            {
                return BattleStatus.PlayerWon;
            }

            if (state.Rules.AudienceVictory && state.PlayerAudience >= BattleRules.MaxAudience)
            {
                return BattleStatus.PlayerWon;
            }

            if (state.Player.IsDefeated)
            {
                return BattleStatus.PlayerLost;
            }

            return state.Turn >= state.Rules.TurnLimit ? BattleStatus.TurnLimitReached : BattleStatus.Ongoing;
        }
    }
}
