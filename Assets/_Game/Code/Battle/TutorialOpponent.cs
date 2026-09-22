namespace Game.Battle
{
    public sealed class TutorialOpponent : IOpponentPolicy
    {
        public Play Decide(BattleState state, Debater self, IArgumentSource arguments) =>
            arguments != null && arguments.TryNext(self, out var argument)
                ? Play.Argue(self.Id, state.Player.Id, argument)
                : Play.Concede(self.Id);
    }
}
