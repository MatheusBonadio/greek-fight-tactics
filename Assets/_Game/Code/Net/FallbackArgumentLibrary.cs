using System;
using System.Collections.Generic;
using Game.Content;
using Game.Core;

namespace Game.Net
{
    public sealed class FallbackArgumentLibrary
    {
        private readonly FallbackLibrarySO source;
        private readonly ArgumentValidator validator;
        private readonly int seed;
        private readonly Dictionary<string, FallbackArgumentPicker> pickers = new(StringComparer.Ordinal);

        public FallbackArgumentLibrary(FallbackLibrarySO source, ArgumentValidator validator, int seed)
        {
            this.source = source ? source : throw new ArgumentNullException(nameof(source));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.seed = seed;
        }

        public bool TryNext(string entryId, out Argument argument)
        {
            argument = default;
            return TryGetPicker(entryId, out var picker) && picker.TryNext(out argument);
        }

        public int CountFor(string entryId) => TryGetPicker(entryId, out var picker) ? picker.Count : 0;

        private bool TryGetPicker(string entryId, out FallbackArgumentPicker picker)
        {
            picker = null;
            if (entryId == null)
            {
                return false;
            }

            if (pickers.TryGetValue(entryId, out picker))
            {
                return true;
            }

            if (!source.TryGetJson(entryId, out var json))
            {
                return false;
            }

            var arguments = new List<Argument>();
            ArgumentJson.ParseValidList(json, validator, arguments);
            picker = new FallbackArgumentPicker(arguments, HashCode.Combine(seed, entryId));
            pickers[entryId] = picker;
            return true;
        }
    }
}
