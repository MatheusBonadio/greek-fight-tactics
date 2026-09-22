using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.EditorTools;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Grid
{
    public sealed class DuelCrowdPlannerTests
    {
        private const int Seed = 402;
        private const int PlayerId = 1;
        private const int OpponentId = 2;
        private const int FirstBystanderId = 100;
        private const int OpenSize = 16;
        private const int SeedsToCompare = 8;

        private static readonly Coord Player = new(7, 8);
        private static readonly Coord Opponent = new(8, 8);

        [Test]
        public void OpenSquare_SeatsTheWholeCrowdOnARingAroundTheDuel()
        {
            var grid = OpenRevealed(OpenSize);
            var rules = new CrowdRules();

            var arena = DuelCrowdPlanner.Plan(grid, Player, Opponent, rules, Seed);

            Assert.AreEqual(rules.MaximumCrowd, arena.Seats.Count);
            Assert.AreEqual(new Vector2(7.5f, 8f), arena.Center);
            CollectionAssert.AllItemsAreUnique(arena.Seats.Select(seat => seat.Seat));
            foreach (var seat in arena.Seats)
            {
                AssertIsFreeRingCell(grid, arena, rules, seat.Seat);
                Assert.AreEqual(arena.FacingFrom(seat.Seat), seat.Facing);
                Assert.Greater(Dot(seat.Facing, arena.Center - new Vector2(seat.Seat.X, seat.Seat.Y)), 0f, $"{seat} must face the duel");
                Assert.IsTrue(arena.IsLit(seat.Seat), $"{seat} stands inside the lit arena");
            }
        }

        [Test]
        public void EverySpectator_WalksInFromAnEdgeOutsideTheArena()
        {
            var grid = OpenRevealed(OpenSize);
            var rules = new CrowdRules();

            var arena = DuelCrowdPlanner.Plan(grid, Player, Opponent, rules, Seed);

            CollectionAssert.AllItemsAreUnique(arena.Seats.Select(seat => seat.Spawn));
            foreach (var seat in arena.Seats)
            {
                Assert.IsTrue(DuelCrowdPlanner.IsOnSide(grid, seat.Spawn), $"{seat} must come from the map edge");
                Assert.IsFalse(arena.IsLit(seat.Spawn), $"{seat} must come from outside the lit arena");
                var steps = DuelCrowdPlanner.StepsFrom(grid, seat.Seat, cell => grid[cell].IsPassable && !grid[cell].IsOccupied && !arena.IsInterior(cell));
                var walk = steps[seat.Spawn.Y * grid.Width + seat.Spawn.X];
                Assert.GreaterOrEqual(walk, rules.MinimumApproachSteps, $"{seat} walks in instead of popping up beside the ring");
                Assert.LessOrEqual(walk, rules.MaximumApproachSteps, $"{seat} must arrive before the cap");
            }
        }

        [Test]
        public void TheSameSeed_PlansTheSameCrowd()
        {
            var grid = OpenRevealed(OpenSize);

            var first = DuelCrowdPlanner.Plan(grid, Player, Opponent, new CrowdRules(), Seed);
            var second = DuelCrowdPlanner.Plan(grid, Player, Opponent, new CrowdRules(), Seed);

            CollectionAssert.AreEqual(first.Seats.Select(seat => seat.ToString()), second.Seats.Select(seat => seat.ToString()));
            var others = Enumerable.Range(Seed + 1, SeedsToCompare).Select(seed => string.Join(" ", DuelCrowdPlanner.Plan(grid, Player, Opponent, new CrowdRules(), seed).Seats));
            CollectionAssert.Contains(others.Select(plan => plan != string.Join(" ", first.Seats)), true, "the seed varies the crowd");
        }

        [Test]
        public void BlockedOccupiedAndFoggedCells_AreNeverSeats()
        {
            var grid = OpenRevealed(OpenSize);
            var blocked = new[] { new Coord(11, 8), new Coord(4, 8), new Coord(7, 11) };
            var occupied = new Coord(7, 5);
            var fogged = new Coord(10, 11);
            foreach (var cell in blocked)
            {
                GridFactory.Block(grid, cell);
            }

            GridFactory.Occupy(grid, occupied, FirstBystanderId);
            var tile = grid[fogged];
            tile.Revealed = false;
            grid.TrySet(fogged, tile);

            var arena = DuelCrowdPlanner.Plan(grid, Player, Opponent, new CrowdRules(), Seed);

            var seats = arena.Seats.Select(seat => seat.Seat).ToList();
            CollectionAssert.IsNotEmpty(seats);
            CollectionAssert.DoesNotContain(seats, occupied);
            CollectionAssert.DoesNotContain(seats, fogged);
            foreach (var cell in blocked)
            {
                CollectionAssert.DoesNotContain(seats, cell);
            }
        }

        [Test]
        public void ACrampedRoom_SeatsOnlyWhatFits()
        {
            var player = new Coord(2, 2);
            var opponent = new Coord(2, 3);
            var grid = OpenRevealed(5, player, opponent);
            var rules = new CrowdRules();

            var arena = DuelCrowdPlanner.Plan(grid, player, opponent, rules, Seed);

            Assert.Less(arena.Seats.Count, rules.MinimumCrowd);
            foreach (var seat in arena.Seats)
            {
                AssertIsFreeRingCell(grid, arena, rules, seat.Seat);
            }
        }

        [Test]
        public void LeavingTheArena_HeadsForAnEdgeOnTheSpectatorsSide()
        {
            var grid = OpenRevealed(OpenSize);
            var arena = DuelCrowdPlanner.Plan(grid, Player, Opponent, new CrowdRules(), Seed);
            var terrain = DuelCrowdPlanner.CopyTerrain(grid);
            var random = new System.Random(Seed);
            var path = new List<Coord>();
            var sides = new HashSet<string>();

            foreach (var seat in arena.Seats)
            {
                Assert.IsTrue(DuelCrowdPlanner.TryPlanExit(terrain, arena, seat.Seat, new CrowdRules(), random, path), $"{seat} has no way out");
                var exit = path[^1];
                Assert.IsTrue(DuelCrowdPlanner.IsOnSide(terrain, exit), $"{seat} must leave through the map edge");
                var outwardX = seat.Seat.X >= arena.Center.x ? exit.X == terrain.Width - 1 : exit.X == 0;
                var outwardY = seat.Seat.Y >= arena.Center.y ? exit.Y == terrain.Height - 1 : exit.Y == 0;
                Assert.IsTrue(outwardX || outwardY, $"{seat} walked across the arena to {exit} instead of away from it");
                AssertIsWalk(seat.Seat, path);
                sides.Add(exit.X == 0 ? "west" : exit.X == terrain.Width - 1 ? "east" : exit.Y == 0 ? "south" : "north");
            }

            Assert.GreaterOrEqual(sides.Count, 3, "the crowd breaks up towards opposite sides");
        }

        [Test]
        public void LeavingTheArena_IsTheSameForTheSameSeed()
        {
            var grid = OpenRevealed(OpenSize);
            var arena = DuelCrowdPlanner.Plan(grid, Player, Opponent, new CrowdRules(), Seed);

            CollectionAssert.AreEqual(Exits(grid, arena, Seed), Exits(grid, arena, Seed));
        }

        [Test]
        public void CopyTerrain_DropsOccupantsAndBlocksWhatIsAsked()
        {
            var grid = OpenRevealed(4, new Coord(0, 0), new Coord(0, 1));
            GridFactory.Occupy(grid, new Coord(1, 1), FirstBystanderId);

            var copy = DuelCrowdPlanner.CopyTerrain(grid, cell => cell == new Coord(2, 2));
            var kept = DuelCrowdPlanner.CopyTerrain(grid, null, true);

            Assert.IsFalse(copy[new Coord(1, 1)].IsOccupied);
            Assert.IsFalse(copy[new Coord(2, 2)].IsPassable);
            Assert.IsTrue(copy[new Coord(3, 3)].IsPassable);
            Assert.IsTrue(copy[new Coord(3, 3)].Revealed);
            Assert.AreEqual(FirstBystanderId, kept[new Coord(1, 1)].OccupantId);
        }

        [TestCaseSource(nameof(AgoraOpponents))]
        public void AgoraDuel_GathersEightToTwelveSpectators(string id)
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var opponent = map.Actors.Single(actor => actor.Character == id);
            var grid = BuildAgora(map, opponent);
            var target = new Coord(opponent.X, opponent.Y);
            GridFactory.Occupy(grid, target, OpponentId);
            var player = ChallengeCell(map, grid, target);
            GridFactory.Occupy(grid, player, PlayerId);
            new FogOfWar(grid).Reveal(player);
            var rules = new CrowdRules();

            var arena = DuelCrowdPlanner.Plan(grid, player, target, rules, Seed);

            TestContext.WriteLine($"{id}: {arena.Seats.Count} spectators around {arena.Center}, lit radius {arena.LitRadius:0.00}");
            Assert.GreaterOrEqual(arena.Seats.Count, rules.MinimumCrowd, $"the duel with {id} gathers too small a crowd");
            Assert.LessOrEqual(arena.Seats.Count, rules.MaximumCrowd);
            foreach (var seat in arena.Seats)
            {
                AssertIsFreeRingCell(grid, arena, rules, seat.Seat);
            }
        }

        private static IEnumerable<string> AgoraOpponents()
        {
            var opponents = DataTools.OpponentIds();
            return MapLayout.Load(SandboxTools.MapPath).Actors.Skip(1).Select(actor => actor.Character).Where(character => opponents.Contains(character));
        }

        private static List<Coord> Exits(GridModel grid, DuelArena arena, int seed)
        {
            var random = new System.Random(seed);
            var path = new List<Coord>();
            return arena.Seats
                .Select(seat => DuelCrowdPlanner.TryPlanExit(DuelCrowdPlanner.CopyTerrain(grid), arena, seat.Seat, new CrowdRules(), random, path) ? path[^1] : seat.Seat)
                .ToList();
        }

        private static void AssertIsFreeRingCell(GridModel grid, DuelArena arena, CrowdRules rules, Coord seat)
        {
            Assert.IsTrue(grid[seat].IsPassable, $"{seat} is blocked");
            Assert.IsFalse(grid[seat].IsOccupied, $"{seat} is taken");
            Assert.IsTrue(grid[seat].Revealed, $"{seat} is under the fog");
            Assert.GreaterOrEqual(PositionalModifiers.Distance(seat, arena.Player), rules.DebaterClearance, $"{seat} crowds Kleia");
            Assert.GreaterOrEqual(PositionalModifiers.Distance(seat, arena.Opponent), rules.DebaterClearance, $"{seat} crowds the opponent");
            Assert.GreaterOrEqual(arena.DistanceFromCenter(seat), rules.InnerRadius, $"{seat} stands inside the duel");
            Assert.LessOrEqual(arena.DistanceFromCenter(seat), rules.MaximumRingRadius, $"{seat} stands too far away");
        }

        private static void AssertIsWalk(Coord from, IReadOnlyList<Coord> path)
        {
            var previous = from;
            foreach (var cell in path)
            {
                Assert.AreEqual(1, PositionalModifiers.Distance(previous, cell), $"{previous} to {cell} is not a step");
                previous = cell;
            }
        }

        private static float Dot(Facing facing, Vector2 toward)
        {
            var step = PositionalModifiers.FacingVector(facing);
            return step.X * toward.x + step.Y * toward.y;
        }

        private static GridModel OpenRevealed(int size) => OpenRevealed(size, Player, Opponent);

        private static GridModel OpenRevealed(int size, Coord player, Coord opponent)
        {
            var grid = GridFactory.Open(size, size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cell = new Coord(x, y);
                    var tile = grid[cell];
                    tile.Revealed = true;
                    grid.TrySet(cell, tile);
                }
            }

            GridFactory.Occupy(grid, player, PlayerId);
            GridFactory.Occupy(grid, opponent, OpponentId);
            return grid;
        }

        private static GridModel BuildAgora(MapLayout map, MapActor opponent)
        {
            var grid = new GridModel(map.Width, map.Height);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    grid.TrySet(new Coord(x, y), new TileData
                    {
                        Terrain = TerrainId.Marble,
                        Cost = map.IsWalkable(x, y) ? (byte)PathFinder.MinimumStepCost : TileData.ImpassableCost,
                        Revealed = x + y < (map.FogRevealDepth ?? SandboxTools.FogRevealDepth)
                    });
                }
            }

            for (var index = 1; index < map.Actors.Count; index++)
            {
                var actor = map.Actors[index];
                if (actor != opponent)
                {
                    GridFactory.Occupy(grid, new Coord(actor.X, actor.Y), FirstBystanderId + index);
                }
            }

            return grid;
        }

        private static Coord ChallengeCell(MapLayout map, GridModel grid, Coord target)
        {
            var start = new Coord(map.Actors[0].X, map.Actors[0].Y);
            var path = new List<Coord>();

            Assert.IsTrue(new PathFinder().FindPathBeside(grid, start, target, path), $"Kleia cannot walk up to {target} to challenge him");
            return path.Count > 0 ? path[^1] : start;
        }
    }
}
