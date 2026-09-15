using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Scripting.LifecycleManagement;

namespace Game.Net
{
    public static class ArgumentJson
    {
        [NoAutoStaticsCleanup]
        private static readonly JsonSerializerSettings Settings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static bool TryParse(string json, ArgumentValidator validator, out Argument argument, out ArgumentViolation violation)
        {
            argument = default;
            if (!TryDeserialize(json, out var payload))
            {
                violation = ArgumentViolation.Malformed;
                return false;
            }

            return TryAccept(payload, validator, out argument, out violation);
        }

        public static Argument ParseOrFallback(string json, ArgumentValidator validator, Argument fallback) =>
            TryParse(json, validator, out var argument, out _) ? argument : fallback;

        public static int ParseValidList(string json, ArgumentValidator validator, List<Argument> accepted)
        {
            if (!TryParseArray(json, out var items))
            {
                return 0;
            }

            var acceptedCount = 0;
            foreach (var item in items)
            {
                if (TryConvert(item, out var payload) && TryAccept(payload, validator, out var argument, out _))
                {
                    accepted.Add(argument);
                    acceptedCount++;
                }
            }

            return acceptedCount;
        }

        public static string Serialize(in Argument argument) =>
            JsonConvert.SerializeObject(ArgumentPayload.From(argument), Formatting.Indented);

        private static bool TryAccept(ArgumentPayload payload, ArgumentValidator validator, out Argument argument, out ArgumentViolation violation)
        {
            argument = default;
            var candidate = payload.ToArgument();
            violation = validator.Validate(candidate);
            if (violation != ArgumentViolation.None)
            {
                return false;
            }

            argument = candidate;
            return true;
        }

        private static bool TryDeserialize(string json, out ArgumentPayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                payload = JsonConvert.DeserializeObject<ArgumentPayload>(json, Settings);
                return payload != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryParseArray(string json, out JArray items)
        {
            items = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                items = JArray.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryConvert(JToken item, out ArgumentPayload payload)
        {
            payload = null;
            if (item.Type != JTokenType.Object)
            {
                return false;
            }

            try
            {
                payload = item.ToObject<ArgumentPayload>(JsonSerializer.Create(Settings));
                return payload != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
