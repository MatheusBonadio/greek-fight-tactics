using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Grid;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Grid
{
    public sealed class PathFinderPerformanceTests
    {
        private const int GridSize = 100;
        private const int WarmupRuns = 5;
        private const int MeasuredRuns = 21;
        private const double BudgetMilliseconds = 5.0;

        private static readonly Coord From = new(0, 0);
        private static readonly Coord To = new(GridSize - 1, GridSize - 1);

        [Test, Performance]
        public void FindPath_LongestSerpentineRoute_Benchmark()
        {
            var grid = GridFactory.Serpentine(GridSize);
            var finder = new PathFinder();
            var path = new List<Coord>();

            Measure.Method(() => finder.FindPath(grid, From, To, path))
                .WarmupCount(WarmupRuns)
                .MeasurementCount(MeasuredRuns)
                .Run();
        }

        [Test]
        public void FindPath_LongestSerpentineRoute_StaysUnderBudget()
        {
            var grid = GridFactory.Serpentine(GridSize);
            var finder = new PathFinder();
            var path = new List<Coord>();

            for (var run = 0; run < WarmupRuns; run++)
            {
                finder.FindPath(grid, From, To, path);
            }

            var samples = new double[MeasuredRuns];
            var stopwatch = new Stopwatch();
            for (var run = 0; run < MeasuredRuns; run++)
            {
                stopwatch.Restart();
                Assert.IsTrue(finder.FindPath(grid, From, To, path));
                stopwatch.Stop();
                samples[run] = stopwatch.Elapsed.TotalMilliseconds;
            }

            System.Array.Sort(samples);
            var median = samples[MeasuredRuns / 2];
            TestContext.WriteLine($"path length {path.Count}, median {median:F3} ms");

            Assert.Greater(path.Count, GridSize * GridSize / 3);
            Assert.Less(median, BudgetMilliseconds);
        }
    }
}
