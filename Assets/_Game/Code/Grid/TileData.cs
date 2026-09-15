using Game.Core;

namespace Game.Grid
{
    public struct TileData
    {
        public const byte ImpassableCost = byte.MaxValue;
        public const int NoOccupant = 0;

        public static readonly TileData OutOfBounds = new()
        {
            Cost = ImpassableCost,
            Revealed = false,
            OccupantId = NoOccupant
        };

        public TerrainId Terrain;
        public byte Cost;
        public bool Revealed;
        public int OccupantId;

        public readonly bool IsPassable => Cost != ImpassableCost;

        public readonly bool IsOccupied => OccupantId != NoOccupant;
    }
}
