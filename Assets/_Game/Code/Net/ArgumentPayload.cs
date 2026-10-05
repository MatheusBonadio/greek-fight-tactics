using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;

namespace Game.Net
{
    internal sealed class ArgumentPayload
    {
        [JsonProperty("speech", Required = Required.Always)]
        public string Speech { get; set; }

        [JsonProperty("fallacy_id", Required = Required.AllowNull)]
        public string FallacyId { get; set; }

        [JsonProperty("premises", Required = Required.Always)]
        public List<string> Premises { get; set; }

        [JsonProperty("weak_premise", Required = Required.AllowNull)]
        public int? WeakPremise { get; set; }

        [JsonProperty("tone", Required = Required.Always)]
        public string Tone { get; set; }

        public Argument ToArgument() => new(Speech, FallacyId, Premises, WeakPremise ?? Argument.NoWeakPremise, Tone);

        public static ArgumentPayload From(in Argument argument) => new()
        {
            Speech = argument.Speech,
            FallacyId = argument.FallacyId,
            Premises = argument.Premises.ToList(),
            WeakPremise = argument.HasWeakPremise ? argument.WeakPremise : null,
            Tone = argument.Tone
        };
    }
}
