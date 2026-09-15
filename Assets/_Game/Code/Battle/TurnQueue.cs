using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Battle
{
    public sealed class TurnQueue
    {
        private readonly List<int> order;
        private readonly Dictionary<int, int> skippedTurns;
        private int cursor;

        public TurnQueue(IEnumerable<Debater> debaters)
        {
            order = debaters
                .OrderByDescending(debater => debater.Initiative)
                .ThenBy(debater => debater.Id)
                .Select(debater => debater.Id)
                .ToList();

            if (order.Count == 0)
            {
                throw new ArgumentException("A turn queue needs at least one debater.", nameof(debaters));
            }

            skippedTurns = order.ToDictionary(id => id, _ => 0);
        }

        public int Current => order[cursor];

        public void Advance(int turnCost)
        {
            skippedTurns[Current] += Math.Max(0, turnCost - 1);
            cursor = NextCursor(cursor, skippedTurns);
        }

        public IReadOnlyList<int> Preview(int count)
        {
            var upcoming = new List<int>(count);
            var simulatedSkips = new Dictionary<int, int>(skippedTurns);
            var simulatedCursor = cursor;
            for (var index = 0; index < count; index++)
            {
                upcoming.Add(order[simulatedCursor]);
                simulatedCursor = NextCursor(simulatedCursor, simulatedSkips);
            }

            return upcoming;
        }

        private int NextCursor(int from, Dictionary<int, int> pendingSkips)
        {
            var next = from;
            while (true)
            {
                next = (next + 1) % order.Count;
                var id = order[next];
                if (pendingSkips[id] == 0)
                {
                    return next;
                }

                pendingSkips[id]--;
            }
        }
    }
}
