using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Battle
{
    public sealed class DuelPhase
    {
        public DuelPhase(string id, ActionId counter, int untilCoherence, IEnumerable<Argument> arguments)
        {
            Id = id;
            Counter = counter;
            UntilCoherence = untilCoherence;
            Arguments = (arguments ?? Enumerable.Empty<Argument>()).ToList();
        }

        public string Id { get; }

        public ActionId Counter { get; }

        public int UntilCoherence { get; }

        public IReadOnlyList<Argument> Arguments { get; }
    }
}
