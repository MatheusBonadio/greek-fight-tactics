using System.Collections.Generic;
using Game.Battle;
using Game.Core;
using Game.Grid;

namespace Game.Tests.EditMode.Battle
{
    internal sealed class CounterPlayerPolicy : IPlayerPolicy
    {
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();

        public Play Decide(BattleState state, Debater player)
        {
            var opponent = state.Opponent;
            var duel = state.Rules.Phases;
            if (duel == null || !state.CurrentArgument.HasValue)
            {
                return Play.Concede(player.Id);
            }

            var argument = state.CurrentArgument.Value;
            var distance = PositionalModifiers.Distance(player.Position, opponent.Position);
            return duel.PhaseFor(opponent.Coherence).Counter switch
            {
                ActionId.Refute when distance <= state.Rules.RefuteRange => Play.Refute(player.Id, opponent.Id, argument.FallacyId),
                ActionId.Maieutics when distance <= BattleResolver.MaieuticsRange(state) => Play.Maieutics(player.Id, opponent.Id, argument.WeakPremise),
                ActionId.Concede => Play.Concede(player.Id),
                _ => Approach(state, player, opponent)
            };
        }

        private Play Approach(BattleState state, Debater player, Debater opponent)
        {
            pathFinder.Reachable(state.Grid, player.Position, state.Rules.MoveRange, reachable);
            var best = player.Position;
            var bestDistance = PositionalModifiers.Distance(player.Position, opponent.Position);
            foreach (var cell in reachable)
            {
                var distance = PositionalModifiers.Distance(cell, opponent.Position);
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            return best == player.Position ? Play.Concede(player.Id) : Play.Circulate(player.Id, best);
        }
    }
}
