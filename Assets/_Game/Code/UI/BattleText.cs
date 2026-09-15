using Game.Battle;
using Game.Core;

namespace Game.UI
{
    public static class BattleText
    {
        public const string PlayerNameKey = "battle.player_name";
        public const string IntroKey = "battle.intro";
        public const string RefuteHitKey = "battle.refute_hit";
        public const string RefuteMissKey = "battle.refute_miss";
        public const string MaieuticsHitKey = "battle.maieutics_hit";
        public const string MaieuticsMissKey = "battle.maieutics_miss";
        public const string ConcedeKey = "battle.concede";
        public const string QuoteKey = "battle.quote";
        public const string VictoryKey = "battle.victory";
        public const string DefeatKey = "battle.defeat";
        public const string WithdrewKey = "battle.withdrew";
        public const string StalemateKey = "battle.stalemate";

        public static string OutcomeKey(Outcome outcome) => outcome.Action switch
        {
            ActionId.Refute => outcome.Hit ? RefuteHitKey : RefuteMissKey,
            ActionId.Maieutics => outcome.Hit ? MaieuticsHitKey : MaieuticsMissKey,
            ActionId.Concede => ConcedeKey,
            ActionId.Quote => QuoteKey,
            _ => null
        };

        public static string StatusKey(BattleStatus status) => status switch
        {
            BattleStatus.PlayerWon => VictoryKey,
            BattleStatus.PlayerLost => DefeatKey,
            BattleStatus.PlayerWithdrew => WithdrewKey,
            _ => StalemateKey
        };
    }
}
