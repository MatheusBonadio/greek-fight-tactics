using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Battle
{
    public sealed class ScriptedArgumentSource : IArgumentSource
    {
        private readonly Argument[] arguments;
        private int next;

        public ScriptedArgumentSource(IEnumerable<Argument> arguments)
        {
            this.arguments = (arguments ?? Enumerable.Empty<Argument>()).ToArray();
        }

        public int Count => arguments.Length;

        public bool TryNext(Debater opponent, out Argument argument)
        {
            if (arguments.Length == 0)
            {
                argument = default;
                return false;
            }

            argument = arguments[next % arguments.Length];
            next++;
            return true;
        }
    }
}
