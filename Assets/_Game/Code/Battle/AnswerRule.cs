using Game.Core;

namespace Game.Battle
{
    public static class AnswerRule
    {
        public static bool AsksForAQuestion(in Argument argument, BattleState state) =>
            state.Rules.Phases == null && argument.HasWeakPremise;

        public static ActionId? Expected(BattleState state)
        {
            if (!state.HasOpenArgument)
            {
                return null;
            }

            if (state.Rules.Phases is { } duel)
            {
                return duel.PhaseFor(state.Opponent.Coherence).Counter;
            }

            return state.CurrentArgument.Value.HasWeakPremise ? ActionId.Maieutics : ActionId.Refute;
        }
    }
}
