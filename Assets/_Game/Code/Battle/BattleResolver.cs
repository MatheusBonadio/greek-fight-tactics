using System.Collections.Generic;
using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public sealed class BattleResolver
    {
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();

        public Outcome Resolve(in Play play, BattleState state)
        {
            if (state.Status != BattleStatus.Ongoing)
            {
                return Outcome.Rejected(play.ActorId, play.Action, RejectionReason.BattleOver);
            }

            var actor = state.Find(play.ActorId);
            if (actor == null)
            {
                return Outcome.Rejected(play.ActorId, play.Action, RejectionReason.UnknownActor);
            }

            if (state.Queue.Current != actor.Id)
            {
                return Outcome.Rejected(play.ActorId, play.Action, RejectionReason.NotActorsTurn);
            }

            if (!IsAllowed(actor.Side, play.Action))
            {
                return Outcome.Rejected(play.ActorId, play.Action, RejectionReason.ActionNotAllowed);
            }

            if (actor.Side == Side.Player && state.IsLockedForPlayer(play.Action))
            {
                return Outcome.Rejected(play.ActorId, play.Action, RejectionReason.ActionLocked);
            }

            var outcome = play.Action switch
            {
                ActionId.Circulate => ResolveCirculate(play, actor, state),
                ActionId.Refute => ResolveRefute(play, actor, state),
                ActionId.Maieutics => ResolveMaieutics(play, actor, state),
                ActionId.Quote => ResolveQuote(play, actor, state),
                ActionId.Concede => ResolveConcede(actor, state),
                ActionId.Withdraw => ResolveWithdraw(actor, state),
                _ => ResolveArgue(play, actor, state)
            };

            if (!outcome.IsRejected && PositionalModifiers.IsCornered(state, actor.Position))
            {
                outcome.ActorCoherenceDelta -= state.Rules.CorneredCoherencePenalty;
            }

            AbilityRules.Apply(outcome, actor, state);
            return outcome;
        }

        public static bool IsAllowed(Side side, ActionId action) => side == Side.Player
            ? action != ActionId.Argue
            : action is ActionId.Circulate or ActionId.Concede or ActionId.Argue;

        public static int MaieuticsRange(BattleState state) =>
            state.Rules.MaieuticsRange + (state.Act == Act.Two ? state.Rules.ActTwoMaieuticsRangeBonus : 0);

        private Outcome ResolveCirculate(in Play play, Debater actor, BattleState state)
        {
            pathFinder.Reachable(state.Grid, actor.Position, state.Rules.MoveRange, reachable);
            var path = new List<Coord>();
            if (!reachable.Contains(play.Destination) || !pathFinder.FindPath(state.Grid, actor.Position, play.Destination, path))
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.DestinationUnreachable);
            }

            return new Outcome(actor.Id, play.Action) { Path = path };
        }

        private static Outcome ResolveRefute(in Play play, Debater actor, BattleState state)
        {
            if (!TryFindTarget(play, actor, state, state.Rules.RefuteRange, out var target, out var rejection))
            {
                return Outcome.Rejected(actor.Id, play.Action, rejection);
            }

            if (!state.CurrentArgument.HasValue)
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.NoArgumentToAnswer);
            }

            var argument = state.CurrentArgument.Value;
            if (!argument.IsRefutable)
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.ArgumentNotRefutable);
            }

            var outcome = new Outcome(actor.Id, play.Action) { TargetId = target.Id };
            if (play.ChosenFallacyId != argument.FallacyId)
            {
                outcome.Counterattack = true;
                outcome.ActorCoherenceDelta = -state.Rules.CounterattackDamage;
                return outcome;
            }

            var flankBonus = PositionalModifiers.IsFlanking(actor, target) ? state.Rules.FlankRefuteBonus : 0;
            outcome.Hit = true;
            outcome.TargetCoherenceDelta = -(state.Rules.RefuteDamage + flankBonus);
            outcome.PlayerAudienceDelta = AudienceTowards(actor, state.Rules.RefuteAudienceGain + AdjacencyBonus(actor, state));
            return outcome;
        }

        private static Outcome ResolveMaieutics(in Play play, Debater actor, BattleState state)
        {
            if (!TryFindTarget(play, actor, state, MaieuticsRange(state), out var target, out var rejection))
            {
                return Outcome.Rejected(actor.Id, play.Action, rejection);
            }

            if (!state.CurrentArgument.HasValue)
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.NoArgumentToAnswer);
            }

            var argument = state.CurrentArgument.Value;
            if (play.ChosenPremise < 0 || play.ChosenPremise >= argument.Premises.Count)
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.InvalidPremise);
            }

            var outcome = new Outcome(actor.Id, play.Action) { TargetId = target.Id, TurnCost = state.Rules.MaieuticsTurnCost };
            if (play.ChosenPremise != argument.WeakPremise)
            {
                outcome.PlayerAudienceDelta = -AudienceTowards(actor, state.Rules.MaieuticsMissAudienceLoss);
                return outcome;
            }

            var diagramBonus = PositionalModifiers.StandsOnDiagram(state, actor.Position) ? state.Rules.DiagramMaieuticsBonus : 0;
            outcome.Hit = true;
            outcome.TargetCoherenceDelta = -(state.Rules.MaieuticsDamage + diagramBonus);
            return outcome;
        }

        private static Outcome ResolveQuote(in Play play, Debater actor, BattleState state)
        {
            if (!state.TryGetQuote(play.QuoteId, out var quote, out var usesLeft) || usesLeft <= 0)
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.NoQuoteUses);
            }

            return new Outcome(actor.Id, play.Action)
            {
                QuoteId = quote.Id,
                ActorCoherenceDelta = quote.CoherenceDelta,
                PlayerAudienceDelta = AudienceTowards(actor, quote.AudienceDelta)
            };
        }

        private static Outcome ResolveConcede(Debater actor, BattleState state)
        {
            var actBonus = state.Act == Act.Two ? state.Rules.ActTwoConcedeBonus : 0;
            return new Outcome(actor.Id, ActionId.Concede)
            {
                ActorCoherenceDelta = state.Rules.ConcedeRecovery + actBonus,
                PlayerAudienceDelta = -AudienceTowards(actor, state.Rules.ConcedeAudienceLoss)
            };
        }

        private static Outcome ResolveWithdraw(Debater actor, BattleState state) =>
            new(actor.Id, ActionId.Withdraw)
            {
                EndsBattle = true,
                ReputationDelta = -state.Rules.WithdrawReputationPenalty
            };

        private static Outcome ResolveArgue(in Play play, Debater actor, BattleState state)
        {
            if (!play.PresentedArgument.HasValue)
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.MissingArgument);
            }

            var target = state.Find(play.TargetId);
            if (target == null || target.Side == actor.Side)
            {
                return Outcome.Rejected(actor.Id, play.Action, RejectionReason.UnknownTarget);
            }

            var outcome = new Outcome(actor.Id, play.Action)
            {
                TargetId = target.Id,
                PresentedArgument = play.PresentedArgument
            };

            if (actor.Profile == Profile.Sophist)
            {
                outcome.PlayerAudienceDelta = AudienceTowards(actor, state.Rules.SophistArgueAudienceShift + AdjacencyBonus(actor, state));
            }
            else
            {
                outcome.TargetCoherenceDelta = -state.Rules.PhilosopherArgueCoherenceDamage;
            }

            return outcome;
        }

        private static bool TryFindTarget(in Play play, Debater actor, BattleState state, int range, out Debater target, out RejectionReason rejection)
        {
            target = state.Find(play.TargetId);
            if (target == null || target.Side == actor.Side)
            {
                rejection = RejectionReason.UnknownTarget;
                return false;
            }

            if (PositionalModifiers.Distance(actor.Position, target.Position) > range)
            {
                rejection = RejectionReason.TargetOutOfRange;
                return false;
            }

            rejection = RejectionReason.None;
            return true;
        }

        private static int AdjacencyBonus(Debater actor, BattleState state) =>
            PositionalModifiers.AdjacentAudienceMembers(state, actor.Position) * state.Rules.CitizenAdjacencyAudienceBonus;

        private static int AudienceTowards(Debater actor, int amount) => actor.Side == Side.Player ? amount : -amount;
    }
}
