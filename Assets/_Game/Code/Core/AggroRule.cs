using System;

namespace Game.Core
{
    public static class AggroRule
    {
        public const int DefaultRadius = 3;

        public static bool IsTriggered(Coord player, Coord opponent, int radius = DefaultRadius) =>
            Math.Abs(player.X - opponent.X) + Math.Abs(player.Y - opponent.Y) <= radius;
    }
}
