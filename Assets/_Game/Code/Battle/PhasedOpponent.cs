using System;

namespace Game.Battle
{
    public sealed class PhasedOpponent : IOpponentPolicy
    {
        private readonly PhasedDuel duel;
        private int phaseIndex = PhasedDuel.NoPhase;
        private int nextArgument;

        public PhasedOpponent(PhasedDuel duel)
        {
            this.duel = duel ?? throw new ArgumentNullException(nameof(duel));
        }

        public Play Decide(BattleState state, Debater self, IArgumentSource arguments)
        {
            var index = duel.IndexFor(self.Coherence);
            if (index != phaseIndex)
            {
                phaseIndex = index;
                nextArgument = 0;
            }

            var pool = duel.Phases[index].Arguments;
            var argument = pool[nextArgument % pool.Count];
            nextArgument++;
            return Play.Argue(self.Id, state.Player.Id, argument);
        }
    }
}
