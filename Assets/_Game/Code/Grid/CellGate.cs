using Game.Core;

namespace Game.Grid
{
    public static class CellGate
    {
        public static bool Close(GridModel grid, FogOfWar fog, Coord cell)
        {
            if (grid == null || fog == null || !grid.TryGet(cell, out var tile))
            {
                return false;
            }

            tile.Cost = TileData.ImpassableCost;
            grid.TrySet(cell, tile);
            fog.Hold(cell);
            return true;
        }

        public static bool Open(GridModel grid, FogOfWar fog, Coord cell, byte cost)
        {
            if (grid == null || fog == null || !grid.TryGet(cell, out var tile))
            {
                return false;
            }

            fog.Release(cell);
            tile.Cost = cost;
            tile.Revealed = true;
            grid.TrySet(cell, tile);
            return true;
        }

        public static bool IsClosed(GridModel grid, FogOfWar fog, Coord cell) =>
            fog != null && fog.IsHeld(cell) && grid != null && grid.TryGet(cell, out var tile) && !tile.IsPassable;
    }
}
