namespace Game.Core
{
    public sealed class DuelRecord
    {
        public DuelRecord(bool won, DuelVictory victory)
        {
            Won = won;
            Victory = victory;
        }

        public bool Won { get; }

        public DuelVictory Victory { get; }
    }
}
