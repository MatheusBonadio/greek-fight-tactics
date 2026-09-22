using System.Collections.Generic;

namespace Game.Core
{
    public static class ChallengeRule
    {
        public const int NoChallenger = -1;

        public static int ChallengerAt(Coord confirmed, GameMode mode, IReadOnlyList<Challenger> challengers)
        {
            if (mode != GameMode.Exploration || challengers == null)
            {
                return NoChallenger;
            }

            for (var index = 0; index < challengers.Count; index++)
            {
                var challenger = challengers[index];
                if (challenger.StandsAt(confirmed) && challenger.CanBeChallenged)
                {
                    return index;
                }
            }

            return NoChallenger;
        }
    }
}
