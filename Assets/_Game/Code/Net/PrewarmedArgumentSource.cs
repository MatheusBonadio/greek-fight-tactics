using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Battle;
using Game.Core;

namespace Game.Net
{
    public sealed class PrewarmedArgumentSource : IArgumentSource
    {
        public const int SeedVariants = 8;

        private readonly ArgumentServiceClient client;
        private readonly ArgumentDiskCache cache;
        private readonly ArgumentValidator validator;
        private readonly FallbackArgumentLibrary fallback;
        private readonly Random random;
        private readonly Dictionary<int, OpponentContext> opponents = new();
        private readonly Dictionary<int, Queue<Argument>> ready = new();

        public PrewarmedArgumentSource(ArgumentServiceClient client, ArgumentDiskCache cache, ArgumentValidator validator, FallbackArgumentLibrary fallback, int seed)
        {
            this.client = client;
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            random = new Random(seed);
        }

        public bool IsOnline => client != null;

        public void Register(int debaterId, OpponentContext opponent)
        {
            opponents[debaterId] = opponent ?? throw new ArgumentNullException(nameof(opponent));
            if (!ready.ContainsKey(debaterId))
            {
                ready[debaterId] = new Queue<Argument>();
            }
        }

        public int ReadyCount(int debaterId) => ready.TryGetValue(debaterId, out var queue) ? queue.Count : 0;

        public async Task<int> PrewarmAsync(int debaterId, int count, CancellationToken cancellation)
        {
            if (client == null || !opponents.TryGetValue(debaterId, out var opponent) || opponent.CatalogIds.Count == 0)
            {
                return 0;
            }

            var queue = ready[debaterId];
            var added = 0;
            for (var index = 0; index < count && !cancellation.IsCancellationRequested; index++)
            {
                var request = opponent.RequestFor(opponent.CatalogIds[random.Next(opponent.CatalogIds.Count)], random.Next(SeedVariants));
                if (cache.TryGet(request.CacheKey, out var cached) && ArgumentJson.TryParse(cached, validator, out var cachedArgument, out _))
                {
                    queue.Enqueue(cachedArgument);
                    added++;
                    continue;
                }

                var fetch = await client.FetchAsync(request, cancellation);
                if (fetch.Status is ArgumentFetchStatus.Cancelled or ArgumentFetchStatus.Unavailable or ArgumentFetchStatus.Declined)
                {
                    break;
                }

                if (!fetch.IsDelivered)
                {
                    continue;
                }

                cache.Put(request.CacheKey, fetch.Json);
                queue.Enqueue(fetch.Argument);
                added++;
            }

            return added;
        }

        public bool TryNext(Debater opponent, out Argument argument)
        {
            argument = default;
            if (opponent == null)
            {
                return false;
            }

            if (ready.TryGetValue(opponent.Id, out var queue) && queue.Count > 0)
            {
                argument = queue.Dequeue();
                return true;
            }

            if (!opponents.TryGetValue(opponent.Id, out var context) || context.CatalogIds.Count == 0)
            {
                return false;
            }

            var entries = context.CatalogIds;
            var start = random.Next(entries.Count);
            for (var offset = 0; offset < entries.Count; offset++)
            {
                if (fallback.TryNext(entries[(start + offset) % entries.Count], out argument))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
