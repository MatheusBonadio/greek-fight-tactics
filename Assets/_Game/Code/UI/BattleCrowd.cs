using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;
using UnityEngine;

namespace Game.UI
{
    public sealed class BattleCrowd : MonoBehaviour
    {
        public const float DefaultArrivalCapSeconds = 3f;
        public const float DefaultSecondsPerCell = 0.2f;
        public const float DefaultDepartureStaggerSeconds = 0.4f;
        public const float DefaultDepartureTimeoutSeconds = 10f;
        public const int FirstSpectatorId = 5000;

        private const float MinimumSecondsPerCell = 0.01f;

        [SerializeField] private ActorView template;
        [SerializeField] private float arrivalCapSeconds = DefaultArrivalCapSeconds;
        [SerializeField] private float secondsPerCell = DefaultSecondsPerCell;
        [SerializeField] private float departureStaggerSeconds = DefaultDepartureStaggerSeconds;
        [SerializeField] private float departureTimeoutSeconds = DefaultDepartureTimeoutSeconds;

        private readonly List<Spectator> gathered = new();
        private readonly List<Spectator> leaving = new();
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> path = new();
        private GridModel approach;
        private GridModel terrain;
        private GridModel fog;

        public CrowdRules Rules { get; } = new();

        public Material OutsideMaterial { get; set; }

        public ActorView Template => template;

        public DuelArena Arena { get; private set; }

        public IReadOnlyList<Spectator> Gathered => gathered;

        public IReadOnlyList<Spectator> Leaving => leaving;

        public bool IsGathering { get; private set; }

        public bool HasArrived => !IsGathering;

        public float GatherSeconds { get; private set; }

        public float ExpectedArrivalSeconds { get; private set; }

        public float ArrivalCapSeconds
        {
            get => arrivalCapSeconds;
            set => arrivalCapSeconds = Mathf.Max(0f, value);
        }

        public float SecondsPerCell
        {
            get => secondsPerCell;
            set => secondsPerCell = Mathf.Max(MinimumSecondsPerCell, value);
        }

        public IEnumerable<KeyValuePair<int, Coord>> Reservations => gathered.Select(spectator => new KeyValuePair<int, Coord>(spectator.Id, spectator.Seat.Seat)).ToList();

        public static int IdFor(int seatIndex) => FirstSpectatorId + seatIndex;

        public void Configure(ActorView spectatorTemplate)
        {
            template = spectatorTemplate;
        }

        public void Gather(GridModel exploration, DuelArena arena)
        {
            if (exploration == null)
            {
                throw new ArgumentNullException(nameof(exploration));
            }

            DestroyAll(gathered);
            Arena = arena ?? throw new ArgumentNullException(nameof(arena));
            fog = exploration;
            terrain = DuelCrowdPlanner.CopyTerrain(exploration);
            approach = DuelCrowdPlanner.CopyTerrain(exploration, arena.IsInterior, true);
            GatherSeconds = 0f;
            var longest = 0;
            for (var index = 0; index < arena.Seats.Count; index++)
            {
                var seat = arena.Seats[index];
                if (!template || !approach.TryGet(seat.Spawn, out var tile) || !tile.IsPassable || tile.IsOccupied)
                {
                    continue;
                }

                var id = IdFor(index);
                var view = Spawn(id);
                var walker = new GridWalker(approach, id, seat.Spawn, arena.FacingFrom(seat.Spawn));
                view.Bind(walker);
                if (pathFinder.FindPath(approach, seat.Spawn, seat.Seat, path))
                {
                    walker.Follow(path);
                    longest = Math.Max(longest, path.Count);
                }

                var spectator = new Spectator(id, view, seat);
                Light(spectator, !arena.IsLit(walker.Cell));
                ShowIfRevealed(spectator);
                gathered.Add(spectator);
            }

            ExpectedArrivalSeconds = Mathf.Min(arrivalCapSeconds, longest * secondsPerCell);
            IsGathering = true;
            if (gathered.Count == 0 || arrivalCapSeconds <= 0f)
            {
                Settle();
            }
        }

        public void Disperse(int seed)
        {
            if (IsGathering)
            {
                Settle();
            }

            var random = new System.Random(seed);
            foreach (var spectator in gathered)
            {
                var exits = DuelCrowdPlanner.CopyTerrain(terrain);
                spectator.View.Bind(new GridWalker(exits, spectator.Id, spectator.Seat.Seat, spectator.Seat.Facing));
                if (Arena == null || !DuelCrowdPlanner.TryPlanExit(exits, Arena, spectator.Seat.Seat, Rules, random, path))
                {
                    path.Clear();
                }

                spectator.BeginLeaving(path, (float)random.NextDouble() * departureStaggerSeconds);
                leaving.Add(spectator);
            }

            gathered.Clear();
            Arena = null;
        }

        public void Clear()
        {
            DestroyAll(gathered);
            DestroyAll(leaving);
            IsGathering = false;
            Arena = null;
        }

        private void Update()
        {
            var steps = Time.deltaTime * GridWalker.SecondsPerCell / Mathf.Max(MinimumSecondsPerCell, secondsPerCell);
            if (IsGathering)
            {
                UpdateGathering(Time.deltaTime, steps);
            }

            UpdateLeaving(Time.deltaTime, steps);
            foreach (var spectator in gathered)
            {
                ShowIfRevealed(spectator);
            }

            foreach (var spectator in leaving)
            {
                ShowIfRevealed(spectator);
            }
        }

        private void ShowIfRevealed(Spectator spectator)
        {
            if (fog != null && spectator.View && spectator.Walker != null)
            {
                spectator.View.IsShown = FogTileRules.ShowsWhatStandsAt(fog, spectator.Walker.WorldPosition);
            }
        }

        private void UpdateGathering(float seconds, float steps)
        {
            GatherSeconds += seconds;
            var seated = 0;
            foreach (var spectator in gathered)
            {
                if (!spectator.IsSeated && TryReachSeat(spectator, steps))
                {
                    spectator.MarkSeated();
                }

                Light(spectator, !spectator.IsSeated && !Arena.IsLit(spectator.Walker.Cell));

                seated += spectator.IsSeated ? 1 : 0;
            }

            if (seated == gathered.Count || GatherSeconds >= arrivalCapSeconds)
            {
                Settle();
            }
        }

        private void Light(Spectator spectator, bool outside)
        {
            var material = outside && OutsideMaterial ? OutsideMaterial : template.SpriteRenderer.sharedMaterial;
            foreach (var renderer in spectator.View.Renderers)
            {
                renderer.sharedMaterial = material;
            }
        }

        private bool TryReachSeat(Spectator spectator, float steps)
        {
            var walker = spectator.Walker;
            walker.Advance(steps);
            if (walker.IsMoving)
            {
                return false;
            }

            var seat = spectator.Seat.Seat;
            if (walker.Cell == seat)
            {
                walker.Teleport(seat, spectator.Seat.Facing);
                return true;
            }

            if (pathFinder.FindPath(approach, walker.Cell, seat, path))
            {
                walker.Follow(path);
            }

            return false;
        }

        private void Settle()
        {
            var seats = DuelCrowdPlanner.CopyTerrain(terrain);
            foreach (var spectator in gathered)
            {
                spectator.View.Bind(new GridWalker(seats, spectator.Id, spectator.Seat.Seat, spectator.Seat.Facing));
                spectator.MarkSeated();
                Light(spectator, false);
            }

            IsGathering = false;
        }

        private void UpdateLeaving(float seconds, float steps)
        {
            for (var index = leaving.Count - 1; index >= 0; index--)
            {
                var spectator = leaving[index];
                spectator.TickLeaving(seconds);
                var walker = spectator.Walker;
                if (spectator.HasSetOff && walker != null)
                {
                    walker.Advance(steps);
                }

                var gone = spectator.ExitPath.Count == 0 || (spectator.HasSetOff && (walker == null || !walker.IsMoving));
                if (gone || spectator.LeavingSeconds >= departureTimeoutSeconds)
                {
                    DestroyView(spectator);
                    leaving.RemoveAt(index);
                }
            }
        }

        private ActorView Spawn(int id)
        {
            var view = Instantiate(template, transform);
            view.name = $"spectator_{id}";
            view.gameObject.SetActive(true);
            return view;
        }

        private static void DestroyAll(List<Spectator> spectators)
        {
            foreach (var spectator in spectators)
            {
                DestroyView(spectator);
            }

            spectators.Clear();
        }

        private static void DestroyView(Spectator spectator)
        {
            if (spectator.View)
            {
                spectator.View.Bind(null);
                Destroy(spectator.View.gameObject);
            }
        }
    }
}
