using System;
using System.Collections.Generic;

namespace Game.Core
{
    public readonly struct Argument
    {
        public const int NoWeakPremise = -1;

        public readonly string Speech;
        public readonly string FallacyId;
        public readonly IReadOnlyList<string> Premises;
        public readonly int WeakPremise;
        public readonly string Tone;

        public Argument(string speech, string fallacyId, IEnumerable<string> premises, int weakPremise, string tone)
        {
            Speech = speech;
            FallacyId = fallacyId;
            Premises = premises == null ? Array.Empty<string>() : new List<string>(premises).AsReadOnly();
            WeakPremise = weakPremise;
            Tone = tone;
        }

        public bool IsRefutable => FallacyId != null;

        public bool HasWeakPremise => WeakPremise != NoWeakPremise;

        public bool IsOnlyRefutable => IsRefutable && !HasWeakPremise;
    }
}
