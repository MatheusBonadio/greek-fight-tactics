using Game.Core;

namespace Game.Battle
{
    public sealed class RefuteTutorial
    {
        private bool argumentExplained;
        private bool fallaciesHinted;
        private bool remind;

        public bool IsLearned { get; private set; }

        public static BattleRules Rules() => new()
        {
            CounterattackDamage = 0,
            CorneredCoherencePenalty = 0
        };

        public TutorialCue OnBattleStart() => TutorialCue.Intro;

        public TutorialCue OnPlayerTurn(BattleState state)
        {
            fallaciesHinted = false;
            if (IsLearned || state.Status != BattleStatus.Ongoing || !state.CurrentArgument.HasValue)
            {
                return TutorialCue.None;
            }

            if (!argumentExplained)
            {
                argumentExplained = true;
                remind = false;
                return TutorialCue.Argument;
            }

            if (!remind)
            {
                return TutorialCue.None;
            }

            remind = false;
            return TutorialCue.Reminder;
        }

        public TutorialCue OnFallaciesOpened()
        {
            if (IsLearned || fallaciesHinted)
            {
                return TutorialCue.None;
            }

            fallaciesHinted = true;
            return TutorialCue.Fallacies;
        }

        public TutorialCue OnOutcome(Outcome outcome, BattleState state)
        {
            if (IsLearned || outcome == null || outcome.IsRejected || outcome.ActorId != state.Player.Id)
            {
                return TutorialCue.None;
            }

            if (outcome.Action != ActionId.Refute)
            {
                remind = true;
                return TutorialCue.None;
            }

            if (outcome.Hit)
            {
                IsLearned = true;
            }

            if (state.Status != BattleStatus.Ongoing)
            {
                return TutorialCue.None;
            }

            return outcome.Hit ? TutorialCue.Hit : TutorialCue.Miss;
        }

        public TutorialCue OnBattleOver(BattleState state) =>
            state.Status == BattleStatus.PlayerWon ? TutorialCue.Victory : TutorialCue.None;
    }
}
