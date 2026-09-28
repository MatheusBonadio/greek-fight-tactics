using Game.Battle;
using Game.Core;
using Game.Tests.EditMode.Grid;
using NUnit.Framework;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class MoraleRuleTests
    {
        private static readonly Coord BesideKleia = new(1, 2);
        private static readonly Coord TwoFromKleia = new(2, 4);
        private static readonly Coord ThreeFromKleia = new(2, 5);
        private static readonly Coord BelowKleia = new(2, 0);
        private static readonly Coord BehindKleia = new(0, 2);
        private static readonly Coord BesideTheOpponent = new(5, 2);

        private BattleResolver resolver;

        [SetUp]
        public void SetUp()
        {
            resolver = new BattleResolver();
        }

        [Test]
        public void ACrowdOnHerSide_StrengthensKleiasBlow()
        {
            var state = ArguedWithCrowd(70, BesideKleia, TwoFromKleia);
            Assert.Greater(state.PlayerAudience, BattleRules.MaxAudience / 2);

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);

            Assert.AreEqual(2 * state.Rules.MoraleDamageStep, outcome.MoraleShift);
            Assert.AreEqual(-(state.Rules.RefuteDamage + outcome.MoraleShift), outcome.TargetCoherenceDelta);
        }

        [Test]
        public void ACrowdAgainstHer_WeakensKleiasBlow()
        {
            var state = ArguedWithCrowd(40, BesideKleia, TwoFromKleia);

            var outcome = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state);

            Assert.AreEqual(-2 * state.Rules.MoraleDamageStep, outcome.MoraleShift);
            Assert.AreEqual(-(state.Rules.MaieuticsDamage - 2 * state.Rules.MoraleDamageStep), outcome.TargetCoherenceDelta);
        }

        [Test]
        public void AnEvenlySplitAudience_LeavesTheBlowAlone()
        {
            var state = ArguedWithCrowd(58, BesideKleia, TwoFromKleia);
            Assert.AreEqual(BattleRules.MaxAudience / 2, state.PlayerAudience);

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);

            Assert.AreEqual(0, outcome.MoraleShift);
            Assert.AreEqual(-state.Rules.RefuteDamage, outcome.TargetCoherenceDelta);
        }

        [Test]
        public void TheCrowdShift_StopsAtItsCap()
        {
            var state = ArguedWithCrowd(90, BesideKleia, TwoFromKleia, BelowKleia, BehindKleia);

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);

            Assert.AreEqual(state.Rules.MaxMoraleShift, outcome.MoraleShift, "four listeners around her weigh no more than the cap");
        }

        [Test]
        public void OnlyTheCrowdCloseToTheStriker_Counts()
        {
            var state = ArguedWithCrowd(70, ThreeFromKleia, BesideTheOpponent);

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);

            Assert.AreEqual(0, outcome.MoraleShift, "a listener three cells away, or beside the one who is hit, does not lift Kleia");
        }

        [Test]
        public void AWrongRefutation_IsStruckBackWithTheOpponentsMorale()
        {
            var state = ArguedWithCrowd(30, BesideTheOpponent);
            Assert.Less(state.PlayerAudience, BattleRules.MaxAudience / 2);

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, "straw_man"), state);

            Assert.IsTrue(outcome.Counterattack);
            Assert.AreEqual(state.Rules.MoraleDamageStep, outcome.MoraleShift, "the audience cheers the opponent who is close to it");
            Assert.AreEqual(-(state.Rules.CounterattackDamage + state.Rules.MoraleDamageStep), outcome.ActorCoherenceDelta);
        }

        [Test]
        public void APhilosopherBooedUpClose_StillLandsAtLeastOnePoint()
        {
            var crowd = new[] { CitizenId, CitizenId + 1, CitizenId + 2, CitizenId + 3 };
            var state = Create(Act.Two, Profile.Philosopher, playerInitiative: 5, opponentInitiative: 10, initialPlayerAudience: 90, audienceMembers: crowd);
            Occupy(state, crowd, BesideTheOpponent, new Coord(4, 3), new Coord(4, 1), new Coord(6, 2));

            var outcome = resolver.Resolve(Play.Argue(OpponentId, PlayerId, Paradox()), state);

            Assert.AreEqual(-state.Rules.MaxMoraleShift, outcome.MoraleShift);
            Assert.AreEqual(-MoraleRule.MinimumBlow, outcome.TargetCoherenceDelta, "morale weakens a blow but never turns it into nothing");
        }

        [Test]
        public void TheSpectatorsOfTheArena_LiftMoraleButNotTheAudienceBonus()
        {
            var spectators = new[] { CitizenId, CitizenId + 1 };
            var state = Create(playerInitiative: 5, opponentInitiative: 10, initialPlayerAudience: 70, spectators: spectators);
            Occupy(state, spectators, BesideKleia, TwoFromKleia);
            state.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state));

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);

            Assert.AreEqual(2 * state.Rules.MoraleDamageStep, outcome.MoraleShift, "the spectators who came to watch are part of the crowd");
            Assert.AreEqual(state.Rules.RefuteAudienceGain, outcome.PlayerAudienceDelta, "only the citizens of the map give the audience bonus");
        }

        [Test]
        public void TheTutorial_ShowsTheCrowdWeighingAndStillFallsToOneRefutation()
        {
            const int OpponentCoherence = 50;
            var crowd = new[] { CitizenId, CitizenId + 1, CitizenId + 2, CitizenId + 3 };
            var state = Create(playerInitiative: 5, opponentInitiative: 10, initialPlayerAudience: 10, audienceMembers: crowd, rules: RefuteTutorial.Rules(OpponentCoherence));
            Occupy(state, crowd, BesideKleia, TwoFromKleia, BelowKleia, BehindKleia);
            state.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state));

            Assert.AreEqual(-state.Rules.MaxMoraleShift, MoraleRule.ShiftFor(state, state.Player), "the lesson shows the crowd booing her");

            var outcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);
            state.Apply(outcome);

            Assert.AreEqual(-state.Rules.MaxMoraleShift, outcome.MoraleShift);
            Assert.AreEqual(0, state.Opponent.Coherence, "one refutation still ends the lesson, whatever the crowd thinks");
            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
        }

        private BattleState ArguedWithCrowd(int initialPlayerAudience, params Coord[] cells)
        {
            var crowd = new int[cells.Length];
            for (var index = 0; index < cells.Length; index++)
            {
                crowd[index] = CitizenId + index;
            }

            var state = Create(playerInitiative: 5, opponentInitiative: 10, initialPlayerAudience: initialPlayerAudience, audienceMembers: crowd);
            Occupy(state, crowd, cells);
            state.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, RefutableArgument()), state));
            return state;
        }

        private static void Occupy(BattleState state, int[] ids, params Coord[] cells)
        {
            for (var index = 0; index < cells.Length; index++)
            {
                GridFactory.Occupy(state.Grid, cells[index], ids[index]);
            }
        }
    }
}
