using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Game.Net
{
    public sealed class ArgumentValidationReport
    {
        private readonly List<ArgumentValidationItem> items = new();

        [JsonProperty("success")]
        public bool Success => true;

        [JsonProperty("valid")]
        public bool IsValid => ParseError == null && items.Count > 0 && items.TrueForAll(item => item.Violation == ArgumentViolation.None);

        [JsonProperty("parse_error")]
        public string ParseError { get; private set; }

        [JsonProperty("items")]
        public IReadOnlyList<ArgumentValidationItem> Items => items;

        public static ArgumentValidationReport Validate(string json, ArgumentValidator validator)
        {
            var report = new ArgumentValidationReport();
            JToken root;
            try
            {
                root = JToken.Parse(json ?? string.Empty);
            }
            catch (JsonException exception)
            {
                report.ParseError = exception.Message;
                return report;
            }

            IEnumerable<JToken> elements = root.Type == JTokenType.Array ? root.Children() : new[] { root };
            var index = 0;
            foreach (var element in elements)
            {
                ArgumentJson.TryParse(element.ToString(Formatting.None), validator, out var argument, out var violation);
                report.items.Add(new ArgumentValidationItem(index++, violation, argument));
            }

            if (index == 0)
            {
                report.ParseError = "no argument found";
            }

            return report;
        }
    }

    public sealed class ArgumentValidationItem
    {
        public ArgumentValidationItem(int index, ArgumentViolation violation, Argument argument)
        {
            Index = index;
            Violation = violation;
            Argument = violation == ArgumentViolation.None ? argument : (Argument?)null;
        }

        [JsonProperty("index")]
        public int Index { get; }

        [JsonProperty("violation")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ArgumentViolation Violation { get; }

        [JsonProperty("speech")]
        public string Speech => Argument?.Speech;

        [JsonIgnore]
        public Argument? Argument { get; }
    }
}
