using System;
using System.Linq;
using Game.Core;

namespace Game.Battle
{
    public sealed class ShuffledPremiseSource : IArgumentSource
    {
        private readonly IArgumentSource inner;
        private readonly Random random;

        public ShuffledPremiseSource(IArgumentSource inner, Random random)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool TryNext(Debater opponent, out Argument argument)
        {
            if (!inner.TryNext(opponent, out argument))
            {
                return false;
            }

            argument = Shuffle(argument, random);
            return true;
        }

        public static Argument Shuffle(in Argument argument, Random random)
        {
            var count = argument.Premises?.Count ?? 0;
            if (count < 2)
            {
                return argument;
            }

            var order = Enumerable.Range(0, count).ToArray();
            for (var index = count - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (order[index], order[swap]) = (order[swap], order[index]);
            }

            var source = argument.Premises;
            return new Argument(
                argument.Speech,
                argument.FallacyId,
                order.Select(position => source[position]),
                Array.IndexOf(order, argument.WeakPremise),
                argument.Tone);
        }
    }
}
