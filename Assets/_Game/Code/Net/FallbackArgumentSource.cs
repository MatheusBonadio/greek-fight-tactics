using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;

namespace Game.Net
{
    public sealed class FallbackArgumentSource : IArgumentSource
    {
        private readonly FallbackArgumentLibrary library;
        private readonly Random random;
        private readonly Dictionary<int, IReadOnlyList<string>> pools = new();

        public FallbackArgumentSource(FallbackArgumentLibrary library, int seed)
        {
            this.library = library ?? throw new ArgumentNullException(nameof(library));
            random = new Random(seed);
        }

        public void Register(int debaterId, IEnumerable<string> fallacyIds)
        {
            if (fallacyIds == null)
            {
                throw new ArgumentNullException(nameof(fallacyIds));
            }

            pools[debaterId] = fallacyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        }

        public bool TryNext(Debater opponent, out Argument argument)
        {
            argument = default;
            if (opponent == null || !pools.TryGetValue(opponent.Id, out var entries) || entries.Count == 0)
            {
                return false;
            }

            var start = random.Next(entries.Count);
            for (var offset = 0; offset < entries.Count; offset++)
            {
                if (library.TryNext(entries[(start + offset) % entries.Count], out argument))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
