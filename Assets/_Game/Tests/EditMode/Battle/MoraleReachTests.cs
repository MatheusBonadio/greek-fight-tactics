using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.Tests.EditMode.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Battle
{
    public sealed class MoraleReachTests
    {
        private const int OpenSize = 20;
        private const int FirstSpectatorId = 100;
        private const int SeedsToSample = 16;

        private static readonly Coord Player = new(9, 9);
        private static readonly Coord Opponent = new(10, 9);

        [Test]
        public void WhenTheCrowdGathers_ItStandsOutsideTheReachOfMorale()
        {
            var rules = new BattleRules();
            foreach (var seed in Enumerable.Range(0, SeedsToSample))
            {
                var arena = Plan(seed);
                var closest = arena.Seats.Min(seat => Manhattan(seat.Seat, Player));

                Assert.Greater(closest, rules.MoraleRadius,
                    $"seed {seed}: a spectator sat {closest} cells from Kleia, so the duel would open with morale already weighing");
            }
        }

        [Test]
        public void ASingleStepOfCirculate_ReachesTheCrowd()
        {
            var rules = new BattleRules();
            var reachable = new PathFinder();
            var cells = new System.Collections.Generic.List<Coord>();

            foreach (var seed in Enumerable.Range(0, SeedsToSample))
            {
                var arena = Plan(seed);
                var grid = OpenRevealed();
                for (var index = 0; index < arena.Seats.Count; index++)
                {
                    GridFactory.Occupy(grid, arena.Seats[index].Seat, FirstSpectatorId + index);
                }

                reachable.Reachable(grid, Player, rules.MoveRange, cells);
                var best = cells.Max(cell => arena.Seats.Count(seat => Manhattan(seat.Seat, cell) <= rules.MoraleRadius));

                Assert.GreaterOrEqual(best, 2,
                    $"seed {seed}: circulating within {rules.MoveRange} steps must bring Kleia beside at least two listeners");
            }
        }

        private static DuelArena Plan(int seed) =>
            DuelCrowdPlanner.Plan(OpenRevealed(), Player, Opponent, new CrowdRules(), seed);

        private static GridModel OpenRevealed()
        {
            var grid = GridFactory.Open(OpenSize, OpenSize);
            new FogOfWar(grid).Reveal(new Coord(OpenSize / 2, OpenSize / 2), OpenSize * 2);
            return grid;
        }

        private static int Manhattan(Coord from, Coord to) =>
            System.Math.Abs(from.X - to.X) + System.Math.Abs(from.Y - to.Y);
    }
}
