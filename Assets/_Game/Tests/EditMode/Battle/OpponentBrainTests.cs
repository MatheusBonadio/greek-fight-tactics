using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.Tests.EditMode.Grid;
using NUnit.Framework;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class OpponentBrainTests
    {
        private const int Seed = 402;

        private BattleResolver resolver;
        private IArgumentSource arguments;

        [SetUp]
        public void SetUp()
        {
            resolver = new BattleResolver();
            arguments = new ListArgumentSource(new[] { RefutableArgument() }, Seed);
        }

        [Test]
        public void OpeningTurn_PresentsAnArgument()
        {
            var state = Create(Act.Two, playerInitiative: 1, opponentInitiative: 9);

            var play = new OpponentBrain(new System.Random(Seed)).Decide(state, state.Opponent, arguments);

            Assert.AreEqual(ActionId.Argue, play.Action);
        }

        [Test]
        public void Sophist_WalksToTheCrowdUsingThePathFinder()
        {
            var state = CreateWithArgument(RefutableArgument(), Act.Two, audienceMembers: new[] { CitizenId, CitizenId + 1 });
            GridFactory.Occupy(state.Grid, new Coord(6, 1), CitizenId);
            GridFactory.Occupy(state.Grid, new Coord(6, 3), CitizenId + 1);
            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));

            var play = new OpponentBrain(new System.Random(Seed)).Decide(state, state.Opponent, arguments);

            Assert.AreEqual(ActionId.Circulate, play.Action);
            Assert.AreEqual(new Coord(6, 2), play.Destination);
        }

        [Test]
        public void Philosopher_ConcedesWhenCoherenceIsLow()
        {
            var rules = new BattleRules { MaieuticsDamage = 30 };
            var state = CreateWithArgument(Paradox(), Act.Two, opponentProfile: Profile.Philosopher, rules: rules);
            state.Apply(resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state));

            Assert.AreEqual(OpponentId, state.Queue.Current);
            Assert.Less((double)state.Opponent.Coherence / state.Opponent.MaxCoherence, OpponentBrain.PhilosopherLowCoherenceRatio);

            var play = new OpponentBrain(new System.Random(Seed)).Decide(state, state.Opponent, arguments);

            Assert.AreEqual(ActionId.Concede, play.Action);
        }

        [Test]
        public void ActOne_SometimesMakesMistakes_ActTwo_Never()
        {
            var actOneChoices = DecisionsOnRepeat(Act.One);
            var actTwoChoices = DecisionsOnRepeat(Act.Two);

            Assert.Greater(actOneChoices.Distinct().Count(), 1);
            Assert.AreEqual(1, actTwoChoices.Distinct().Count());
        }

        [TestCase(Act.One, Profile.Sophist)]
        [TestCase(Act.One, Profile.Philosopher)]
        [TestCase(Act.Two, Profile.Sophist)]
        [TestCase(Act.Two, Profile.Philosopher)]
        public void EveryDecision_IsAcceptedByTheResolver(Act act, Profile profile)
        {
            for (var battle = 0; battle < 40; battle++)
            {
                var state = Create(act, profile, playerInitiative: 1 + battle % 7, opponentInitiative: 1 + battle % 5);
                var brain = new OpponentBrain(new System.Random(Seed + battle));
                var player = new RandomPlayerPolicy(new System.Random(battle), new[] { Fallacy, "straw_man" });

                while (state.Status == BattleStatus.Ongoing)
                {
                    var actor = state.Find(state.Queue.Current);
                    if (actor.Side == Side.Opponent)
                    {
                        var decision = brain.Decide(state, actor, arguments);
                        var outcome = resolver.Resolve(decision, state);
                        Assert.IsFalse(outcome.IsRejected, $"battle {battle} turn {state.Turn}: {decision.Action} rejected with {outcome.Rejection}");
                        state.Apply(outcome);
                        continue;
                    }

                    state.Apply(resolver.Resolve(player.Decide(state, actor), state));
                }
            }
        }

        private List<ActionId> DecisionsOnRepeat(Act act)
        {
            var state = CreateWithArgument(RefutableArgument(), act);
            state.Apply(resolver.Resolve(Play.Concede(PlayerId), state));
            var brain = new OpponentBrain(new System.Random(Seed));
            return Enumerable.Range(0, 200).Select(_ => brain.Decide(state, state.Opponent, arguments).Action).ToList();
        }
    }
}
