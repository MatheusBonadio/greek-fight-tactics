using Game.Core;

namespace Game.Battle
{
    public static class DuelVictoryRule
    {
        public const DuelVictory WithoutALandedBlow = DuelVictory.Concede;

        public static bool TryClassify(BattleState state, out DuelVictory victory)
        {
            victory = WithoutALandedBlow;
            if (state == null || state.Status != BattleStatus.PlayerWon)
            {
                return false;
            }

            var playerId = state.Player.Id;
            for (var index = state.Log.Count - 1; index >= 0; index--)
            {
                var entry = state.Log[index];
                if (entry.ActorId != playerId || !entry.Hit)
                {
                    continue;
                }

                if (entry.Action == ActionId.Refute)
                {
                    victory = DuelVictory.Refute;
                    return true;
                }

                if (entry.Action == ActionId.Maieutics)
                {
                    victory = DuelVictory.Maieutics;
                    return true;
                }
            }

            return true;
        }
    }
}
