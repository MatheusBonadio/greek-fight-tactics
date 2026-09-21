using System;
using System.Collections.Generic;

namespace Game.Core
{
    public readonly struct AggroCandidate
    {
        public readonly Coord Cell;
        public readonly int Radius;
        public readonly bool Dormant;

        public AggroCandidate(Coord cell, int radius, bool dormant = false)
        {
            Cell = cell;
            Radius = radius;
            Dormant = dormant;
        }

        public static AggroCandidate Asleep => new(default, 0, true);

        public bool Reaches(Coord player) => !Dormant && AggroRule.IsTriggered(player, Cell, Radius);
    }

    public sealed class AggroWatch
    {
        public const int NoTrigger = -1;

        private readonly HashSet<int> waitingForExit = new();

        public bool IsWaitingForExit(int index) => waitingForExit.Contains(index);

        public void Disengage(int index)
        {
            if (index >= 0)
            {
                waitingForExit.Add(index);
            }
        }

        public int Update(Coord player, IReadOnlyList<AggroCandidate> candidates)
        {
            var triggered = NoTrigger;
            var nearest = int.MaxValue;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var inRange = candidate.Reaches(player);
                if (waitingForExit.Contains(index))
                {
                    if (!inRange)
                    {
                        waitingForExit.Remove(index);
                    }

                    continue;
                }

                var distance = Math.Abs(player.X - candidate.Cell.X) + Math.Abs(player.Y - candidate.Cell.Y);
                if (inRange && distance < nearest)
                {
                    triggered = index;
                    nearest = distance;
                }
            }

            return triggered;
        }
    }
}
