using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Grid
{
    public sealed class FogOfWar
    {
        public const int DefaultRevealRadius = 3;

        private const int BitsPerByte = 8;

        private readonly GridModel grid;
        private readonly HashSet<Coord> held = new();

        public FogOfWar(GridModel grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public int BitmaskLength => (grid.Width * grid.Height + BitsPerByte - 1) / BitsPerByte;

        public IReadOnlyCollection<Coord> Held => held;

        public bool IsHeld(Coord cell) => held.Contains(cell);

        public void Hold(Coord cell)
        {
            if (!grid.TryGet(cell, out var tile))
            {
                return;
            }

            held.Add(cell);
            tile.Revealed = false;
            grid.TrySet(cell, tile);
        }

        public bool Release(Coord cell) => held.Remove(cell);

        public int Reveal(Coord center, int radius = DefaultRevealRadius)
        {
            var newlyRevealed = 0;
            for (var offsetY = -radius; offsetY <= radius; offsetY++)
            {
                var span = radius - Math.Abs(offsetY);
                for (var offsetX = -span; offsetX <= span; offsetX++)
                {
                    var cell = new Coord(center.X + offsetX, center.Y + offsetY);
                    if (!grid.TryGet(cell, out var tile) || tile.Revealed || held.Contains(cell))
                    {
                        continue;
                    }

                    tile.Revealed = true;
                    grid.TrySet(cell, tile);
                    newlyRevealed++;
                }
            }

            return newlyRevealed;
        }

        public byte[] ToBitmask()
        {
            var bitmask = new byte[BitmaskLength];
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (!grid[new Coord(x, y)].Revealed)
                    {
                        continue;
                    }

                    var bit = BitIndex(x, y);
                    bitmask[bit / BitsPerByte] |= (byte)(1 << (bit % BitsPerByte));
                }
            }

            return bitmask;
        }

        public bool TryLoadBitmask(byte[] bitmask)
        {
            if (bitmask == null || bitmask.Length != BitmaskLength)
            {
                return false;
            }

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var bit = BitIndex(x, y);
                    var tile = grid[cell];
                    tile.Revealed = !held.Contains(cell) && (bitmask[bit / BitsPerByte] & (1 << (bit % BitsPerByte))) != 0;
                    grid.TrySet(cell, tile);
                }
            }

            return true;
        }

        private int BitIndex(int x, int y) => y * grid.Width + x;
    }
}
