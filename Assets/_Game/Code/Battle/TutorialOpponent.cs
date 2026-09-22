using Game.Core;

namespace Game.Battle
{
    public sealed class TutorialOpponent : IOpponentPolicy
    {
        private Argument? lesson;

        public Play Decide(BattleState state, Debater self, IArgumentSource arguments)
        {
            if (!lesson.HasValue && arguments != null && arguments.TryNext(self, out var argument))
            {
                lesson = argument;
            }

            return lesson.HasValue ? Play.Argue(self.Id, state.Player.Id, lesson.Value) : Play.Concede(self.Id);
        }
    }
}
