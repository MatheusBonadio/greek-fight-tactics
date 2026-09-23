namespace Game.Core
{
    public static class BattleAnimations
    {
        public const string Idle = "idle";
        public const string IdleBreath = "idle_breath";
        public const string Walk = "walk";
        public const string Talk = "talk";
        public const string Think = "think";
        public const string Argue = "argue";
        public const string Refute = "refute";
        public const string Maieutics = "maieutics";
        public const string Quote = "quote";
        public const string Concede = "concede";
        public const string Withdraw = "withdraw";
        public const string CoherenceDamage = "coherence_damage";
        public const string Defeat = "defeat";
        public const string Victory = "victory";
        public const string Listen = "listen";
        public const string Invert = "invert";

        public static string For(ActionId action) => action switch
        {
            ActionId.Circulate => Walk,
            ActionId.Refute => Refute,
            ActionId.Maieutics => Maieutics,
            ActionId.Quote => Quote,
            ActionId.Concede => Concede,
            ActionId.Withdraw => Withdraw,
            ActionId.Argue => Argue,
            _ => null
        };

        public static string ForAbility(AbilityId ability) => ability switch
        {
            AbilityId.Listening => Listen,
            AbilityId.Inversion => Invert,
            _ => null
        };
    }
}
