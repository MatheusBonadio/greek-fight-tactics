namespace Game.Battle
{
    public interface IPlayerPolicy
    {
        Play Decide(BattleState state, Debater player);
    }
}
