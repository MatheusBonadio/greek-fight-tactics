using System.Collections.Generic;

namespace Game.Core
{
    public sealed class MusicResume
    {
        public const float WindowSeconds = 300f;

        private readonly Dictionary<string, (float Position, float LeftAt)> stops = new();

        public void Remember(string trackId, float position, float now)
        {
            if (!string.IsNullOrEmpty(trackId))
            {
                stops[trackId] = (position, now);
            }
        }

        public float StartFor(string trackId, float length, float now)
        {
            if (string.IsNullOrEmpty(trackId) || length <= 0f || !stops.TryGetValue(trackId, out var stop))
            {
                return 0f;
            }

            if (now - stop.LeftAt > WindowSeconds || stop.Position < 0f)
            {
                return 0f;
            }

            return stop.Position % length;
        }
    }
}
