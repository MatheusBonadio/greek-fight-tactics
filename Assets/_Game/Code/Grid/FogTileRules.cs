using Game.Core;

namespace Game.Grid
{
    public static class FogTileRules
    {
        public static FogTileKind KindAt(GridModel grid, Coord cell)
        {
            if (!grid.TryGet(cell, out var tile) || tile.Revealed)
            {
                return FogTileKind.None;
            }

            if (IsRevealed(grid, cell + new Coord(1, 0)))
            {
                return FogTileKind.EdgeNorthEast;
            }

            if (IsRevealed(grid, cell + new Coord(0, 1)))
            {
                return FogTileKind.EdgeNorthWest;
            }

            if (IsRevealed(grid, cell + new Coord(0, -1)))
            {
                return FogTileKind.EdgeSouthEast;
            }

            return IsRevealed(grid, cell + new Coord(-1, 0)) ? FogTileKind.EdgeSouthWest : FogTileKind.Full;
        }

        private static bool IsRevealed(GridModel grid, Coord cell) => grid.TryGet(cell, out var tile) && tile.Revealed;
    }
}
