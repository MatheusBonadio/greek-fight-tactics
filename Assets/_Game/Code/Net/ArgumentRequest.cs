using System;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Net
{
    public sealed class ArgumentRequest
    {
        public ArgumentRequest(string opponentId, string opponentName, Profile profile, Act act, string catalogId, int seed, string topic = null)
        {
            if (string.IsNullOrWhiteSpace(opponentId))
            {
                throw new ArgumentException("Opponent id is required.", nameof(opponentId));
            }

            if (string.IsNullOrWhiteSpace(catalogId))
            {
                throw new ArgumentException("Catalog id is required.", nameof(catalogId));
            }

            OpponentId = opponentId;
            OpponentName = opponentName;
            Profile = profile;
            Act = act;
            CatalogId = catalogId;
            Seed = seed;
            Topic = topic;
        }

        public string OpponentId { get; }

        public string OpponentName { get; }

        public Profile Profile { get; }

        public Act Act { get; }

        public string CatalogId { get; }

        public int Seed { get; }

        public string Topic { get; }

        public string CacheKey => ArgumentDiskCache.KeyFor(OpponentId, CatalogId, Seed);

        public string ToJson()
        {
            var json = new JObject
            {
                ["opponent_id"] = OpponentId,
                ["profile"] = Profile == Profile.Sophist ? "sophist" : "philosopher",
                ["act"] = (int)Act,
                ["fallacy_id"] = CatalogId,
                ["seed"] = Seed
            };

            if (!string.IsNullOrWhiteSpace(OpponentName))
            {
                json["opponent_name"] = OpponentName;
            }

            if (!string.IsNullOrWhiteSpace(Topic))
            {
                json["topic"] = Topic;
            }

            return json.ToString(Formatting.None);
        }
    }
}
