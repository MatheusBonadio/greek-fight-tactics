namespace Game.Battle
{
    public readonly struct SimulationResult
    {
        public readonly BattleStatus Status;
        public readonly int Turns;
        public readonly int Rejections;

        public SimulationResult(BattleStatus status, int turns, int rejections)
        {
            Status = status;
            Turns = turns;
            Rejections = rejections;
        }
    }
}
