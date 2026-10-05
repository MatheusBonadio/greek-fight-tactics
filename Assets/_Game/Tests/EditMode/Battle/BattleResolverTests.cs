using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using NUnit.Framework;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class BattleResolverTests
    {
        private BattleResolver resolver;

        [SetUp]
        public void SetUp()
        {
            resolver = new BattleResolver();
        }

        [Test]
        public void Resolve_NeverMutatesTheState()
        {
            var state = CreateWithArgument(RefutableArgument());
            var before = Snapshot(state);

            resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);
            resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state);
            resolver.Resolve(Play.Circulate(PlayerId, new Coord(2, 4)), state);
            resolver.Resolve(Play.Quote(PlayerId, QuoteId), state);
            resolver.Resolve(Play.Withdraw(PlayerId), state);

            Assert.AreEqual(before, Snapshot(state));
        }

        [Test]
        public void Refute_WithTheRightFallacy_DamagesAndWinsAudience()
        {
            var state = CreateWithArgument(SoundArgument());
            var audience = state.PlayerAudience;

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);
            state.Apply(outcome);

            Assert.IsTrue(outcome.Hit);
            Assert.AreEqual(50 - state.Rules.RefuteDamage, state.Opponent.Coherence);
            Assert.AreEqual(audience + state.Rules.RefuteAudienceGain, state.PlayerAudience);
        }

        [Test]
        public void Refute_WithTheWrongFallacy_TriggersCounterattack()
        {
            var state = CreateWithArgument(RefutableArgument());

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, "straw_man"), state);
            state.Apply(outcome);

            Assert.IsFalse(outcome.Hit);
            Assert.IsTrue(outcome.Counterattack);
            Assert.AreEqual(60 - state.Rules.CounterattackDamage, state.Player.Coherence);
            Assert.AreEqual(50, state.Opponent.Coherence);
        }

        [Test]
        public void Refute_AgainstAParadox_IsRejectedWithoutSpendingTheTurn()
        {
            var state = CreateWithArgument(Paradox(), Act.Two);
            var turn = state.Turn;

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);
            state.Apply(outcome);

            Assert.AreEqual(RejectionReason.ArgumentNotRefutable, outcome.Rejection);
            Assert.AreEqual(turn, state.Turn);
            Assert.AreEqual(PlayerId, state.Queue.Current);
            Assert.AreEqual(RejectionReason.ArgumentNotRefutable, state.Log.Last().Rejection);
        }

        [Test]
        public void Refute_BeforeAnyArgument_IsRejected()
        {
            var state = Create();

            Assert.AreEqual(RejectionReason.NoArgumentToAnswer, resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state).Rejection);
        }

        [Test]
        public void Maieutics_OnTheWeakPremise_DealsHeavyDamageAndCostsThreeTurns()
        {
            var state = CreateWithArgument(RefutableArgument());

            var outcome = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state);
            state.Apply(outcome);

            Assert.IsTrue(outcome.Hit);
            Assert.AreEqual(3, BattleResolver.MaieuticsTurnCost(state));
            Assert.AreEqual(BattleResolver.MaieuticsTurnCost(state), outcome.TurnCost);
            Assert.AreEqual(2, BattleResolver.MaieuticsTurnCost(CreateWithArgument(Paradox(), Act.Two)), "in the second act Kleia has the method, and paradoxes leave her nothing else");
            Assert.AreEqual(50 - state.Rules.MaieuticsDamage, state.Opponent.Coherence);
            CollectionAssert.AreEqual(new[] { OpponentId, OpponentId, OpponentId, PlayerId }, state.Queue.Preview(4));
        }

        [Test]
        public void Maieutics_OnAStrongPremise_CostsAudienceAndOpensACounterattack()
        {
            var state = CreateWithArgument(RefutableArgument());
            var audience = state.PlayerAudience;

            var outcome = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, 0), state);
            state.Apply(outcome);

            Assert.IsTrue(outcome.Counterattack, "the wrong question hands the opponent his cue");
            Assert.AreEqual(50, state.Opponent.Coherence);
            Assert.AreEqual(audience - state.Rules.MaieuticsMissAudienceLoss, state.PlayerAudience);
            Assert.AreEqual(60 - state.Rules.MaieuticsMissCoherenceLoss, state.Player.Coherence, "a wrong question exposes Kleia too");
        }

        [Test]
        public void AnArgumentRestingOnAWeakPremise_FallsOnlyToAQuestion()
        {
            var state = CreateWithArgument(RefutableArgument());

            var refutation = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);
            Assert.IsFalse(refutation.Hit, "naming the fallacy does not knock down a false premise");
            Assert.IsTrue(refutation.PremiseWasWeak, "the narration can say the premise was the problem");
            Assert.IsTrue(refutation.Counterattack);
            Assert.AreEqual(0, refutation.TargetCoherenceDelta);

            var wrongFallacy = resolver.Resolve(Play.Refute(PlayerId, OpponentId, "straw_man"), state);
            Assert.IsFalse(wrongFallacy.PremiseWasWeak, "a wrong fallacy gives nothing away");

            Assert.AreEqual(ActionId.Maieutics, AnswerRule.Expected(state));
            Assert.IsTrue(resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state).Hit);
        }

        [Test]
        public void AnArgumentWithSoundPremises_FallsOnlyToARefutation()
        {
            var sound = new Argument(RefutableArgument().Speech, Fallacy, RefutableArgument().Premises, Argument.NoWeakPremise, ArgumentTones.Mocking);
            var state = CreateWithArgument(sound);

            for (var premise = 0; premise < sound.Premises.Count; premise++)
            {
                var question = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, premise), state);
                Assert.IsFalse(question.Hit, "no premise gives way when the error is in the reasoning");
                Assert.IsTrue(question.PremisesHeld);
                Assert.IsTrue(question.Counterattack);
                Assert.AreEqual(0, question.TargetCoherenceDelta);
            }

            var refutation = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);
            Assert.IsTrue(refutation.Hit);
            Assert.Less(refutation.TargetCoherenceDelta, 0);
            Assert.AreEqual(ActionId.Refute, AnswerRule.Expected(state));
        }

        [Test]
        public void TheTurnMaieuticsSkips_LeavesTheNextArgumentStandingAgainstKleia()
        {
            var state = CreateWithArgument(RefutableArgument());
            state.Apply(resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state));

            var answered = resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state);
            state.Apply(answered);
            Assert.IsFalse(answered.ArgumentStood, "Kleia did answer the argument before the question");

            for (var lost = 1; lost < BattleResolver.MaieuticsTurnCost(state); lost++)
            {
                var coherence = state.Player.Coherence;
                var stood = resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state);
                state.Apply(stood);

                Assert.IsTrue(stood.ArgumentStood, "each turn she lost left the previous argument without an answer");
                Assert.AreEqual(coherence - state.Rules.StandingArgumentDamage, state.Player.Coherence);
                Assert.IsTrue(state.Log.Last().ArgumentStood, "the log tells where the damage came from");
            }

            Assert.AreEqual(PlayerId, state.Queue.Current);
        }

        [Test]
        public void AnyMoveByKleia_KeepsTheNextArgumentFromStanding()
        {
            var state = CreateWithArgument(RefutableArgument());
            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));

            var next = resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state);

            Assert.IsFalse(state.IsArgumentStanding);
            Assert.IsFalse(next.ArgumentStood);
            Assert.AreEqual(0, next.TargetCoherenceDelta, "a sophist's argument only sways the crowd");
        }

        [Test]
        public void AnAnsweredArgument_CannotBeAnsweredAgainUntilTheNextOne()
        {
            var state = CreateWithArgument(RefutableArgument());
            state.Apply(resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, 0), state));
            for (var turn = 0; turn < BattleResolver.MaieuticsTurnCost(state); turn++)
            {
                state.Apply(resolver.Resolve(Play.Concede(OpponentId), state));
            }

            Assert.AreEqual(PlayerId, state.Queue.Current);
            Assert.IsTrue(state.IsArgumentAnswered);
            Assert.IsFalse(state.HasOpenArgument);
            Assert.AreEqual(RejectionReason.ArgumentAlreadyAnswered, resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state).Rejection,
                "a missed question already told Kleia which premise holds");
            Assert.AreEqual(RejectionReason.ArgumentAlreadyAnswered, resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state).Rejection);

            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));
            state.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state));

            Assert.IsTrue(state.HasOpenArgument, "a new argument opens the floor again");
            Assert.IsFalse(resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state).IsRejected);
        }

        [Test]
        public void AWrongRefutation_LeavesTheArgumentOnTheTable()
        {
            var state = CreateWithArgument(RefutableArgument());
            state.Apply(resolver.Resolve(Play.Refute(PlayerId, OpponentId, "straw_man"), state));

            Assert.IsTrue(state.HasOpenArgument);
        }

        [Test]
        public void Maieutics_WorksAgainstParadoxes()
        {
            var state = CreateWithArgument(Paradox(), Act.Two);

            var outcome = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state);

            Assert.IsTrue(outcome.Hit);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Maieutics_WithAPremiseOutsideTheArgument_IsRejected(int premise)
        {
            var state = CreateWithArgument(RefutableArgument());

            Assert.AreEqual(RejectionReason.InvalidPremise, resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, premise), state).Rejection);
        }

        [Test]
        public void ActTwo_ExtendsMaieuticsRangeAndConcedeRecovery()
        {
            var actOne = CreateWithArgument(RefutableArgument());
            var actTwo = CreateWithArgument(RefutableArgument(), Act.Two);

            Assert.AreEqual(actOne.Rules.MaieuticsRange + actOne.Rules.ActTwoMaieuticsRangeBonus, BattleResolver.MaieuticsRange(actTwo));

            var actOneConcede = resolver.Resolve(Play.Concede(PlayerId), actOne);
            var actTwoConcede = resolver.Resolve(Play.Concede(PlayerId), actTwo);
            Assert.AreEqual(actOne.Rules.ActTwoConcedeBonus, actTwoConcede.ActorCoherenceDelta - actOneConcede.ActorCoherenceDelta);
        }

        [Test]
        public void TargetBeyondRange_IsRejected()
        {
            var state = CreateWithArgument(RefutableArgument());
            state.Rules.MaieuticsRange = 1;

            Assert.AreEqual(RejectionReason.TargetOutOfRange, resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state).Rejection);
        }

        [Test]
        public void Concede_RecoversCoherenceAndGivesAudienceAway()
        {
            var state = CreateWithArgument(SoundArgument());
            state.Apply(resolver.Resolve(Play.Refute(PlayerId, OpponentId, "straw_man"), state));
            state.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state));
            var coherence = state.Player.Coherence;
            var audience = state.PlayerAudience;

            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));

            Assert.AreEqual(System.Math.Min(60, coherence + state.Rules.ConcedeRecovery), state.Player.Coherence);
            Assert.AreEqual(audience - state.Rules.ConcedeAudienceLoss, state.PlayerAudience);
        }

        [Test]
        public void Quote_ConsumesAUseAndAppliesItsFixedEffect()
        {
            var state = Create();
            var audience = state.PlayerAudience;

            state.Apply(resolver.Resolve(Play.Quote(PlayerId, QuoteId), state));

            Assert.IsTrue(state.TryGetQuote(QuoteId, out _, out var usesLeft));
            Assert.AreEqual(0, usesLeft);
            Assert.AreEqual(audience + 5, state.PlayerAudience);

            state.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state));
            Assert.AreEqual(RejectionReason.NoQuoteUses, resolver.Resolve(Play.Quote(PlayerId, QuoteId), state).Rejection);
        }

        [Test]
        public void Circulate_MovesAlongThePathAndUpdatesOccupancy()
        {
            var state = Create();
            var destination = new Coord(2, 5);

            var outcome = resolver.Resolve(Play.Circulate(PlayerId, destination), state);
            state.Apply(outcome);

            Assert.IsFalse(outcome.IsRejected);
            Assert.AreEqual(destination, state.Player.Position);
            Assert.AreEqual(PlayerId, state.Grid[destination].OccupantId);
            Assert.AreEqual(TileData.NoOccupant, state.Grid[PlayerStart].OccupantId);
            Assert.AreEqual(Facing.NorthWest, state.Player.Facing);
        }

        [Test]
        public void Circulate_BeyondMoveRangeOrOntoAnOpponent_IsRejected()
        {
            var state = Create();

            Assert.AreEqual(RejectionReason.DestinationUnreachable, resolver.Resolve(Play.Circulate(PlayerId, new Coord(2, 7)), state).Rejection);
            Assert.AreEqual(RejectionReason.DestinationUnreachable, resolver.Resolve(Play.Circulate(PlayerId, OpponentStart), state).Rejection);
        }

        [Test]
        public void Withdraw_EndsTheBattleWithReputationPenalty()
        {
            var state = Create();

            state.Apply(resolver.Resolve(Play.Withdraw(PlayerId), state));

            Assert.AreEqual(BattleStatus.PlayerWithdrew, state.Status);
            Assert.AreEqual(-state.Rules.WithdrawReputationPenalty, state.ReputationDelta);
            Assert.AreEqual(RejectionReason.BattleOver, resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state).Rejection);
        }

        [Test]
        public void ActingOutOfTurn_OrWithAnOpponentOnlyAction_IsRejected()
        {
            var state = Create();

            Assert.AreEqual(RejectionReason.NotActorsTurn, resolver.Resolve(Play.Concede(OpponentId), state).Rejection);
            Assert.AreEqual(RejectionReason.ActionNotAllowed, resolver.Resolve(Play.Argue(PlayerId, OpponentId, RefutableArgument()), state).Rejection);
        }

        [Test]
        public void Argue_BySophistShiftsAudience_ByPhilosopherDamagesCoherence()
        {
            var sophist = Create(opponentProfile: Profile.Sophist, playerInitiative: 1, opponentInitiative: 9);
            var philosopher = Create(opponentProfile: Profile.Philosopher, playerInitiative: 1, opponentInitiative: 9);

            sophist.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), sophist));
            philosopher.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, Paradox()), philosopher));

            Assert.AreEqual(40 - sophist.Rules.SophistArgueAudienceShift, sophist.PlayerAudience);
            Assert.AreEqual(60, sophist.Player.Coherence);
            Assert.AreEqual(40, philosopher.PlayerAudience);
            Assert.AreEqual(60 - philosopher.Rules.PhilosopherArgueCoherenceDamage, philosopher.Player.Coherence);
            Assert.IsTrue(sophist.CurrentArgument.HasValue);
        }

        [Test]
        public void Victory_WhenOpponentCoherenceHitsZero()
        {
            var state = CreateWithArgument(RefutableArgument());
            state.Rules.MaieuticsDamage = 100;

            state.Apply(resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state));

            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
        }

        [Test]
        public void Victory_WhenAudienceReachesOneHundred()
        {
            var state = Create(initialPlayerAudience: 95);

            state.Apply(resolver.Resolve(Play.Quote(PlayerId, QuoteId), state));

            Assert.AreEqual(BattleRules.MaxAudience, state.PlayerAudience);
            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
        }

        [Test]
        public void Defeat_WhenPlayerCoherenceHitsZero()
        {
            var state = CreateWithArgument(RefutableArgument());
            state.Rules.CounterattackDamage = 100;

            state.Apply(resolver.Resolve(Play.Refute(PlayerId, OpponentId, "straw_man"), state));

            Assert.AreEqual(0, state.Player.Coherence);
            Assert.AreEqual(BattleStatus.PlayerLost, state.Status);
        }

        [Test]
        public void TurnLimit_StopsTheBattle()
        {
            var rules = new BattleRules { TurnLimit = 2 };
            var state = Create(rules: rules);

            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));
            state.Apply(resolver.Resolve(Play.Concede(OpponentId), state));

            Assert.AreEqual(BattleStatus.TurnLimitReached, state.Status);
        }

        [Test]
        public void Log_RecordsEveryResolutionWithResultingBars()
        {
            var state = CreateWithArgument(SoundArgument());

            state.Apply(resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state));

            Assert.AreEqual(2, state.Log.Count);
            var entry = state.Log.Last();
            Assert.AreEqual(ActionId.Refute, entry.Action);
            Assert.IsTrue(entry.Hit);
            Assert.AreEqual(state.Opponent.Coherence, entry.OpponentCoherence);
            Assert.AreEqual(state.PlayerAudience, entry.PlayerAudience);
            Assert.AreEqual($"turn-2-actor-{PlayerId}-refute", entry.LogId);
        }

        private static string Snapshot(BattleState state) =>
            string.Join("|",
                state.Player.Coherence, state.Opponent.Coherence, state.PlayerAudience, state.Turn, state.Status,
                state.Player.Position, state.Opponent.Position, state.Queue.Current, state.Log.Count, state.ReputationDelta,
                state.TryGetQuote(QuoteId, out _, out var uses) ? uses : -1,
                state.Grid[PlayerStart].OccupantId);
    }
}
