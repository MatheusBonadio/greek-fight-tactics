using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Grid
{
    public sealed class DuelArena
    {
        private readonly List<CrowdSeat> seats;

        public DuelArena(Coord player, Coord opponent, float innerRadius, float litRadius, IEnumerable<CrowdSeat> crowdSeats)
        {
            Player = player;
            Opponent = opponent;
            Center = new Vector2((player.X + opponent.X) * 0.5f, (player.Y + opponent.Y) * 0.5f);
            InnerRadius = innerRadius;
            LitRadius = Math.Max(innerRadius, litRadius);
            seats = new List<CrowdSeat>(crowdSeats ?? Array.Empty<CrowdSeat>());
        }

        public Coord Player { get; }

        public Coord Opponent { get; }

        public Vector2 Center { get; }

        public Vector2 WorldCenter => new((Center.x - Center.y) * IsoMath.CellWidth, (Center.x + Center.y) * IsoMath.CellHeight);

        public float InnerRadius { get; }

        public float LitRadius { get; }

        public IReadOnlyList<CrowdSeat> Seats => seats;

        public float DistanceFromCenter(Coord cell) => Vector2.Distance(Center, new Vector2(cell.X, cell.Y));

        public bool IsInterior(Coord cell) => DistanceFromCenter(cell) < InnerRadius;

        public bool IsLit(Coord cell) => cell == Player || cell == Opponent || DistanceFromCenter(cell) <= LitRadius;

        public bool IsSeat(Coord cell) => seats.Exists(seat => seat.Seat == cell);

        public Facing FacingFrom(Coord cell)
        {
            var towardX = Center.x - cell.X;
            var towardY = Center.y - cell.Y;
            if (Math.Abs(towardX) >= Math.Abs(towardY))
            {
                return towardX >= 0f ? Facing.NorthEast : Facing.SouthWest;
            }

            return towardY >= 0f ? Facing.NorthWest : Facing.SouthEast;
        }
    }
}
