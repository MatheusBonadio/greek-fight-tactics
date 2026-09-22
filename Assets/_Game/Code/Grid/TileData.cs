using Game.Core;
using Unity.Scripting.LifecycleManagement;

namespace Game.Grid
{
    public struct TileData
    {
        public const byte ImpassableCost = byte.MaxValue;
        public const int NoOccupant = 0;
        public const byte GroundLevel = 0;

        [NoAutoStaticsCleanup]
        public static readonly TileData OutOfBounds = new()
        {
            Cost = ImpassableCost,
            Revealed = false,
            OccupantId = NoOccupant,
            Level = GroundLevel
        };

        public TerrainId Terrain;
        public byte Cost;
        public bool Revealed;
        public int OccupantId;
        public byte Level;

        public readonly bool IsPassable => Cost != ImpassableCost;

        public readonly bool IsOccupied => OccupantId != NoOccupant;
    }
}
