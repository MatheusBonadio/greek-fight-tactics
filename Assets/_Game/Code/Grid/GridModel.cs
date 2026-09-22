using System;
using Game.Core;

namespace Game.Grid
{
    public sealed class GridModel
    {
        private readonly TileData[,] tiles;

        public GridModel(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be positive.");
            }

            Width = width;
            Height = height;
            tiles = new TileData[width, height];
        }

        public int Width { get; }

        public int Height { get; }

        public int MaxLevel { get; private set; }

        public TileData this[Coord cell] => Contains(cell) ? tiles[cell.X, cell.Y] : TileData.OutOfBounds;

        public bool Contains(Coord cell) => (uint)cell.X < (uint)Width && (uint)cell.Y < (uint)Height;

        public bool TryGet(Coord cell, out TileData tile)
        {
            if (!Contains(cell))
            {
                tile = TileData.OutOfBounds;
                return false;
            }

            tile = tiles[cell.X, cell.Y];
            return true;
        }

        public bool TrySet(Coord cell, TileData tile)
        {
            if (!Contains(cell))
            {
                return false;
            }

            tiles[cell.X, cell.Y] = tile;
            MaxLevel = Math.Max(MaxLevel, tile.Level);
            return true;
        }
    }
}
