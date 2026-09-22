using Game.Core;

namespace Game.UI
{
    public interface IDuelChallenges
    {
        ActorView ChallengerAt(Coord cell);

        void Challenge(ActorView challenger);
    }
}
