using System.Linq;
using Game.Battle;
using Game.Core;
using Game.UI;
using NUnit.Framework;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class AbilityRulesTests
    {
        private static readonly AbilityId[] Inversion = { AbilityId.Inversion };
        private static readonly AbilityId[] Listening = { AbilityId.Listening };

        private readonly BattleResolver resolver = new();

        [Test]
        public void Inversion_TurnsTheFirstSophistAttackIntoAudienceForKleia()
        {
            var state = OpponentFirst(Profile.Sophist, Inversion);
            var rules = state.Rules;
            var audience = state.PlayerAudience;

            var inverted = Argue(state);

            Assert.AreEqual(AbilityId.Inversion, inverted.TriggeredAbility);
            Assert.AreEqual(rules.SophistArgueAudienceShift, inverted.PlayerAudienceDelta, "the shift the sophist wanted goes to Kleia");
            Assert.AreEqual(audience + rules.SophistArgueAudienceShift, state.PlayerAudience);
            Assert.AreEqual(AbilityId.Inversion, state.Log.Last().TriggeredAbility);
            Assert.AreEqual(0, state.AbilityUsesLeft(AbilityId.Inversion));

            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));
            var plain = Argue(state);
            Assert.IsNull(plain.TriggeredAbility, "the inversion works once per battle");
            Assert.AreEqual(-rules.SophistArgueAudienceShift, plain.PlayerAudienceDelta);
        }

        [Test]
        public void Inversion_TurnsAPhilosophersBlowIntoAudienceInsteadOfLostCoherence()
        {
            var state = OpponentFirst(Profile.Philosopher, Inversion);
            var coherence = state.Player.Coherence;
            var audience = state.PlayerAudience;

            var inverted = Argue(state);

            Assert.AreEqual(AbilityId.Inversion, inverted.TriggeredAbility);
            Assert.AreEqual(0, inverted.TargetCoherenceDelta);
            Assert.AreEqual(coherence, state.Player.Coherence, "Kleia takes no damage from the inverted attack");
            Assert.AreEqual(audience + state.Rules.PhilosopherArgueCoherenceDamage, state.PlayerAudience);
        }

        [Test]
        public void Listening_RevealsTheWeakPremiseOfTheFirstArgumentOnly()
        {
            var state = OpponentFirst(Profile.Sophist, Listening);
            var controller = new BattleMenuController(new ActionMenuModel(state, resolver), new[] { Fallacy });

            var heard = Argue(state);

            Assert.AreEqual(AbilityId.Listening, heard.TriggeredAbility);
            Assert.AreEqual(-state.Rules.SophistArgueAudienceShift, heard.PlayerAudienceDelta, "listening does not change the attack");
            Assert.IsTrue(state.IsWeakPremiseRevealed);
            controller.Refresh();
            Assert.IsTrue(controller.Actions.TrySelect(ActionId.Maieutics));
            Assert.IsFalse(controller.TryConfirm(out _));
            Assert.AreEqual(BattleMenuLayer.Premises, controller.Layer);
            Assert.AreEqual(WeakPremise, controller.HighlightedPremise(false), "the weak premise glows gold without the assisted mode");
            controller.Cancel();

            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));
            var next = Argue(state);
            Assert.IsNull(next.TriggeredAbility, "listening works once per battle");
            Assert.IsFalse(state.IsWeakPremiseRevealed, "a new argument hides the premise again");
            Assert.IsTrue(controller.Actions.TrySelect(ActionId.Maieutics));
            controller.Refresh();
            controller.TryConfirm(out _);
            Assert.IsNull(controller.HighlightedPremise(false));
            Assert.AreEqual(WeakPremise, controller.HighlightedPremise(true), "the assisted mode still highlights it");
        }

        [Test]
        public void Abilities_LeaveTheDuelUnchangedWhenKleiaHasNone()
        {
            var state = OpponentFirst(Profile.Philosopher);

            var outcome = Argue(state);

            Assert.IsNull(outcome.TriggeredAbility);
            Assert.AreEqual(-state.Rules.PhilosopherArgueCoherenceDamage, outcome.TargetCoherenceDelta);
            Assert.IsFalse(state.IsWeakPremiseRevealed);
            CollectionAssert.IsEmpty(state.PlayerAbilities);
        }

        [Test]
        public void Abilities_TakeTheirUsesFromTheRulesAndResolvingAloneSpendsNothing()
        {
            var rules = new BattleRules { InversionUsesPerBattle = 2 };
            var state = Create(rules: rules, playerInitiative: 5, opponentInitiative: 10, abilities: Inversion);
            var play = Play.Argue(OpponentId, PlayerId, RefutableArgument());

            var first = resolver.Resolve(play, state);
            var second = resolver.Resolve(play, state);
            Assert.AreEqual(AbilityId.Inversion, first.TriggeredAbility);
            Assert.AreEqual(AbilityId.Inversion, second.TriggeredAbility, "the resolver is pure: only Apply spends a use");
            Assert.AreEqual(2, state.AbilityUsesLeft(AbilityId.Inversion));

            state.Apply(first);
            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));
            Assert.AreEqual(AbilityId.Inversion, Argue(state).TriggeredAbility);
            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));
            Assert.IsNull(Argue(state).TriggeredAbility);
        }

        [Test]
        public void Abilities_OnlyAnswerTheOpponentsArgument()
        {
            var state = OpponentFirst(Profile.Sophist, new[] { AbilityId.Inversion, AbilityId.Listening });

            var concede = resolver.Resolve(Play.Concede(OpponentId), state);
            Assert.IsNull(concede.TriggeredAbility, "conceding is not an attack");

            Argue(state);
            var refute = resolver.Resolve(Play.Refute(PlayerId, OpponentId, "straw_man"), state);
            Assert.IsNull(refute.TriggeredAbility, "the counterattack after a wrong refutation is not inverted");
            Assert.IsTrue(refute.Counterattack);
        }

        [Test]
        public void Session_BringsTheAbilitiesKleiaOwnsIntoTheDuel()
        {
            var grid = Game.Tests.EditMode.Grid.GridFactory.Open(8, 8);
            var session = BattleSession.Start(grid, new BattleSetup
            {
                PlayerId = 1,
                PlayerCell = new Coord(2, 2),
                OpponentId = 5,
                OpponentCell = new Coord(4, 2),
                Abilities = Listening
            }, new ListArgumentSource(new[] { RefutableArgument() }, 1));

            Assert.IsTrue(session.State.HasAbility(AbilityId.Listening));
            Assert.IsFalse(session.State.HasAbility(AbilityId.Inversion));
            Assert.AreEqual(1, session.State.AbilityUsesLeft(AbilityId.Listening));
        }

        private BattleState OpponentFirst(Profile profile, AbilityId[] abilities = null) =>
            Create(opponentProfile: profile, playerInitiative: 5, opponentInitiative: 10, abilities: abilities);

        private Outcome Argue(BattleState state)
        {
            var outcome = resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state);
            Assert.IsFalse(outcome.IsRejected, outcome.Rejection.ToString());
            state.Apply(outcome);
            return outcome;
        }
    }
}
