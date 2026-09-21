using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Grid
{
    public sealed class GridWalker
    {
        public const float SecondsPerCell = 0.25f;

        private readonly GridModel grid;
        private readonly Queue<Coord> pending = new();
        private Coord stepOrigin;
        private float stepElapsed;

        public GridWalker(GridModel grid, int actorId, Coord start, Facing facing = Facing.SouthEast)
        {
            if (actorId == TileData.NoOccupant)
            {
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId, "Actor id must differ from TileData.NoOccupant.");
            }

            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (!grid.TryGet(start, out var tile) || tile.IsOccupied)
            {
                throw new ArgumentException($"Start cell {start} is outside the grid or already occupied.", nameof(start));
            }

            ActorId = actorId;
            Cell = start;
            stepOrigin = start;
            Facing = facing;
            SetOccupant(start, actorId);
        }

        public event Action<Coord> CellEntered;

        public int ActorId { get; }

        public Coord Cell { get; private set; }

        public Facing Facing { get; private set; }

        public bool IsStepping { get; private set; }

        public bool IsMoving => IsStepping || pending.Count > 0;

        public int PendingSteps => pending.Count;

        public bool WasBlocked { get; private set; }

        public Vector2 WorldPosition
        {
            get
            {
                if (!IsStepping)
                {
                    return IsoMath.GridToWorld(Cell);
                }

                var progress = Mathf.Clamp01(stepElapsed / SecondsPerCell);
                return Vector2.Lerp(IsoMath.GridToWorld(stepOrigin), IsoMath.GridToWorld(Cell), progress);
            }
        }

        public void Follow(IReadOnlyList<Coord> path)
        {
            pending.Clear();
            WasBlocked = false;
            foreach (var cell in path)
            {
                pending.Enqueue(cell);
            }

            if (!IsStepping)
            {
                TryBeginNextStep();
            }
        }

        public void Cancel()
        {
            pending.Clear();
        }

        public void FaceToward(Coord cell)
        {
            if (!IsStepping)
            {
                Facing = FacingRules.FromStep(Cell, cell, Facing);
            }
        }

        public void Teleport(Coord cell, Facing facing)
        {
            if (!grid.TryGet(cell, out var tile) || !tile.IsPassable || (tile.IsOccupied && tile.OccupantId != ActorId))
            {
                throw new ArgumentException($"Cannot teleport actor {ActorId} to {cell}.", nameof(cell));
            }

            pending.Clear();
            IsStepping = false;
            WasBlocked = false;
            SetOccupant(Cell, TileData.NoOccupant);
            SetOccupant(cell, ActorId);
            Cell = cell;
            stepOrigin = cell;
            stepElapsed = SecondsPerCell;
            Facing = facing;
        }

        public void Advance(float deltaSeconds)
        {
            var remaining = Math.Max(0f, deltaSeconds);
            while (IsStepping && remaining > 0f)
            {
                var stepLeft = SecondsPerCell - stepElapsed;
                if (remaining < stepLeft)
                {
                    stepElapsed += remaining;
                    return;
                }

                remaining -= stepLeft;
                CompleteStep();
                TryBeginNextStep();
            }
        }

        private void TryBeginNextStep()
        {
            if (pending.Count == 0)
            {
                return;
            }

            var next = pending.Peek();
            var isAdjacent = Math.Abs(next.X - Cell.X) + Math.Abs(next.Y - Cell.Y) == 1;
            if (!isAdjacent || !grid.TryGet(next, out var tile) || !tile.IsPassable || tile.IsOccupied)
            {
                pending.Clear();
                WasBlocked = true;
                return;
            }

            pending.Dequeue();
            Facing = FacingRules.FromStep(Cell, next, Facing);
            SetOccupant(Cell, TileData.NoOccupant);
            SetOccupant(next, ActorId);
            stepOrigin = Cell;
            Cell = next;
            stepElapsed = 0f;
            IsStepping = true;
            CellEntered?.Invoke(next);
        }

        private void CompleteStep()
        {
            stepElapsed = SecondsPerCell;
            stepOrigin = Cell;
            IsStepping = false;
        }

        private void SetOccupant(Coord cell, int occupantId)
        {
            var tile = grid[cell];
            tile.OccupantId = occupantId;
            grid.TrySet(cell, tile);
        }
    }
}
