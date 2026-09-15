using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static class ArgumentTones
    {
        public const string Arrogant = "arrogant";
        public const string Mocking = "mocking";
        public const string Cunning = "cunning";
        public const string Theatrical = "theatrical";
        public const string Calm = "calm";
        public const string Solemn = "solemn";
        public const string Curious = "curious";
        public const string Melancholic = "melancholic";

        public static readonly IReadOnlyList<string> Sophist = new[] { Arrogant, Mocking, Cunning, Theatrical };
        public static readonly IReadOnlyList<string> Philosopher = new[] { Calm, Solemn, Curious, Melancholic };
        public static readonly IReadOnlyList<string> All = Sophist.Concat(Philosopher).ToArray();

        public static bool IsKnown(string tone) => tone != null && All.Contains(tone);

        public static IReadOnlyList<string> For(Profile profile) => profile == Profile.Sophist ? Sophist : Philosopher;
    }
}
