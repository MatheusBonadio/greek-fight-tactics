using System;

namespace Game.Core
{
    public readonly struct Coord : IEquatable<Coord>
    {
        public readonly int X;
        public readonly int Y;

        public Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Coord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Coord other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(Coord left, Coord right) => left.Equals(right);

        public static bool operator !=(Coord left, Coord right) => !left.Equals(right);

        public static Coord operator +(Coord left, Coord right) => new(left.X + right.X, left.Y + right.Y);
    }
}
