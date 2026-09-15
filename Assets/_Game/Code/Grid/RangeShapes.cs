using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Grid
{
    public static class RangeShapes
    {
        public static void RevealedDiamond(GridModel grid, Coord center, int radius, List<Coord> cells)
        {
            cells.Clear();
            for (var offsetY = -radius; offsetY <= radius; offsetY++)
            {
                var span = radius - Math.Abs(offsetY);
                for (var offsetX = -span; offsetX <= span; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var cell = new Coord(center.X + offsetX, center.Y + offsetY);
                    if (grid.TryGet(cell, out var tile) && tile.Revealed)
                    {
                        cells.Add(cell);
                    }
                }
            }
        }

        public static void RemoveUnrevealed(GridModel grid, List<Coord> cells)
        {
            cells.RemoveAll(cell => !grid[cell].Revealed);
        }
    }
}
