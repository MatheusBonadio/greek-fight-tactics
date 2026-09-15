using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;

namespace Game.Net
{
    public sealed class OpponentContext
    {
        public OpponentContext(string id, string displayName, Profile profile, Act act, IEnumerable<string> catalogIds, string topic = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Opponent id is required.", nameof(id));
            }

            if (catalogIds == null)
            {
                throw new ArgumentNullException(nameof(catalogIds));
            }

            Id = id;
            DisplayName = displayName;
            Profile = profile;
            Act = act;
            CatalogIds = catalogIds.Where(entry => !string.IsNullOrWhiteSpace(entry)).Distinct(StringComparer.Ordinal).ToList();
            Topic = topic;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public Profile Profile { get; }

        public Act Act { get; }

        public IReadOnlyList<string> CatalogIds { get; }

        public string Topic { get; }

        public static OpponentContext From(OpponentSO opponent, string topic = null)
        {
            if (!opponent)
            {
                throw new ArgumentNullException(nameof(opponent));
            }

            return new OpponentContext(
                opponent.name.ToLowerInvariant(),
                opponent.DisplayName,
                opponent.Profile,
                opponent.Act,
                opponent.FallacyPool.Where(fallacy => fallacy).Select(fallacy => fallacy.Id),
                topic);
        }

        public ArgumentRequest RequestFor(string catalogId, int seed) => new(Id, DisplayName, Profile, Act, catalogId, seed, Topic);
    }
}
