using Game.Battle;
using Game.Core;
using NUnit.Framework;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class DuelVictoryRuleTests
    {
        private const int KnockoutDamage = 100;
        private const int AlmostWonAudience = 95;

        private readonly BattleResolver resolver = new();

        [Test]
        public void ARefutationThatEndsTheDuel_IsAVictoryByRefutation()
        {
            var state = CreateWithArgument(RefutableArgument(), rules: new BattleRules { RefuteDamage = KnockoutDamage });

            Apply(state, Play.Refute(PlayerId, OpponentId, Fallacy));

            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
            Assert.IsTrue(DuelVictoryRule.TryClassify(state, out var victory));
            Assert.AreEqual(DuelVictory.Refute, victory);
        }

        [Test]
        public void AMaieuticQuestionThatEndsTheDuel_IsAVictoryByMaieutics()
        {
            var state = CreateWithArgument(RefutableArgument(), rules: new BattleRules { MaieuticsDamage = KnockoutDamage });

            Apply(state, Play.Maieutics(PlayerId, OpponentId, WeakPremise));

            Assert.IsTrue(DuelVictoryRule.TryClassify(state, out var victory));
            Assert.AreEqual(DuelVictory.Maieutics, victory);
        }

        [Test]
        public void TheLastLandedBlowDecides_NotTheFirst()
        {
            var state = CreateWithArgument(RefutableArgument(), rules: new BattleRules { RefuteDamage = 10, MaieuticsDamage = KnockoutDamage, MaieuticsTurnCost = 1 });

            Apply(state, Play.Refute(PlayerId, OpponentId, Fallacy));
            Apply(state, Play.Argue(OpponentId, PlayerId, RefutableArgument()));
            Apply(state, Play.Maieutics(PlayerId, OpponentId, WeakPremise));

            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
            Assert.IsTrue(DuelVictoryRule.TryClassify(state, out var victory));
            Assert.AreEqual(DuelVictory.Maieutics, victory);
        }

        [Test]
        public void AWinWithoutALandedBlow_CountsAsWonByConceding()
        {
            var state = Create(playerInitiative: 5, opponentInitiative: 10, initialPlayerAudience: AlmostWonAudience);

            Apply(state, Play.Concede(OpponentId));

            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
            Assert.IsTrue(DuelVictoryRule.TryClassify(state, out var victory));
            Assert.AreEqual(DuelVictoryRule.WithoutALandedBlow, victory);
        }

        [Test]
        public void ADuelThatWasNotWon_HasNoVictory()
        {
            var state = CreateWithArgument(RefutableArgument());

            Assert.IsFalse(DuelVictoryRule.TryClassify(state, out _));

            Apply(state, Play.Withdraw(PlayerId));
            Assert.AreEqual(BattleStatus.PlayerWithdrew, state.Status);
            Assert.IsFalse(DuelVictoryRule.TryClassify(state, out _));
            Assert.IsFalse(DuelVictoryRule.TryClassify(null, out _));
        }

        private void Apply(BattleState state, Play play)
        {
            var outcome = resolver.Resolve(play, state);
            Assert.IsFalse(outcome.IsRejected, $"{play.Action} was rejected: {outcome.Rejection}");
            state.Apply(outcome);
        }
    }
}
