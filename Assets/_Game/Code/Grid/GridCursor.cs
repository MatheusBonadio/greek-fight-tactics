using System;
using Game.Core;
using UnityEngine;

namespace Game.Grid
{
    public sealed class GridCursor
    {
        public const float StickDeadZone = 0.5f;

        private readonly GridModel grid;

        public GridCursor(GridModel grid, Coord start)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (!grid.Contains(start))
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "Cursor must start inside the grid.");
            }

            Cell = start;
        }

        public event Action<Coord> Moved;

        public Coord Cell { get; private set; }

        public bool TryHover(Vector2 worldPoint, Vector2 gridOrigin)
        {
            var cell = IsoMath.WorldToGrid(worldPoint - gridOrigin);
            return TryMoveTo(cell);
        }

        public bool TryStep(Vector2 screenDirection)
        {
            var right = Axis(screenDirection.x);
            var up = Axis(screenDirection.y);
            if (right == 0 && up == 0)
            {
                return false;
            }

            var delta = new Coord(Math.Sign(up + right), Math.Sign(up - right));
            return TryMoveTo(Cell + delta);
        }

        private bool TryMoveTo(Coord cell)
        {
            if (!grid.Contains(cell))
            {
                return false;
            }

            if (cell != Cell)
            {
                Cell = cell;
                Moved?.Invoke(cell);
            }

            return true;
        }

        private static int Axis(float value) => value >= StickDeadZone ? 1 : value <= -StickDeadZone ? -1 : 0;
    }
}
