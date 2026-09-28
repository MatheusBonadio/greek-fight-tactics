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
        private const int SeedsToSample = 16;

        private static readonly Coord Player = new(9, 9);
        private static readonly Coord Opponent = new(10, 9);

        [Test]
        public void TheCrowdNeverSitsCloserThanTheClearance()
        {
            var crowdRules = new CrowdRules();
            foreach (var seed in Enumerable.Range(0, SeedsToSample))
            {
                var arena = Plan(seed, crowdRules);
                var closest = arena.Seats.Min(seat => Manhattan(seat.Seat, Player));

                Assert.GreaterOrEqual(closest, crowdRules.DebaterClearance,
                    $"seed {seed}: a spectator sat {closest} cells from Kleia, inside the clearance the planner promises");
            }
        }

        [Test]
        public void TheClearance_LeavesTheCrowdInsideTheReachOfMorale()
        {
            var rules = new BattleRules();

            Assert.GreaterOrEqual(rules.MoraleRadius, new CrowdRules().DebaterClearance,
                "the crowd must be able to weigh on the duel it came to watch: a morale radius under the seating clearance would leave the indicator dark in every duel");
        }

        [Test]
        public void ASingleStepOfCirculate_ChangesHowManyListenersAreInReach()
        {
            var rules = new BattleRules();
            var reachable = new PathFinder();
            var cells = new System.Collections.Generic.List<Coord>();
            var seedsThatMove = 0;

            foreach (var seed in Enumerable.Range(0, SeedsToSample))
            {
                var crowdRules = new CrowdRules();
                var arena = Plan(seed, crowdRules);
                var grid = OpenRevealed();
                for (var index = 0; index < arena.Seats.Count; index++)
                {
                    GridFactory.Occupy(grid, arena.Seats[index].Seat, 100 + index);
                }

                reachable.Reachable(grid, Player, rules.MoveRange, cells);
                var here = InReach(arena, Player, rules.MoraleRadius);
                var best = cells.Max(cell => InReach(arena, cell, rules.MoraleRadius));
                seedsThatMove += best > here ? 1 : 0;
            }

            Assert.AreEqual(SeedsToSample, seedsThatMove,
                "circulating must always be able to bring more listeners into reach, or the morale would be a number the player cannot act on");
        }

        private static int InReach(DuelArena arena, Coord cell, int radius) =>
            arena.Seats.Count(seat => Manhattan(seat.Seat, cell) <= radius);

        private static DuelArena Plan(int seed, CrowdRules crowdRules) =>
            DuelCrowdPlanner.Plan(OpenRevealed(), Player, Opponent, crowdRules, seed);

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
