using Game.Core;

namespace Game.Battle
{
    public sealed class BattleLogEntry
    {
        internal BattleLogEntry(int turn, Outcome outcome, int playerCoherence, int opponentCoherence, int playerAudience, BattleStatus status)
        {
            Turn = turn;
            ActorId = outcome.ActorId;
            Action = outcome.Action;
            Rejection = outcome.Rejection;
            TargetId = outcome.TargetId;
            Hit = outcome.Hit;
            Counterattack = outcome.Counterattack;
            ActorCoherenceDelta = outcome.ActorCoherenceDelta;
            TargetCoherenceDelta = outcome.TargetCoherenceDelta;
            PlayerAudienceDelta = outcome.PlayerAudienceDelta;
            PlayerCoherence = playerCoherence;
            OpponentCoherence = opponentCoherence;
            PlayerAudience = playerAudience;
            Status = status;
            TriggeredAbility = outcome.TriggeredAbility;
            LogId = $"turn-{turn}-actor-{outcome.ActorId}-{outcome.Action}".ToLowerInvariant();
        }

        public string LogId { get; }

        public int Turn { get; }

        public int ActorId { get; }

        public ActionId Action { get; }

        public RejectionReason Rejection { get; }

        public int TargetId { get; }

        public bool Hit { get; }

        public bool Counterattack { get; }

        public int ActorCoherenceDelta { get; }

        public int TargetCoherenceDelta { get; }

        public int PlayerAudienceDelta { get; }

        public int PlayerCoherence { get; }

        public int OpponentCoherence { get; }

        public int PlayerAudience { get; }

        public BattleStatus Status { get; }

        public AbilityId? TriggeredAbility { get; }
    }
}
