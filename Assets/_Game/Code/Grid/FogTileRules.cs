using Game.Core;
using UnityEngine;

namespace Game.Grid
{
    public static class FogTileRules
    {
        public static bool ShowsWhatStandsOn(GridModel grid, Coord cell) => IsRevealed(grid, cell);

        public static bool ShowsWhatStandsAt(GridModel grid, Vector2 world) => IsRevealed(grid, IsoMath.WorldToGrid(world));

        public static FogTileKind KindAt(GridModel grid, Coord cell)
        {
            if (!grid.TryGet(cell, out var tile) || tile.Revealed)
            {
                return FogTileKind.None;
            }

            if (SharesGroundWithRevealed(grid, cell, tile, new Coord(1, 0)))
            {
                return FogTileKind.EdgeNorthEast;
            }

            if (SharesGroundWithRevealed(grid, cell, tile, new Coord(0, 1)))
            {
                return FogTileKind.EdgeNorthWest;
            }

            if (SharesGroundWithRevealed(grid, cell, tile, new Coord(0, -1)))
            {
                return FogTileKind.EdgeSouthEast;
            }

            return SharesGroundWithRevealed(grid, cell, tile, new Coord(-1, 0)) ? FogTileKind.EdgeSouthWest : FogTileKind.Full;
        }

        private static bool SharesGroundWithRevealed(GridModel grid, Coord cell, in TileData tile, Coord towards) =>
            grid.TryGet(cell + towards, out var neighbour) && neighbour.Revealed
            && System.Math.Abs(neighbour.Level - tile.Level) <= TerrainSteps.MaxClimb;

        private static bool IsRevealed(GridModel grid, Coord cell) => grid.TryGet(cell, out var tile) && tile.Revealed;
    }
}
