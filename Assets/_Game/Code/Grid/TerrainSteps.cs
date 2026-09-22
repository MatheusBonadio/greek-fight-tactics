using System;
using Game.Core;

namespace Game.Grid
{
    public static class TerrainSteps
    {
        public const int MaxClimb = 1;
        public const int ClimbCost = 1;

        public static bool CanStep(in TileData from, in TileData to) => Math.Abs(to.Level - from.Level) <= MaxClimb;

        public static bool CanStep(GridModel grid, Coord from, Coord to) =>
            PathFinder.AreBeside(from, to)
            && grid.TryGet(from, out var origin)
            && grid.TryGet(to, out var destination)
            && CanStep(origin, destination);

        public static int StepCost(in TileData from, in TileData to) =>
            Math.Max(PathFinder.MinimumStepCost, (int)to.Cost) + (to.Level > from.Level ? ClimbCost : 0);
    }
}
