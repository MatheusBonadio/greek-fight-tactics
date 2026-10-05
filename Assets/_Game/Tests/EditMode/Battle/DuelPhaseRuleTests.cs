using System;
using System.Linq;
using Game.Battle;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Battle
{
    public sealed class DuelPhaseRuleTests
    {
        private const int OpponentMaxCoherence = 50;
        private const int FirstFloor = 40;
        private const int SecondFloor = 20;
        private const string PersonalFallacy = "ad_hominem";
        private const string DilemmaFallacy = "false_dilemma";
        private const string FearFallacy = "appeal_to_emotion";
        private const int Seed = 402;

        private readonly BattleResolver resolver = new();

        [Test]
        public void Phase_FollowsTheOpponentsCoherenceDownThroughEachFloor()
        {
            var duel = StreetTrial();

            Assert.AreEqual(0, duel.IndexFor(OpponentMaxCoherence));
            Assert.AreEqual(0, duel.IndexFor(FirstFloor + 1));
            Assert.AreEqual(1, duel.IndexFor(FirstFloor), "reaching the floor ends the phase");
            Assert.AreEqual(1, duel.IndexFor(SecondFloor + 1));
            Assert.AreEqual(2, duel.IndexFor(SecondFloor));
            Assert.AreEqual(2, duel.IndexFor(1));
            Assert.AreEqual(2, duel.IndexFor(0), "the last phase is the one that ends the duel");
            CollectionAssert.AreEqual(new[] { ActionId.Concede, ActionId.Refute, ActionId.Maieutics }, duel.Phases.Select(phase => phase.Counter));
        }

        [Test]
        public void Problems_RejectPhasesThatCannotBeWonOrThatSkipTheirFloors()
        {
            var ad = new[] { Argument(PersonalFallacy) };
            Assert.IsEmpty(PhasedDuel.Problems(StreetTrial().Phases));
            Assert.IsNotEmpty(PhasedDuel.Problems(Array.Empty<DuelPhase>()), "no phase");
            Assert.IsNotEmpty(PhasedDuel.Problems(new[] { new DuelPhase("walk", ActionId.Circulate, 0, ad) }), "moving is not an answer");
            Assert.IsNotEmpty(PhasedDuel.Problems(new[] { new DuelPhase("a", ActionId.Concede, 20, ad), new DuelPhase("b", ActionId.Refute, 30, ad), new DuelPhase("c", ActionId.Maieutics, 0, ad) }), "floors must go down");
            Assert.IsNotEmpty(PhasedDuel.Problems(new[] { new DuelPhase("a", ActionId.Concede, 20, ad) }), "the last floor must end the duel");
            Assert.IsNotEmpty(PhasedDuel.Problems(new[] { new DuelPhase("a", ActionId.Concede, 0, Array.Empty<Argument>()) }), "a phase must have something to say");
            Assert.IsNotEmpty(PhasedDuel.Problems(new[] { new DuelPhase("a", ActionId.Refute, 0, new[] { BattleFactory.Paradox() }) }), "a paradox cannot be refuted");
            Assert.IsNotEmpty(PhasedDuel.Problems(new[] { new DuelPhase("a", ActionId.Concede, 20, ad), new DuelPhase("a", ActionId.Refute, 0, ad) }), "phase ids are unique");
            Assert.Throws<ArgumentException>(() => new PhasedDuel(new[] { new DuelPhase("a", ActionId.Quote, 0, ad) }));
        }

        [Test]
        public void PersonalAttack_IsDisarmedOnlyByConceding()
        {
            var duel = StreetTrial();
            var state = StateArguing(duel, duel.Phases[0].Arguments[0]);
            var audience = state.PlayerAudience;

            var refute = resolver.Resolve(Play.Refute(BattleFactory.PlayerId, BattleFactory.OpponentId, PersonalFallacy), state);
            Assert.IsTrue(refute.Deflected, "naming the right fallacy does not answer a personal attack");
            Assert.IsFalse(refute.Hit);
            Assert.IsTrue(refute.Counterattack);
            Assert.AreEqual(-state.Rules.CounterattackDamage, refute.ActorCoherenceDelta);
            Assert.AreEqual(0, refute.TargetCoherenceDelta);

            var maieutics = resolver.Resolve(Play.Maieutics(BattleFactory.PlayerId, BattleFactory.OpponentId, BattleFactory.WeakPremise), state);
            Assert.IsTrue(maieutics.Deflected, "the right question does not answer a personal attack either");
            Assert.AreEqual(0, maieutics.TargetCoherenceDelta);
            Assert.AreEqual(-state.Rules.MaieuticsMissAudienceLoss, maieutics.PlayerAudienceDelta);

            var concede = resolver.Resolve(Play.Concede(BattleFactory.PlayerId), state);
            Assert.IsTrue(concede.Hit, "conceding the fact takes the weapon away");
            Assert.IsFalse(concede.Deflected);
            Assert.AreEqual(BattleFactory.OpponentId, concede.TargetId);
            Assert.AreEqual(FirstFloor - OpponentMaxCoherence, concede.TargetCoherenceDelta);
            Assert.AreEqual(state.Rules.PhaseCounterAudienceGain, concede.PlayerAudienceDelta);
            Assert.AreEqual(state.Rules.ConcedeRecovery, concede.ActorCoherenceDelta, "conceding still recovers coherence");

            state.Apply(concede);
            Assert.AreEqual(FirstFloor, state.Opponent.Coherence);
            Assert.AreEqual(1, duel.IndexFor(state.Opponent.Coherence), "the personal attack is over");
            Assert.AreEqual(audience + state.Rules.PhaseCounterAudienceGain, state.PlayerAudience);
            Assert.AreEqual(BattleStatus.Ongoing, state.Status);
        }

        [Test]
        public void FalseDilemma_IsAnsweredOnlyByRefutingTheMissingThirdWay()
        {
            var duel = StreetTrial();
            var state = StateInPhase(duel, 1);
            var coherence = state.Opponent.Coherence;

            var concede = resolver.Resolve(Play.Concede(BattleFactory.PlayerId), state);
            Assert.IsFalse(concede.Hit, "conceding a false dilemma is just conceding");
            Assert.AreEqual(0, concede.TargetCoherenceDelta);
            Assert.AreEqual(-state.Rules.ConcedeAudienceLoss, concede.PlayerAudienceDelta);

            var wrong = resolver.Resolve(Play.Refute(BattleFactory.PlayerId, BattleFactory.OpponentId, PersonalFallacy), state);
            Assert.IsFalse(wrong.Hit);
            Assert.IsFalse(wrong.Deflected, "a wrong fallacy is a plain miss");
            Assert.IsTrue(wrong.Counterattack);

            var maieutics = resolver.Resolve(Play.Maieutics(BattleFactory.PlayerId, BattleFactory.OpponentId, BattleFactory.WeakPremise), state);
            Assert.IsTrue(maieutics.Deflected);

            var refute = resolver.Resolve(Play.Refute(BattleFactory.PlayerId, BattleFactory.OpponentId, DilemmaFallacy), state);
            Assert.IsTrue(refute.Hit);
            Assert.AreEqual(SecondFloor - coherence, refute.TargetCoherenceDelta, "the refutation stops at the floor of the phase");
            state.Apply(refute);
            Assert.AreEqual(2, duel.IndexFor(state.Opponent.Coherence));
        }

        [Test]
        public void PoliticalFear_FallsOnlyToMaieuticsOnTheWeakPremise()
        {
            var duel = StreetTrial();
            var state = StateInPhase(duel, 2);

            var refute = resolver.Resolve(Play.Refute(BattleFactory.PlayerId, BattleFactory.OpponentId, FearFallacy), state);
            Assert.IsTrue(refute.Deflected, "pointing out the appeal to fear does not calm the crowd");
            Assert.AreEqual(0, refute.TargetCoherenceDelta);

            var wrongPremise = resolver.Resolve(Play.Maieutics(BattleFactory.PlayerId, BattleFactory.OpponentId, 0), state);
            Assert.IsFalse(wrongPremise.Hit);
            Assert.IsFalse(wrongPremise.Deflected);

            var maieutics = resolver.Resolve(Play.Maieutics(BattleFactory.PlayerId, BattleFactory.OpponentId, BattleFactory.WeakPremise), state);
            Assert.IsTrue(maieutics.Hit);
            state.Apply(maieutics);
            Assert.AreEqual(0, state.Opponent.Coherence);
            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
            Assert.IsTrue(DuelVictoryRule.TryClassify(state, out var victory));
            Assert.AreEqual(DuelVictory.Maieutics, victory, "the trial is won by the question");
        }

        [Test]
        public void OpponentsOwnPenalties_NeverPushHimIntoTheNextPhase()
        {
            var duel = StreetTrial();
            var rules = duel.Rules();
            rules.CorneredBlockedNeighbours = 0;
            rules.MaieuticsMissCoherenceLoss = 0;
            rules.StandingArgumentDamage = 0;
            var state = BattleFactory.Create(rules: rules, playerInitiative: 5, opponentInitiative: 10);
            var opponent = new PhasedOpponent(duel);

            for (var turn = 0; turn < 12; turn++)
            {
                var actor = state.Find(state.Queue.Current);
                var play = actor.Side == Side.Opponent ? opponent.Decide(state, actor, null) : Play.Maieutics(actor.Id, BattleFactory.OpponentId, 0);
                var outcome = resolver.Resolve(play, state);
                Assert.IsFalse(outcome.IsRejected, $"{play.Action} was rejected: {outcome.Rejection}");
                state.Apply(outcome);
            }

            Assert.Greater(state.Opponent.Coherence, FirstFloor, "being cornered wears him down, but only the counter ends a phase");
            Assert.AreEqual(0, duel.IndexFor(state.Opponent.Coherence));
        }

        [Test]
        public void Audience_NeverWinsAPhasedDuelOnItsOwn()
        {
            var phased = BattleFactory.Create(initialPlayerAudience: BattleRules.MaxAudience - 1, rules: StreetTrial().Rules());
            phased.Apply(resolver.Resolve(Play.Quote(BattleFactory.PlayerId, BattleFactory.QuoteId), phased));
            Assert.AreEqual(BattleRules.MaxAudience, phased.PlayerAudience);
            Assert.AreEqual(BattleStatus.Ongoing, phased.Status, "the crowd cannot skip the phases");

            var normal = BattleFactory.Create(initialPlayerAudience: BattleRules.MaxAudience - 1);
            normal.Apply(resolver.Resolve(Play.Quote(BattleFactory.PlayerId, BattleFactory.QuoteId), normal));
            Assert.AreEqual(BattleStatus.PlayerWon, normal.Status, "an ordinary duel is still won by the crowd");
        }

        [Test]
        public void PhasedOpponent_ArguesWhatTheCurrentPhaseIsAbout()
        {
            var duel = StreetTrial();
            var opponent = new PhasedOpponent(duel);
            var state = BattleFactory.Create(rules: duel.Rules(), playerInitiative: 5, opponentInitiative: 10);

            var first = opponent.Decide(state, state.Opponent, null);
            Assert.AreEqual(ActionId.Argue, first.Action);
            Assert.AreEqual(PersonalFallacy, first.PresentedArgument.Value.FallacyId);
            Assert.AreNotEqual(first.PresentedArgument.Value.Speech, opponent.Decide(state, state.Opponent, null).PresentedArgument.Value.Speech, "he varies the attack inside a phase");

            state.Apply(resolver.Resolve(first, state));
            state.Apply(resolver.Resolve(Play.Concede(BattleFactory.PlayerId), state));
            Assert.AreEqual(DilemmaFallacy, opponent.Decide(state, state.Opponent, null).PresentedArgument.Value.FallacyId);
        }

        [Test]
        public void KnowingEachCounter_WinsTheTrialAndRefutingEverything_LosesIt()
        {
            var won = Simulate(new CounterPlayerPolicy());
            Assert.AreEqual(BattleStatus.PlayerWon, won.Status);
            CollectionAssert.AreEqual(
                new[] { ActionId.Concede, ActionId.Refute, ActionId.Maieutics },
                won.Log.Where(entry => entry.ActorId == BattleFactory.PlayerId && entry.Hit).Select(entry => entry.Action),
                "one counter per phase, in order");

            var lost = Simulate(new RefuteEverythingPolicy());
            Assert.AreEqual(BattleStatus.PlayerLost, lost.Status, "refuting a personal attack keeps backfiring");
            Assert.IsTrue(lost.Log.Any(entry => entry.Deflected));
            Assert.AreEqual(OpponentMaxCoherence, lost.Opponent.Coherence, "the wrong answer never moves him");

            var retry = Simulate(new CounterPlayerPolicy());
            Assert.AreEqual(BattleStatus.PlayerWon, retry.Status, "a lost trial starts over from the first phase and can still be won");
        }

        private BattleState Simulate(IPlayerPolicy player)
        {
            var duel = StreetTrial();
            var state = BattleFactory.Create(rules: duel.Rules(), playerInitiative: 5, opponentInitiative: 10);
            BattleSimulator.Run(state, player, new PhasedOpponent(duel), null);
            return state;
        }

        private BattleState StateArguing(PhasedDuel duel, Argument argument)
        {
            var state = BattleFactory.Create(rules: duel.Rules(), playerInitiative: 5, opponentInitiative: 10);
            state.Apply(resolver.Resolve(Play.Argue(BattleFactory.OpponentId, BattleFactory.PlayerId, argument), state));
            return state;
        }

        private BattleState StateInPhase(PhasedDuel duel, int phase)
        {
            var state = BattleFactory.Create(rules: duel.Rules(), playerInitiative: 5, opponentInitiative: 10);
            var opponent = new PhasedOpponent(duel);
            var player = new CounterPlayerPolicy();
            while (duel.IndexFor(state.Opponent.Coherence) < phase || state.Queue.Current != BattleFactory.PlayerId || !state.CurrentArgument.HasValue || state.CurrentArgument.Value.FallacyId != duel.Phases[phase].Arguments[0].FallacyId)
            {
                var actor = state.Find(state.Queue.Current);
                var play = actor.Side == Side.Opponent ? opponent.Decide(state, actor, null) : player.Decide(state, actor);
                state.Apply(resolver.Resolve(play, state));
                Assert.AreEqual(BattleStatus.Ongoing, state.Status, $"the duel ended before phase {phase}");
            }

            return state;
        }

        private static PhasedDuel StreetTrial() => new(new[]
        {
            new DuelPhase("personal_attack", ActionId.Concede, FirstFloor, new[] { Argument(PersonalFallacy), Argument(PersonalFallacy, "Filha de oleiro não tem voz nesta escadaria, e todos aqui sabem disso muito bem.") }),
            new DuelPhase("false_dilemma", ActionId.Refute, SecondFloor, new[] { Argument(DilemmaFallacy) }),
            new DuelPhase("political_fear", ActionId.Maieutics, PhasedDuel.LastFloor, new[] { Argument(FearFallacy) })
        });

        private static Argument Argument(string fallacy, string speech = "Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.") =>
            new(speech, fallacy, new[] { "Kleia é do Cerâmico", "Oleiros não entendem de justiça" }, BattleFactory.WeakPremise, ArgumentTones.Mocking);
    }
}
