using System.Collections.Generic;
using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public sealed class StrategicPlayerPolicy : IPlayerPolicy
    {
        public const double ConcedeBelowCoherenceRatio = 0.35;

        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();

        public Play Decide(BattleState state, Debater player)
        {
            var opponent = state.Opponent;
            if ((double)player.Coherence / player.MaxCoherence < ConcedeBelowCoherenceRatio || !state.CurrentArgument.HasValue)
            {
                return Play.Concede(player.Id);
            }

            var argument = state.CurrentArgument.Value;
            var distance = PositionalModifiers.Distance(player.Position, opponent.Position);
            if (distance <= BattleResolver.MaieuticsRange(state))
            {
                return Play.Maieutics(player.Id, opponent.Id, argument.WeakPremise);
            }

            if (argument.IsRefutable && distance <= state.Rules.RefuteRange)
            {
                return Play.Refute(player.Id, opponent.Id, argument.FallacyId);
            }

            return ApproachOrConcede(state, player, opponent);
        }

        private Play ApproachOrConcede(BattleState state, Debater player, Debater opponent)
        {
            pathFinder.Reachable(state.Grid, player.Position, state.Rules.MoveRange, reachable);
            var bestDistance = PositionalModifiers.Distance(player.Position, opponent.Position);
            Coord? best = null;
            foreach (var cell in reachable)
            {
                var distance = PositionalModifiers.Distance(cell, opponent.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = cell;
                }
            }

            return best.HasValue ? Play.Circulate(player.Id, best.Value) : Play.Concede(player.Id);
        }
    }
}
