namespace Game.Core
{
    public readonly struct Challenger
    {
        public readonly Coord Cell;
        public readonly bool IsPresent;
        public readonly bool IsRevealed;
        public readonly bool IsDefeated;

        public Challenger(Coord cell, bool revealed, bool defeated)
        {
            Cell = cell;
            IsPresent = true;
            IsRevealed = revealed;
            IsDefeated = defeated;
        }

        public static Challenger Absent => default;

        public bool CanBeChallenged => IsPresent && IsRevealed && !IsDefeated;

        public bool StandsAt(Coord cell) => IsPresent && Cell == cell;
    }
}
