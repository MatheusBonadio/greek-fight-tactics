using Game.Core;
using Game.Grid;

namespace Game.Tests.EditMode.Grid
{
    internal static class GridFactory
    {
        public const byte OpenCost = 1;

        public static GridModel Open(int width, int height)
        {
            var grid = new GridModel(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    grid.TrySet(new Coord(x, y), new TileData { Terrain = TerrainId.Marble, Cost = OpenCost });
                }
            }

            return grid;
        }

        public static void SetCost(GridModel grid, Coord cell, byte cost)
        {
            var tile = grid[cell];
            tile.Cost = cost;
            grid.TrySet(cell, tile);
        }

        public static void Block(GridModel grid, Coord cell) => SetCost(grid, cell, TileData.ImpassableCost);

        public static void Occupy(GridModel grid, Coord cell, int occupantId)
        {
            var tile = grid[cell];
            tile.OccupantId = occupantId;
            grid.TrySet(cell, tile);
        }

        public static GridModel Serpentine(int size)
        {
            var grid = Open(size, size);
            for (var x = 1; x < size - 1; x += 2)
            {
                var gapY = (x / 2) % 2 == 0 ? size - 1 : 0;
                for (var y = 0; y < size; y++)
                {
                    if (y != gapY)
                    {
                        Block(grid, new Coord(x, y));
                    }
                }
            }

            return grid;
        }
    }
}
