namespace Game.Grid
{
    public sealed class CrowdRules
    {
        public const int DefaultMaximumCrowd = 12;
        public const int DefaultMinimumCrowd = 8;
        public const float DefaultRingRadius = 3.5f;
        public const float DefaultRingHalfWidth = 0.75f;
        public const float DefaultMaximumRingRadius = 5.5f;
        public const int DefaultDebaterClearance = 2;
        public const int DefaultMinimumApproachSteps = 3;
        public const int DefaultMaximumApproachSteps = 14;
        public const int DefaultMinimumExitSteps = 2;

        public int MaximumCrowd { get; set; } = DefaultMaximumCrowd;

        public int MinimumCrowd { get; set; } = DefaultMinimumCrowd;

        public float RingRadius { get; set; } = DefaultRingRadius;

        public float RingHalfWidth { get; set; } = DefaultRingHalfWidth;

        public float MaximumRingRadius { get; set; } = DefaultMaximumRingRadius;

        public int DebaterClearance { get; set; } = DefaultDebaterClearance;

        public int MinimumApproachSteps { get; set; } = DefaultMinimumApproachSteps;

        public int MaximumApproachSteps { get; set; } = DefaultMaximumApproachSteps;

        public int MinimumExitSteps { get; set; } = DefaultMinimumExitSteps;

        public bool RevealedSeatsOnly { get; set; } = true;

        public float InnerRadius => RingRadius - RingHalfWidth;

        public float OuterRadius => RingRadius + RingHalfWidth;
    }
}
