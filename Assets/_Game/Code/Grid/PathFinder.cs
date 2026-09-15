using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Grid
{
    public sealed class PathFinder
    {
        public const int MinimumStepCost = 1;

        private const int NoParent = -1;
        private const int HeapEntriesPerNode = 4;

        private static readonly Coord[] Directions =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        private readonly NodeHeap open = new();
        private int[] costs = Array.Empty<int>();
        private int[] parents = Array.Empty<int>();
        private int[] visitStamps = Array.Empty<int>();
        private int[] closedStamps = Array.Empty<int>();
        private int stamp;

        public bool FindPath(GridModel grid, Coord from, Coord to, List<Coord> path)
        {
            path.Clear();
            if (!grid.Contains(from) || !CanEnter(grid, to))
            {
                return false;
            }

            if (from == to)
            {
                return true;
            }

            BeginSearch(grid);
            var width = grid.Width;
            var start = IndexOf(from, width);
            var goal = IndexOf(to, width);
            Visit(start, 0, NoParent);
            open.Push(start, Heuristic(from, to));

            while (open.TryPop(out var current))
            {
                if (!Close(current))
                {
                    continue;
                }

                if (current == goal)
                {
                    BuildPath(goal, width, path);
                    return true;
                }

                var cell = CoordOf(current, width);
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (TryRelax(grid, current, next, out var nextIndex, out var nextCost))
                    {
                        open.Push(nextIndex, nextCost + Heuristic(next, to));
                    }
                }
            }

            return false;
        }

        public void Reachable(GridModel grid, Coord from, int maxCost, List<Coord> cells)
        {
            cells.Clear();
            if (!grid.Contains(from) || maxCost < MinimumStepCost)
            {
                return;
            }

            BeginSearch(grid);
            var width = grid.Width;
            var start = IndexOf(from, width);
            Visit(start, 0, NoParent);
            open.Push(start, 0);

            while (open.TryPop(out var current))
            {
                if (!Close(current))
                {
                    continue;
                }

                if (current != start)
                {
                    cells.Add(CoordOf(current, width));
                }

                var cell = CoordOf(current, width);
                foreach (var direction in Directions)
                {
                    if (TryRelax(grid, current, cell + direction, out var nextIndex, out var nextCost) && nextCost <= maxCost)
                    {
                        open.Push(nextIndex, nextCost);
                    }
                }
            }
        }

        private static bool CanEnter(GridModel grid, Coord cell) =>
            grid.TryGet(cell, out var tile) && tile.IsPassable && !tile.IsOccupied;

        private static int StepCost(TileData tile) => Math.Max(MinimumStepCost, (int)tile.Cost);

        private static int Heuristic(Coord from, Coord to) =>
            (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y)) * MinimumStepCost;

        private static int IndexOf(Coord cell, int width) => cell.Y * width + cell.X;

        private static Coord CoordOf(int index, int width) => new(index % width, index / width);

        private bool TryRelax(GridModel grid, int current, Coord next, out int nextIndex, out int nextCost)
        {
            nextIndex = NoParent;
            nextCost = 0;
            if (!CanEnter(grid, next))
            {
                return false;
            }

            nextIndex = IndexOf(next, grid.Width);
            if (closedStamps[nextIndex] == stamp)
            {
                return false;
            }

            nextCost = costs[current] + StepCost(grid[next]);
            if (visitStamps[nextIndex] == stamp && nextCost >= costs[nextIndex])
            {
                return false;
            }

            Visit(nextIndex, nextCost, current);
            return true;
        }

        private void BeginSearch(GridModel grid)
        {
            var nodeCount = grid.Width * grid.Height;
            if (costs.Length < nodeCount)
            {
                costs = new int[nodeCount];
                parents = new int[nodeCount];
                visitStamps = new int[nodeCount];
                closedStamps = new int[nodeCount];
                open.EnsureCapacity(nodeCount * HeapEntriesPerNode);
                stamp = 0;
            }

            if (stamp == int.MaxValue)
            {
                Array.Clear(visitStamps, 0, visitStamps.Length);
                Array.Clear(closedStamps, 0, closedStamps.Length);
                stamp = 0;
            }

            stamp++;
            open.Clear();
        }

        private void Visit(int index, int cost, int parent)
        {
            visitStamps[index] = stamp;
            costs[index] = cost;
            parents[index] = parent;
        }

        private bool Close(int index)
        {
            if (closedStamps[index] == stamp)
            {
                return false;
            }

            closedStamps[index] = stamp;
            return true;
        }

        private void BuildPath(int goal, int width, List<Coord> path)
        {
            for (var index = goal; parents[index] != NoParent; index = parents[index])
            {
                path.Add(CoordOf(index, width));
            }

            path.Reverse();
        }
    }
}
