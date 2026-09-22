using System;

namespace Game.Battle
{
    public static class BattleSimulator
    {
        public const int MaxConsecutiveRejections = 200;

        public static SimulationResult Run(BattleState state, IPlayerPolicy player, IOpponentPolicy opponent, IArgumentSource arguments)
        {
            var resolver = new BattleResolver();
            var rejections = 0;
            var consecutiveRejections = 0;

            while (state.Status == BattleStatus.Ongoing)
            {
                var actor = state.Find(state.Queue.Current);
                var play = actor.Side == Side.Player
                    ? player.Decide(state, actor)
                    : opponent.Decide(state, actor, arguments);

                var outcome = resolver.Resolve(play, state);
                state.Apply(outcome);

                if (!outcome.IsRejected)
                {
                    consecutiveRejections = 0;
                    continue;
                }

                rejections++;
                if (++consecutiveRejections > MaxConsecutiveRejections)
                {
                    throw new InvalidOperationException($"{actor.Side} {actor.Id} produced {consecutiveRejections} rejected plays in a row on turn {state.Turn}; last rejection {outcome.Rejection}.");
                }
            }

            return new SimulationResult(state.Status, state.Turn, rejections);
        }
    }
}
