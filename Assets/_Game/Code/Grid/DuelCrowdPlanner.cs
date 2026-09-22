using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Game.Grid
{
    public static class DuelCrowdPlanner
    {
        public const int Unreachable = -1;

        private const float FullTurn = Mathf.PI * 2f;
        private const float OffEdgePenalty = 10f;
        private const float StepWeight = 0.05f;
        private const int FallbackExitSteps = 1;

        [NoAutoStaticsCleanup]
        private static readonly float[] PackedSeparationRatios = { 0.5f, 0.25f, 0f };

        [NoAutoStaticsCleanup]
        private static readonly Coord[] Steps =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        private enum MapSide
        {
            LowX,
            HighX,
            LowY,
            HighY
        }

        public static DuelArena Plan(GridModel grid, Coord player, Coord opponent, CrowdRules rules, int seed)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            rules ??= new CrowdRules();
            var random = new System.Random(seed);
            var probe = new DuelArena(player, opponent, rules.InnerRadius, rules.MaximumRingRadius, null);
            var candidates = SeatCandidates(grid, probe, rules, random);
            var chosen = ChooseSeats(candidates, rules, random);
            var litRadius = chosen.Aggregate(rules.OuterRadius, (radius, candidate) => Math.Max(radius, candidate.Distance));
            return new DuelArena(player, opponent, rules.InnerRadius, litRadius, AssignSpawns(grid, probe, chosen, litRadius, rules));
        }

        public static GridModel CopyTerrain(GridModel grid, Func<Coord, bool> blocked = null, bool keepOccupants = false)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            var copy = new GridModel(grid.Width, grid.Height);
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var tile = grid[cell];
                    if (!keepOccupants)
                    {
                        tile.OccupantId = TileData.NoOccupant;
                    }

                    if (blocked != null && blocked(cell))
                    {
                        tile.Cost = TileData.ImpassableCost;
                    }

                    copy.TrySet(cell, tile);
                }
            }

            return copy;
        }

        public static bool TryPlanExit(GridModel terrain, DuelArena arena, Coord seat, CrowdRules rules, System.Random random, List<Coord> path)
        {
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            if (arena == null)
            {
                throw new ArgumentNullException(nameof(arena));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            rules ??= new CrowdRules();
            random ??= new System.Random();
            path.Clear();
            var pathFinder = new PathFinder();
            var steps = StepsFrom(terrain, seat, cell => IsWalkable(terrain, cell));
            var sideX = seat.X >= arena.Center.x ? MapSide.HighX : MapSide.LowX;
            var sideY = seat.Y >= arena.Center.y ? MapSide.HighY : MapSide.LowY;
            var sides = random.Next(2) == 0
                ? new[] { sideX, sideY, Opposite(sideY), Opposite(sideX) }
                : new[] { sideY, sideX, Opposite(sideX), Opposite(sideY) };

            foreach (var minimum in new[] { Math.Max(FallbackExitSteps, rules.MinimumExitSteps), FallbackExitSteps })
            {
                foreach (var side in sides)
                {
                    if (TryNearestOnSide(terrain, steps, side, minimum, out var target) && pathFinder.FindPath(terrain, seat, target, path))
                    {
                        return true;
                    }
                }
            }

            path.Clear();
            return false;
        }

        public static bool IsOnSide(GridModel grid, Coord cell) =>
            cell.X == 0 || cell.Y == 0 || cell.X == grid.Width - 1 || cell.Y == grid.Height - 1;

        public static int[] StepsFrom(GridModel grid, Coord origin, Func<Coord, bool> canEnter)
        {
            var steps = new int[grid.Width * grid.Height];
            Array.Fill(steps, Unreachable);
            if (!grid.Contains(origin))
            {
                return steps;
            }

            var frontier = new Queue<Coord>();
            steps[IndexOf(grid, origin)] = 0;
            frontier.Enqueue(origin);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                var next = steps[IndexOf(grid, cell)] + 1;
                foreach (var step in Steps)
                {
                    var neighbour = cell + step;
                    if (!grid.Contains(neighbour) || steps[IndexOf(grid, neighbour)] != Unreachable || !canEnter(neighbour))
                    {
                        continue;
                    }

                    steps[IndexOf(grid, neighbour)] = next;
                    frontier.Enqueue(neighbour);
                }
            }

            return steps;
        }

        private static List<Candidate> SeatCandidates(GridModel grid, DuelArena probe, CrowdRules rules, System.Random random)
        {
            var candidates = new List<Candidate>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var distance = probe.DistanceFromCenter(cell);
                    if (distance < rules.InnerRadius || distance > rules.MaximumRingRadius || !IsFreeSeat(grid, probe, cell, rules))
                    {
                        continue;
                    }

                    var steps = StepsFrom(grid, cell, next => CanApproach(grid, probe, next));
                    if (!HasSpawn(grid, probe, steps, rules.OuterRadius, rules))
                    {
                        continue;
                    }

                    candidates.Add(new Candidate(cell, distance, AngleOf(probe.Center, cell), random.NextDouble(), steps));
                }
            }

            return candidates;
        }

        private static List<Candidate> ChooseSeats(List<Candidate> candidates, CrowdRules rules, System.Random random)
        {
            var chosen = new List<Candidate>();
            var crowd = Math.Max(1, rules.MaximumCrowd);
            var sector = FullTurn / crowd;
            var offset = (float)random.NextDouble() * sector;
            for (var index = 0; index < crowd; index++)
            {
                var start = offset + index * sector;
                Candidate best = null;
                foreach (var candidate in candidates)
                {
                    if (candidate.Distance <= rules.OuterRadius && IsInSector(candidate.Angle, start, sector) && (best == null || IsCloserToRing(candidate, best, rules)))
                    {
                        best = candidate;
                    }
                }

                if (best != null)
                {
                    chosen.Add(best);
                }
            }

            var minimum = Math.Min(rules.MinimumCrowd, crowd);
            foreach (var ratio in PackedSeparationRatios)
            {
                var separation = sector * ratio;
                foreach (var candidate in candidates.Except(chosen).OrderBy(candidate => candidate.Distance).ThenBy(candidate => candidate.Tiebreak).ToList())
                {
                    if (chosen.Count >= minimum)
                    {
                        return chosen;
                    }

                    if (chosen.All(other => AngularGap(other.Angle, candidate.Angle) >= separation))
                    {
                        chosen.Add(candidate);
                    }
                }
            }

            return chosen;
        }

        private static List<CrowdSeat> AssignSpawns(GridModel grid, DuelArena probe, List<Candidate> chosen, float litRadius, CrowdRules rules)
        {
            var used = new HashSet<Coord>();
            var seats = new List<CrowdSeat>();
            foreach (var candidate in chosen.OrderBy(candidate => candidate.Angle))
            {
                if (TryPickSpawn(grid, probe, candidate, litRadius, rules, used, out var spawn) || TryPickSpawn(grid, probe, candidate, candidate.Distance, rules, used, out spawn))
                {
                    used.Add(spawn);
                    seats.Add(new CrowdSeat(spawn, candidate.Cell, probe.FacingFrom(candidate.Cell)));
                }
            }

            return seats;
        }

        private static bool TryPickSpawn(GridModel grid, DuelArena probe, Candidate seat, float beyondRadius, CrowdRules rules, HashSet<Coord> used, out Coord spawn)
        {
            spawn = seat.Cell;
            var bestScore = float.MaxValue;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var steps = seat.Steps[IndexOf(grid, cell)];
                    if (steps < rules.MinimumApproachSteps || steps > rules.MaximumApproachSteps || used.Contains(cell) || probe.DistanceFromCenter(cell) <= beyondRadius)
                    {
                        continue;
                    }

                    var score = (IsOnSide(grid, cell) ? 0f : OffEdgePenalty) + AngularGap(AngleOf(probe.Center, cell), seat.Angle) + steps * StepWeight;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        spawn = cell;
                    }
                }
            }

            return bestScore < float.MaxValue;
        }

        private static bool HasSpawn(GridModel grid, DuelArena probe, int[] steps, float beyondRadius, CrowdRules rules)
        {
            for (var index = 0; index < steps.Length; index++)
            {
                var cell = new Coord(index % grid.Width, index / grid.Width);
                if (steps[index] >= rules.MinimumApproachSteps && steps[index] <= rules.MaximumApproachSteps && probe.DistanceFromCenter(cell) > beyondRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFreeSeat(GridModel grid, DuelArena probe, Coord cell, CrowdRules rules) =>
            grid.TryGet(cell, out var tile)
            && tile.IsPassable
            && !tile.IsOccupied
            && (tile.Revealed || !rules.RevealedSeatsOnly)
            && Manhattan(cell, probe.Player) >= rules.DebaterClearance
            && Manhattan(cell, probe.Opponent) >= rules.DebaterClearance;

        private static bool CanApproach(GridModel grid, DuelArena probe, Coord cell) =>
            IsWalkable(grid, cell) && !probe.IsInterior(cell);

        private static bool IsWalkable(GridModel grid, Coord cell) =>
            grid.TryGet(cell, out var tile) && tile.IsPassable && !tile.IsOccupied;

        private static bool TryNearestOnSide(GridModel grid, int[] steps, MapSide side, int minimumSteps, out Coord target)
        {
            target = default;
            var best = int.MaxValue;
            var length = side is MapSide.LowX or MapSide.HighX ? grid.Height : grid.Width;
            for (var along = 0; along < length; along++)
            {
                var cell = side switch
                {
                    MapSide.LowX => new Coord(0, along),
                    MapSide.HighX => new Coord(grid.Width - 1, along),
                    MapSide.LowY => new Coord(along, 0),
                    _ => new Coord(along, grid.Height - 1)
                };

                var distance = steps[IndexOf(grid, cell)];
                if (distance >= minimumSteps && distance < best)
                {
                    best = distance;
                    target = cell;
                }
            }

            return best < int.MaxValue;
        }

        private static MapSide Opposite(MapSide side) => side switch
        {
            MapSide.LowX => MapSide.HighX,
            MapSide.HighX => MapSide.LowX,
            MapSide.LowY => MapSide.HighY,
            _ => MapSide.LowY
        };

        private static bool IsCloserToRing(Candidate candidate, Candidate best, CrowdRules rules)
        {
            var gap = Math.Abs(candidate.Distance - rules.RingRadius);
            var bestGap = Math.Abs(best.Distance - rules.RingRadius);
            return gap < bestGap || (Mathf.Approximately(gap, bestGap) && candidate.Tiebreak < best.Tiebreak);
        }

        private static bool IsInSector(float angle, float start, float sector) => Wrap(angle - start) < sector;

        private static float AngularGap(float first, float second)
        {
            var gap = Wrap(first - second);
            return Math.Min(gap, FullTurn - gap);
        }

        private static float Wrap(float angle)
        {
            var wrapped = angle % FullTurn;
            return wrapped < 0f ? wrapped + FullTurn : wrapped;
        }

        private static float AngleOf(Vector2 center, Coord cell) => Wrap(Mathf.Atan2(cell.Y - center.y, cell.X - center.x));

        private static int Manhattan(Coord from, Coord to) => Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);

        private static int IndexOf(GridModel grid, Coord cell) => cell.Y * grid.Width + cell.X;

        private sealed class Candidate
        {
            public Candidate(Coord cell, float distance, float angle, double tiebreak, int[] steps)
            {
                Cell = cell;
                Distance = distance;
                Angle = angle;
                Tiebreak = tiebreak;
                Steps = steps;
            }

            public Coord Cell { get; }

            public float Distance { get; }

            public float Angle { get; }

            public double Tiebreak { get; }

            public int[] Steps { get; }
        }
    }
}
