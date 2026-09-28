using System;
using System.Linq;
using Game.Battle;
using Game.Core;
using NUnit.Framework;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class ShuffledPremiseSourceTests
    {
        private const int Draws = 200;
        private const int Seed = 7;

        private static readonly Argument ThreePremises = new(
            "Todo homem justo é temido pelos injustos. Sócrates era temido. Logo, Sócrates era justo.",
            "affirming_the_consequent",
            new[] { "Todo justo é temido pelos injustos", "Sócrates era temido", "Quem é temido é justo" },
            2,
            ArgumentTones.Arrogant);

        [Test]
        public void TheWeakPremise_FollowsItsTextWhereverItLands()
        {
            var random = new Random(Seed);
            for (var draw = 0; draw < Draws; draw++)
            {
                var shuffled = ShuffledPremiseSource.Shuffle(ThreePremises, random);

                Assert.AreEqual(ThreePremises.Premises[ThreePremises.WeakPremise], shuffled.Premises[shuffled.WeakPremise]);
                CollectionAssert.AreEquivalent(ThreePremises.Premises, shuffled.Premises);
                Assert.AreEqual(ThreePremises.Speech, shuffled.Speech);
                Assert.AreEqual(ThreePremises.FallacyId, shuffled.FallacyId);
            }
        }

        [Test]
        public void TheWeakPremise_DoesNotAlwaysSitInTheSameRow()
        {
            var random = new Random(Seed);
            var rows = Enumerable.Range(0, Draws).Select(_ => ShuffledPremiseSource.Shuffle(RefutableArgument(), random).WeakPremise).ToList();

            Assert.AreEqual(2, rows.Distinct().Count(), "the player cannot learn that the second premise is always the weak one");
        }

        [Test]
        public void TheSource_ShufflesWhatTheInnerSourceGives()
        {
            var source = new ShuffledPremiseSource(new ScriptedArgumentSource(new[] { RefutableArgument() }), new Random(Seed));
            var state = CreateWithArgument(RefutableArgument());

            Assert.IsTrue(source.TryNext(state.Opponent, out var argument));
            Assert.AreEqual(RefutableArgument().Premises[WeakPremise], argument.Premises[argument.WeakPremise]);
            Assert.IsFalse(new ShuffledPremiseSource(new ScriptedArgumentSource(Array.Empty<Argument>()), new Random(Seed)).TryNext(state.Opponent, out _));
        }
    }
}
