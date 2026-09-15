using System;

namespace Game.UI
{
    public sealed class DrainingBar
    {
        public const float DrainSeconds = 0.4f;
        public const float CriticalRatio = 0.25f;
        public const int FlashFrames = 1;

        private float startValue;
        private float elapsed = DrainSeconds;

        public DrainingBar(int max, int value)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "Bar maximum must be positive.");
            }

            Max = max;
            Target = Math.Clamp(value, 0, max);
            Displayed = Target;
            startValue = Target;
        }

        public int Max { get; }

        public int Target { get; private set; }

        public float Displayed { get; private set; }

        public float DisplayedRatio => Displayed / Max;

        public bool IsAnimating => elapsed < DrainSeconds;

        public bool IsCritical => Target < Max * CriticalRatio;

        public int FlashFramesRemaining { get; private set; }

        public void SetValue(int value)
        {
            var clamped = Math.Clamp(value, 0, Max);
            if (clamped == Target)
            {
                return;
            }

            if (clamped < Target)
            {
                FlashFramesRemaining = FlashFrames;
            }

            startValue = Displayed;
            Target = clamped;
            elapsed = 0f;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsAnimating)
            {
                return;
            }

            elapsed = Math.Min(DrainSeconds, elapsed + Math.Max(0f, deltaSeconds));
            var progress = elapsed / DrainSeconds;
            Displayed = startValue + (Target - startValue) * progress;
        }

        public bool ConsumeFlashFrame()
        {
            if (FlashFramesRemaining <= 0)
            {
                return false;
            }

            FlashFramesRemaining--;
            return true;
        }
    }
}
