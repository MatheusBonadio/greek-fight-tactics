using System;

namespace Game.Grid
{
    public static class SelectionPulse
    {
        public const double PeriodSeconds = 0.5;
        public const int FrameCount = 2;

        public static int FrameAt(double seconds) =>
            seconds <= 0 ? 0 : (int)(Math.Floor(seconds / PeriodSeconds) % FrameCount);
    }
}
