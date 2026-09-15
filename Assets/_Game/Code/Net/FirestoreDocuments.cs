using System;
using System.Globalization;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Net
{
    public static class FirestoreDocuments
    {
        public const string SavesCollection = "saves";
        public const string ProgressCollection = "progress";
        public const string DuelsCollection = "duels";

        public static JObject ToFields(JObject json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return new JObject(json.Properties().Select(property => new JProperty(property.Name, ToValue(property.Value))));
        }

        public static JObject FromFields(JObject fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            return new JObject(fields.Properties().Select(property => new JProperty(property.Name, FromValue(property.Value as JObject))));
        }

        public static JObject SaveCommit(string documentsRoot, string uid, SaveData save)
        {
            if (string.IsNullOrWhiteSpace(documentsRoot))
            {
                throw new ArgumentException("Documents root is required.", nameof(documentsRoot));
            }

            if (string.IsNullOrWhiteSpace(uid))
            {
                throw new ArgumentException("User id is required.", nameof(uid));
            }

            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var saveJson = JObject.Parse(SaveJson.Serialize(save, false));
            var writes = new JArray(Update($"{documentsRoot}/{SavesCollection}/{uid}", saveJson));
            if (saveJson[DuelsCollection] is JObject duels)
            {
                foreach (var duel in duels.Properties().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writes.Add(Update($"{documentsRoot}/{ProgressCollection}/{uid}/{DuelsCollection}/{duel.Name}", (JObject)duel.Value));
                }
            }

            return new JObject { ["writes"] = writes };
        }

        public static bool TryReadSave(string documentJson, out SaveData save, out string error)
        {
            save = null;
            if (!JsonFields.TryParseObject(documentJson, out var document) || document["fields"] is not JObject fields)
            {
                error = "document has no fields";
                return false;
            }

            JObject json;
            try
            {
                json = FromFields(fields);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                error = exception.Message;
                return false;
            }

            return SaveJson.TryDeserialize(json.ToString(Formatting.None), out save, out error);
        }

        private static JObject Update(string name, JObject json) =>
            new() { ["update"] = new JObject { ["name"] = name, ["fields"] = ToFields(json) } };

        private static JObject ToValue(JToken token) => token.Type switch
        {
            JTokenType.String => new JObject { ["stringValue"] = token.Value<string>() },
            JTokenType.Integer => new JObject { ["integerValue"] = token.ToString() },
            JTokenType.Float => new JObject { ["doubleValue"] = token.Value<double>() },
            JTokenType.Boolean => new JObject { ["booleanValue"] = token.Value<bool>() },
            JTokenType.Null => new JObject { ["nullValue"] = JValue.CreateNull() },
            JTokenType.Object => new JObject { ["mapValue"] = new JObject { ["fields"] = ToFields((JObject)token) } },
            JTokenType.Array => new JObject { ["arrayValue"] = new JObject { ["values"] = new JArray(token.Select(ToValue)) } },
            _ => throw new FormatException($"unsupported JSON token {token.Type}")
        };

        private static JToken FromValue(JObject value)
        {
            var property = value?.Properties().FirstOrDefault() ?? throw new FormatException("field value must be a non-empty object");
            return property.Name switch
            {
                "stringValue" => new JValue(property.Value.Value<string>()),
                "integerValue" => new JValue(long.Parse(property.Value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture)),
                "doubleValue" => new JValue(property.Value.Value<double>()),
                "booleanValue" => new JValue(property.Value.Value<bool>()),
                "nullValue" => JValue.CreateNull(),
                "mapValue" => FromFields(property.Value["fields"] as JObject ?? new JObject()),
                "arrayValue" => new JArray((property.Value["values"] as JArray ?? new JArray()).Select(item => FromValue(item as JObject))),
                _ => throw new FormatException($"unsupported Firestore value {property.Name}")
            };
        }
    }
}
