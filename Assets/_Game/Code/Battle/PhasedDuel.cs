using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Battle
{
    public sealed class PhasedDuel
    {
        public const int NoPhase = -1;
        public const int LastFloor = 0;

        private readonly List<DuelPhase> phases;

        public PhasedDuel(IEnumerable<DuelPhase> phases)
        {
            this.phases = phases?.ToList() ?? throw new ArgumentNullException(nameof(phases));
            var problems = Problems(this.phases);
            if (problems.Count > 0)
            {
                throw new ArgumentException(string.Join("; ", problems), nameof(phases));
            }
        }

        public IReadOnlyList<DuelPhase> Phases => phases;

        public static bool CanCounterWith(ActionId action) => action is ActionId.Refute or ActionId.Maieutics or ActionId.Concede;

        public static IReadOnlyList<string> Problems(IReadOnlyList<DuelPhase> phases)
        {
            var problems = new List<string>();
            if (phases == null || phases.Count == 0)
            {
                problems.Add("a phased duel needs at least one phase");
                return problems;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var previousFloor = int.MaxValue;
            foreach (var phase in phases)
            {
                if (phase == null || string.IsNullOrWhiteSpace(phase.Id) || !ids.Add(phase.Id))
                {
                    problems.Add($"every phase needs its own id, found '{phase?.Id}'");
                    continue;
                }

                if (!CanCounterWith(phase.Counter))
                {
                    problems.Add($"phase {phase.Id} is countered by {phase.Counter}, which does not answer an argument");
                }

                if (phase.UntilCoherence < LastFloor || phase.UntilCoherence >= previousFloor)
                {
                    problems.Add($"phase {phase.Id} must end below the previous phase, at {LastFloor} or more");
                }

                if (phase.Arguments.Count == 0)
                {
                    problems.Add($"phase {phase.Id} has no argument to present");
                }

                if (phase.Counter == ActionId.Refute && phase.Arguments.Any(argument => !argument.IsRefutable))
                {
                    problems.Add($"phase {phase.Id} is countered by REFUTE but presents a paradox");
                }

                if (phase.Counter == ActionId.Maieutics && phase.Arguments.Any(argument => !argument.HasWeakPremise))
                {
                    problems.Add($"phase {phase.Id} is countered by MAIEUTICS but presents an argument without a weak premise");
                }

                previousFloor = phase.UntilCoherence;
            }

            var last = phases[phases.Count - 1];
            if (last != null && last.UntilCoherence != LastFloor)
            {
                problems.Add($"the last phase must end at {LastFloor} coherence, which ends the duel");
            }

            return problems;
        }

        public int IndexFor(int opponentCoherence)
        {
            for (var index = 0; index < phases.Count; index++)
            {
                if (opponentCoherence > phases[index].UntilCoherence)
                {
                    return index;
                }
            }

            return phases.Count - 1;
        }

        public DuelPhase PhaseFor(int opponentCoherence) => phases[IndexFor(opponentCoherence)];

        public BattleRules Rules() => new() { Phases = this, AudienceVictory = false };
    }
}
