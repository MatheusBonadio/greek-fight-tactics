using System;
using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public sealed class Debater
    {
        public Debater(int id, Side side, Profile profile, int maxCoherence, int initiative, Coord position, Facing facing)
        {
            if (id == TileData.NoOccupant)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Debater id must differ from TileData.NoOccupant.");
            }

            if (maxCoherence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCoherence), maxCoherence, "Max coherence must be positive.");
            }

            Id = id;
            Side = side;
            Profile = profile;
            MaxCoherence = maxCoherence;
            Coherence = maxCoherence;
            Initiative = initiative;
            Position = position;
            Facing = facing;
        }

        public int Id { get; }

        public Side Side { get; }

        public Profile Profile { get; }

        public int MaxCoherence { get; }

        public int Coherence { get; internal set; }

        public int Initiative { get; }

        public Coord Position { get; internal set; }

        public Facing Facing { get; internal set; }

        public bool IsDefeated => Coherence <= 0;
    }
}
