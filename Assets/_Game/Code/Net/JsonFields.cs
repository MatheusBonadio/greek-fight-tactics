using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Net
{
    internal static class JsonFields
    {
        public static bool TryParseObject(string json, out JObject value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                value = JToken.Parse(json) as JObject;
                return value != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static string Text(JObject source, string name)
        {
            var token = source?[name];
            return token != null && (token.Type == JTokenType.String || token.Type == JTokenType.Integer) ? token.ToString() : null;
        }
    }
}
