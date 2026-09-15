using Game.Battle;
using Game.Content;
using NUnit.Framework;
using UnityEngine;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Content
{
    public sealed class QuoteItemConversionTests
    {
        [Test]
        public void QuoteItemSO_IsConsumedByTheResolverWithItsFixedEffect()
        {
            var asset = ScriptableObject.CreateInstance<QuoteItemSO>();
            try
            {
                asset.Configure("parmenides", "Parmênides", "Nada muda: recupera a calma.", 12, 3, 2);
                var quote = asset.ToQuoteItem();

                Assert.AreEqual("parmenides", quote.Id);
                Assert.AreEqual(12, quote.CoherenceDelta);
                Assert.AreEqual(3, quote.AudienceDelta);
                Assert.AreEqual(2, quote.Uses);

                var state = Create();
                var battle = new BattleState(state.Grid, state.Act, state.Rules, new[] { Copy(state.Player, 4, 4), Copy(state.Opponent, 6, 4) }, 40, new[] { quote });
                var outcome = new BattleResolver().Resolve(Play.Quote(PlayerId, "parmenides"), battle);
                battle.Apply(outcome);

                Assert.IsFalse(outcome.IsRejected);
                Assert.AreEqual(43, battle.PlayerAudience);
                Assert.IsTrue(battle.TryGetQuote("parmenides", out _, out var usesLeft));
                Assert.AreEqual(1, usesLeft);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static Debater Copy(Debater source, int x, int y) =>
            new(source.Id, source.Side, source.Profile, source.MaxCoherence, source.Initiative, new Game.Core.Coord(x, y), source.Facing);
    }
}
