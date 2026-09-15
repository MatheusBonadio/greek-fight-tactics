using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Grid
{
    public sealed class RangeHighlighter
    {
        private readonly GridModel grid;
        private readonly PathFinder pathFinder;
        private readonly List<Coord> cells = new();

        public RangeHighlighter(GridModel grid, PathFinder pathFinder)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.pathFinder = pathFinder ?? throw new ArgumentNullException(nameof(pathFinder));
        }

        public event Action Changed;

        public HighlightKind Kind { get; private set; } = HighlightKind.None;

        public IReadOnlyList<Coord> Cells => cells;

        public void ShowMove(Coord from, int maxCost)
        {
            pathFinder.Reachable(grid, from, maxCost, cells);
            RangeShapes.RemoveUnrevealed(grid, cells);
            Publish(HighlightKind.Move);
        }

        public void ShowRefute(Coord from, int radius) => ShowDiamond(HighlightKind.Refute, from, radius);

        public void ShowMaieutics(Coord from, int radius) => ShowDiamond(HighlightKind.Maieutics, from, radius);

        public void Clear()
        {
            cells.Clear();
            Publish(HighlightKind.None);
        }

        public bool Contains(Coord cell) => cells.Contains(cell);

        private void ShowDiamond(HighlightKind kind, Coord from, int radius)
        {
            RangeShapes.RevealedDiamond(grid, from, radius, cells);
            Publish(kind);
        }

        private void Publish(HighlightKind kind)
        {
            Kind = kind;
            Changed?.Invoke();
        }
    }
}
