using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Battle
{
    public sealed class Outcome
    {
        internal Outcome(int actorId, ActionId action)
        {
            ActorId = actorId;
            Action = action;
        }

        public int ActorId { get; }

        public ActionId Action { get; }

        public RejectionReason Rejection { get; internal set; }

        public bool IsRejected => Rejection != RejectionReason.None;

        public int TargetId { get; internal set; } = Play.NoTarget;

        public bool Hit { get; internal set; }

        public bool Counterattack { get; internal set; }

        public bool Deflected { get; internal set; }

        public int ActorCoherenceDelta { get; internal set; }

        public int TargetCoherenceDelta { get; internal set; }

        public int PlayerAudienceDelta { get; internal set; }

        public IReadOnlyList<Coord> Path { get; internal set; } = Array.Empty<Coord>();

        public bool Moves => Path.Count > 0;

        public int TurnCost { get; internal set; } = 1;

        public int ReputationDelta { get; internal set; }

        public bool EndsBattle { get; internal set; }

        public string QuoteId { get; internal set; }

        public Argument? PresentedArgument { get; internal set; }

        public AbilityId? TriggeredAbility { get; internal set; }

        internal static Outcome Rejected(int actorId, ActionId action, RejectionReason reason) =>
            new(actorId, action) { Rejection = reason, TurnCost = 0 };
    }
}
