using System.Collections.Generic;
using Game.Core;
using Game.Grid;

namespace Game.UI
{
    public sealed class Spectator
    {
        private readonly List<Coord> exitPath = new();

        public Spectator(int id, ActorView view, CrowdSeat seat)
        {
            Id = id;
            View = view;
            Seat = seat;
        }

        public int Id { get; }

        public ActorView View { get; }

        public CrowdSeat Seat { get; }

        public GridWalker Walker => View ? View.Walker : null;

        public bool IsSeated { get; private set; }

        public bool IsLeaving { get; private set; }

        public bool HasSetOff { get; private set; }

        public float DepartureDelay { get; private set; }

        public float LeavingSeconds { get; private set; }

        public IReadOnlyList<Coord> ExitPath => exitPath;

        public Coord Exit => exitPath.Count > 0 ? exitPath[exitPath.Count - 1] : Seat.Seat;

        public void MarkSeated() => IsSeated = true;

        public void BeginLeaving(IEnumerable<Coord> path, float delay)
        {
            exitPath.Clear();
            exitPath.AddRange(path);
            IsSeated = false;
            IsLeaving = true;
            HasSetOff = false;
            DepartureDelay = delay;
            LeavingSeconds = 0f;
        }

        public bool TickLeaving(float deltaSeconds)
        {
            LeavingSeconds += deltaSeconds;
            if (HasSetOff || LeavingSeconds < DepartureDelay || Walker == null)
            {
                return false;
            }

            HasSetOff = true;
            Walker.Follow(exitPath);
            return true;
        }
    }
}
