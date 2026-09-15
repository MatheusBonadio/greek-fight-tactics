using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public sealed class RandomPlayerPolicy : IPlayerPolicy
    {
        private const int ActionKinds = 5;

        private readonly Random random;
        private readonly IReadOnlyList<string> fallacyIds;
        private readonly double withdrawChance;
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();

        public RandomPlayerPolicy(Random random, IReadOnlyList<string> fallacyIds, double withdrawChance = 0)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.fallacyIds = fallacyIds is { Count: > 0 } ? fallacyIds : throw new ArgumentException("At least one fallacy id is required.", nameof(fallacyIds));
            this.withdrawChance = withdrawChance;
        }

        public Play Decide(BattleState state, Debater player)
        {
            if (random.NextDouble() < withdrawChance)
            {
                return Play.Withdraw(player.Id);
            }

            var opponent = state.Opponent;
            switch (random.Next(ActionKinds))
            {
                case 0:
                    return Play.Refute(player.Id, opponent.Id, fallacyIds[random.Next(fallacyIds.Count)]);
                case 1:
                    return Play.Maieutics(player.Id, opponent.Id, random.Next(PremiseCount(state)));
                case 2:
                    return Play.Concede(player.Id);
                case 3:
                    pathFinder.Reachable(state.Grid, player.Position, state.Rules.MoveRange, reachable);
                    return reachable.Count == 0 ? Play.Concede(player.Id) : Play.Circulate(player.Id, reachable[random.Next(reachable.Count)]);
                default:
                    return state.QuoteIds.Count == 0
                        ? Play.Concede(player.Id)
                        : Play.Quote(player.Id, state.QuoteIds.ElementAt(random.Next(state.QuoteIds.Count)));
            }
        }

        private static int PremiseCount(BattleState state) =>
            state.CurrentArgument.HasValue ? Math.Max(1, state.CurrentArgument.Value.Premises.Count) : 1;
    }
}
