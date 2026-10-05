using System.Collections.Generic;
using Game.Battle;
using Game.Core;
using Game.Grid;

namespace Game.Tests.EditMode.Battle
{
    internal abstract class AnsweringPolicy : IPlayerPolicy
    {
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();

        public Play Decide(BattleState state, Debater player)
        {
            if (!state.HasOpenArgument)
            {
                return Play.Concede(player.Id);
            }

            var argument = state.CurrentArgument.Value;
            return PositionalModifiers.Distance(player.Position, state.Opponent.Position) > Range(state, argument)
                ? Approach(state, player)
                : Answer(state, player, argument);
        }

        protected abstract int Range(BattleState state, Argument argument);

        protected abstract Play Answer(BattleState state, Debater player, Argument argument);

        private Play Approach(BattleState state, Debater player)
        {
            var opponent = state.Opponent;
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

    internal sealed class MaieuticsPolicy : AnsweringPolicy
    {
        private readonly System.Random guessing;

        public MaieuticsPolicy(System.Random guessing = null)
        {
            this.guessing = guessing;
        }

        protected override int Range(BattleState state, Argument argument) => BattleResolver.MaieuticsRange(state);

        protected override Play Answer(BattleState state, Debater player, Argument argument) =>
            Play.Maieutics(player.Id, state.Opponent.Id, guessing?.Next(argument.Premises.Count) ?? (argument.HasWeakPremise ? argument.WeakPremise : 0));
    }

    internal sealed class KnownFallacyPolicy : AnsweringPolicy
    {
        protected override int Range(BattleState state, Argument argument) => state.Rules.RefuteRange;

        protected override Play Answer(BattleState state, Debater player, Argument argument) =>
            Play.Refute(player.Id, state.Opponent.Id, argument.FallacyId);
    }

    internal sealed class ReadingPolicy : AnsweringPolicy
    {
        protected override int Range(BattleState state, Argument argument) =>
            argument.HasWeakPremise ? BattleResolver.MaieuticsRange(state) : state.Rules.RefuteRange;

        protected override Play Answer(BattleState state, Debater player, Argument argument) =>
            argument.HasWeakPremise
                ? Play.Maieutics(player.Id, state.Opponent.Id, argument.WeakPremise)
                : Play.Refute(player.Id, state.Opponent.Id, argument.FallacyId);
    }
}
