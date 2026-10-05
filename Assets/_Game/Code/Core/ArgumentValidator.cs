using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class ArgumentValidator
    {
        public const int MinSpeechLength = 40;
        public const int MaxSpeechLength = 300;

        private readonly HashSet<string> refutableFallacyIds;

        public ArgumentValidator(IEnumerable<string> refutableFallacyIds)
        {
            if (refutableFallacyIds == null)
            {
                throw new ArgumentNullException(nameof(refutableFallacyIds));
            }

            this.refutableFallacyIds = new HashSet<string>(refutableFallacyIds, StringComparer.Ordinal);
        }

        public bool IsValid(in Argument argument) => Validate(argument) == ArgumentViolation.None;

        public ArgumentViolation Validate(in Argument argument)
        {
            if (string.IsNullOrWhiteSpace(argument.Speech))
            {
                return ArgumentViolation.MissingSpeech;
            }

            var speechLength = argument.Speech.Trim().Length;
            if (speechLength < MinSpeechLength)
            {
                return ArgumentViolation.SpeechTooShort;
            }

            if (speechLength > MaxSpeechLength)
            {
                return ArgumentViolation.SpeechTooLong;
            }

            if (argument.FallacyId != null && !refutableFallacyIds.Contains(argument.FallacyId))
            {
                return ArgumentViolation.UnknownFallacy;
            }

            if (argument.Premises == null || argument.Premises.Count == 0)
            {
                return ArgumentViolation.MissingPremises;
            }

            foreach (var premise in argument.Premises)
            {
                if (string.IsNullOrWhiteSpace(premise))
                {
                    return ArgumentViolation.EmptyPremise;
                }
            }

            if (!argument.HasWeakPremise && !argument.IsRefutable)
            {
                return ArgumentViolation.Unanswerable;
            }

            if (argument.HasWeakPremise && (uint)argument.WeakPremise >= (uint)argument.Premises.Count)
            {
                return ArgumentViolation.WeakPremiseOutOfRange;
            }

            return ArgumentTones.IsKnown(argument.Tone) ? ArgumentViolation.None : ArgumentViolation.UnknownTone;
        }
    }
}
