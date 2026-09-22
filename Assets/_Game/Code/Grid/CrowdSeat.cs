using Game.Core;

namespace Game.Grid
{
    public readonly struct CrowdSeat
    {
        public readonly Coord Spawn;
        public readonly Coord Seat;
        public readonly Facing Facing;

        public CrowdSeat(Coord spawn, Coord seat, Facing facing)
        {
            Spawn = spawn;
            Seat = seat;
            Facing = facing;
        }

        public override string ToString() => $"{Spawn} -> {Seat} {Facing}";
    }
}
