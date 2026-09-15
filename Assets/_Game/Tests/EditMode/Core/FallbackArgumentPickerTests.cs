using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class FallbackArgumentPickerTests
    {
        private const int Seed = 402;
        private const int Draws = 1000;

        [Test]
        public void TryNext_NeverRepeatsTheSameArgumentTwiceInARow()
        {
            var arguments = BuildArguments(3);
            var picker = new FallbackArgumentPicker(arguments, Seed);
            var previous = -1;

            for (var draw = 0; draw < Draws; draw++)
            {
                Assert.IsTrue(picker.TryNext(out var argument));
                var index = IndexOf(arguments, argument);
                Assert.AreNotEqual(previous, index, $"draw {draw}");
                previous = index;
            }
        }

        [Test]
        public void TryNext_EventuallyUsesEveryArgument()
        {
            var arguments = BuildArguments(5);
            var picker = new FallbackArgumentPicker(arguments, Seed);
            var seen = new HashSet<int>();

            for (var draw = 0; draw < Draws; draw++)
            {
                picker.TryNext(out var argument);
                seen.Add(IndexOf(arguments, argument));
            }

            Assert.AreEqual(arguments.Count, seen.Count);
        }

        [Test]
        public void TryNext_WithSameSeed_IsDeterministic()
        {
            var arguments = BuildArguments(4);
            var first = new FallbackArgumentPicker(arguments, Seed);
            var second = new FallbackArgumentPicker(arguments, Seed);

            for (var draw = 0; draw < 50; draw++)
            {
                first.TryNext(out var a);
                second.TryNext(out var b);
                Assert.AreEqual(a.Speech, b.Speech);
            }
        }

        [Test]
        public void TryNext_WithSingleArgument_RepeatsIt()
        {
            var arguments = BuildArguments(1);
            var picker = new FallbackArgumentPicker(arguments, Seed);

            Assert.IsTrue(picker.TryNext(out var first));
            Assert.IsTrue(picker.TryNext(out var second));
            Assert.AreEqual(first.Speech, second.Speech);
        }

        [Test]
        public void TryNext_WithNoArguments_ReturnsFalse()
        {
            var picker = new FallbackArgumentPicker(new List<Argument>(), Seed);

            Assert.IsFalse(picker.TryNext(out _));
        }

        private static List<Argument> BuildArguments(int count) =>
            Enumerable.Range(0, count)
                .Select(index => new Argument($"argumento {index}", null, new[] { "premissa" }, 0, ArgumentTones.Calm))
                .ToList();

        private static int IndexOf(List<Argument> arguments, Argument argument) =>
            arguments.FindIndex(candidate => candidate.Speech == argument.Speech);
    }
}
