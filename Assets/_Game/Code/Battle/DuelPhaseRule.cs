using System;
using Game.Core;

namespace Game.Battle
{
    public static class DuelPhaseRule
    {
        public static Outcome Apply(Outcome outcome, Debater actor, BattleState state)
        {
            var duel = state.Rules.Phases;
            if (duel == null || outcome.IsRejected)
            {
                return outcome;
            }

            var opponent = state.Opponent;
            var phase = duel.PhaseFor(opponent.Coherence);
            var landed = actor.Side == Side.Player && Answer(outcome, phase, opponent, state);
            if (!landed)
            {
                KeepAboveFloor(outcome, actor, opponent, phase.UntilCoherence);
            }

            return outcome;
        }

        public static bool Counters(DuelPhase phase, ActionId action) => phase != null && phase.Counter == action;

        private static bool Answer(Outcome outcome, DuelPhase phase, Debater opponent, BattleState state)
        {
            if (!PhasedDuel.CanCounterWith(outcome.Action))
            {
                return false;
            }

            if (Counters(phase, outcome.Action))
            {
                return Land(outcome, phase, opponent, state);
            }

            if (outcome.Action != ActionId.Concede && outcome.Hit)
            {
                Deflect(outcome, state.Rules);
            }

            return false;
        }

        private static bool Land(Outcome outcome, DuelPhase phase, Debater opponent, BattleState state)
        {
            var answered = outcome.Action == ActionId.Concede ? state.CurrentArgument.HasValue : outcome.Hit;
            if (!answered)
            {
                return false;
            }

            var rules = state.Rules;
            outcome.TargetId = opponent.Id;
            outcome.Hit = true;
            outcome.Counterattack = false;
            outcome.TargetCoherenceDelta = -Math.Min(rules.PhaseCounterDamage, Math.Max(0, opponent.Coherence - phase.UntilCoherence));
            outcome.PlayerAudienceDelta = rules.PhaseCounterAudienceGain;
            return true;
        }

        private static void Deflect(Outcome outcome, BattleRules rules)
        {
            outcome.Hit = false;
            outcome.Deflected = true;
            outcome.TargetCoherenceDelta = 0;
            if (outcome.Action == ActionId.Refute)
            {
                outcome.Counterattack = true;
                outcome.ActorCoherenceDelta -= rules.CounterattackDamage;
                outcome.PlayerAudienceDelta = 0;
                return;
            }

            outcome.PlayerAudienceDelta = -rules.MaieuticsMissAudienceLoss;
        }

        private static void KeepAboveFloor(Outcome outcome, Debater actor, Debater opponent, int floor)
        {
            var deepest = Math.Min(0, floor + 1 - opponent.Coherence);
            if (actor.Id == opponent.Id)
            {
                outcome.ActorCoherenceDelta = Math.Max(outcome.ActorCoherenceDelta, deepest);
            }
            else if (outcome.TargetId == opponent.Id)
            {
                outcome.TargetCoherenceDelta = Math.Max(outcome.TargetCoherenceDelta, deepest);
            }
        }
    }
}
