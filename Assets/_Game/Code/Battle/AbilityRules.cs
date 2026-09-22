using System;
using Game.Core;

namespace Game.Battle
{
    public static class AbilityRules
    {
        public static int UsesPerBattle(AbilityId ability, BattleRules rules) => ability switch
        {
            AbilityId.Inversion => rules.InversionUsesPerBattle,
            _ => rules.ListeningUsesPerBattle
        };

        public static int InvertedAudienceGain(Outcome outcome) =>
            Math.Max(0, -outcome.PlayerAudienceDelta) + Math.Max(0, -outcome.TargetCoherenceDelta);

        public static void Apply(Outcome outcome, Debater actor, BattleState state)
        {
            if (outcome.IsRejected || outcome.Action != ActionId.Argue || actor.Side != Side.Opponent)
            {
                return;
            }

            if (TryInvert(outcome, state))
            {
                return;
            }

            if (state.CanUse(AbilityId.Listening) && outcome.PresentedArgument.HasValue && outcome.PresentedArgument.Value.Premises.Count > 0)
            {
                outcome.TriggeredAbility = AbilityId.Listening;
            }
        }

        private static bool TryInvert(Outcome outcome, BattleState state)
        {
            var gain = InvertedAudienceGain(outcome);
            if (gain == 0 || !state.CanUse(AbilityId.Inversion))
            {
                return false;
            }

            outcome.PlayerAudienceDelta = gain;
            outcome.TargetCoherenceDelta = 0;
            outcome.TriggeredAbility = AbilityId.Inversion;
            return true;
        }
    }
}
