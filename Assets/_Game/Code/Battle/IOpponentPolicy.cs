namespace Game.Battle
{
    public interface IOpponentPolicy
    {
        Play Decide(BattleState state, Debater self, IArgumentSource arguments);
    }
}
