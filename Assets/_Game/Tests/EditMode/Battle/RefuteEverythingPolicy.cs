using Game.Battle;

namespace Game.Tests.EditMode.Battle
{
    internal sealed class RefuteEverythingPolicy : IPlayerPolicy
    {
        public Play Decide(BattleState state, Debater player) =>
            state.CurrentArgument.HasValue
                ? Play.Refute(player.Id, state.Opponent.Id, state.CurrentArgument.Value.FallacyId)
                : Play.Concede(player.Id);
    }
}
