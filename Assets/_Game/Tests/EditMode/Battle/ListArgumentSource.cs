using System.Collections.Generic;
using Game.Battle;
using Game.Core;
using Game.Net;

namespace Game.Tests.EditMode.Battle
{
    internal sealed class ListArgumentSource : IArgumentSource
    {
        private readonly FallbackArgumentPicker picker;

        public ListArgumentSource(IReadOnlyList<Argument> arguments, int seed)
        {
            picker = new FallbackArgumentPicker(arguments, seed);
        }

        public bool TryNext(Debater opponent, out Argument argument) => picker.TryNext(out argument);
    }

    internal sealed class LibraryArgumentSource : IArgumentSource
    {
        private readonly FallbackArgumentLibrary library;
        private readonly IReadOnlyList<string> entries;
        private readonly System.Random random;

        public LibraryArgumentSource(FallbackArgumentLibrary library, IReadOnlyList<string> entries, int seed)
        {
            this.library = library;
            this.entries = entries;
            random = new System.Random(seed);
        }

        public bool TryNext(Debater opponent, out Argument argument) =>
            library.TryNext(entries[random.Next(entries.Count)], out argument);
    }
}
