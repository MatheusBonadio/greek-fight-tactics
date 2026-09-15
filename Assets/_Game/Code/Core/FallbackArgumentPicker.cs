using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class FallbackArgumentPicker
    {
        private const int NoPreviousPick = -1;

        private readonly IReadOnlyList<Argument> arguments;
        private readonly Random random;
        private int previousIndex = NoPreviousPick;

        public FallbackArgumentPicker(IReadOnlyList<Argument> arguments, int seed)
        {
            this.arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            random = new Random(seed);
        }

        public int Count => arguments.Count;

        public bool TryNext(out Argument argument)
        {
            if (arguments.Count == 0)
            {
                argument = default;
                return false;
            }

            var index = arguments.Count == 1 ? 0 : PickDifferentFromPrevious();
            previousIndex = index;
            argument = arguments[index];
            return true;
        }

        private int PickDifferentFromPrevious()
        {
            if (previousIndex == NoPreviousPick)
            {
                return random.Next(arguments.Count);
            }

            var offset = random.Next(1, arguments.Count);
            return (previousIndex + offset) % arguments.Count;
        }
    }
}
