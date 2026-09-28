using System;
using Game.Core;

namespace Game.Battle
{
    public static class MoraleRule
    {
        public const int MinimumBlow = 1;

        public static int ShiftFor(BattleState state, Debater striker)
        {
            var rules = state.Rules;
            var lean = state.AudienceFor(striker.Side) * 2 - BattleRules.MaxAudience;
            if (rules.MoraleDamageStep <= 0 || lean == 0)
            {
                return 0;
            }

            var crowd = PositionalModifiers.CrowdWithin(state, striker.Position, rules.MoraleRadius);
            var shift = Math.Min(crowd * rules.MoraleDamageStep, rules.MaxMoraleShift);
            return lean > 0 ? shift : -shift;
        }

        public static void Apply(Outcome outcome, Debater actor, BattleState state)
        {
            if (outcome.IsRejected)
            {
                return;
            }

            if (outcome.TargetCoherenceDelta < 0 && (outcome.Hit || outcome.Action == ActionId.Argue))
            {
                outcome.MoraleShift = ShiftFor(state, actor);
                outcome.TargetCoherenceDelta = -Blow(-outcome.TargetCoherenceDelta, outcome.MoraleShift);
                return;
            }

            if (outcome.Counterattack && outcome.ActorCoherenceDelta < 0 && state.Find(outcome.TargetId) is { } striker)
            {
                outcome.MoraleShift = ShiftFor(state, striker);
                outcome.ActorCoherenceDelta = -Blow(-outcome.ActorCoherenceDelta, outcome.MoraleShift);
            }
        }

        private static int Blow(int damage, int shift) => Math.Max(MinimumBlow, damage + shift);
    }
}
