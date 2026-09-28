using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Net;
using Game.Tests.EditMode.Battle;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Net
{
    public sealed class FallbackArgumentSourceTests
    {
        private const int Seed = 402;
        private const int DrawsPerFallacy = 6;

        private FallbackLibrarySO fallbackLibrary;
        private ArgumentValidator validator;

        [SetUp]
        public void SetUp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            fallbackLibrary = AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(DataTools.FallbackLibraryPath);
            Assert.IsNotNull(catalog, "run DataTools.Regenerate first");
            Assert.IsNotNull(fallbackLibrary, "run DataTools.Regenerate first");
            validator = new ArgumentValidator(catalog.RefutableIds);
        }

        [Test]
        public void EveryOpponent_ArguesWithHandWrittenLinesFromItsOwnPool()
        {
            foreach (var id in DataTools.OpponentIds())
            {
                var entry = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(id));
                var pool = entry.FallacyPool.Select(fallacy => fallacy.Id).ToList();
                var source = CreateSource(Seed);
                source.Register(BattleFactory.OpponentId, pool);

                foreach (var argument in Draw(source, pool.Count * DrawsPerFallacy))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(argument.Speech), id);
                    Assert.IsTrue(validator.IsValid(argument), $"{id}: {argument.Speech}");
                    CollectionAssert.Contains(pool, argument.FallacyId, id);
                }
            }
        }

        [Test]
        public void TheSameSeed_DrawsTheSameArguments()
        {
            var pool = new[] { "ad_hominem", "false_dilemma" };
            var first = CreateSource(Seed);
            var second = CreateSource(Seed);
            first.Register(BattleFactory.OpponentId, pool);
            second.Register(BattleFactory.OpponentId, pool);

            CollectionAssert.AreEqual(Draw(first, 10).Select(argument => argument.Speech), Draw(second, 10).Select(argument => argument.Speech), "a bug seen in a duel must replay from its seed");
        }

        [Test]
        public void AnOpponentWithoutAPool_HasNoArguments()
        {
            var source = CreateSource(Seed);
            source.Register(7, new[] { " ", null, "not_in_the_catalog" });

            Assert.IsFalse(source.TryNext(Debater(BattleFactory.OpponentId), out _), "an opponent nobody registered stays silent");
            Assert.IsFalse(source.TryNext(Debater(7), out _), "blank and unknown fallacies give nothing to say");
            Assert.IsFalse(source.TryNext(null, out _));
        }

        [Test]
        public void AWholeBattle_RunsOnTheHandWrittenArguments()
        {
            var source = CreateSource(Seed);
            source.Register(BattleFactory.OpponentId, new[] { "ad_hominem" });
            var state = BattleFactory.Create(playerInitiative: 5, opponentInitiative: 10);

            var result = BattleSimulator.Run(state, new StrategicPlayerPolicy(), new OpponentBrain(new System.Random(Seed)), source);

            Assert.AreNotEqual(BattleStatus.Ongoing, result.Status);
            Assert.IsTrue(state.Log.Any(entry => entry.Action == ActionId.Argue && entry.Rejection == RejectionReason.None), "the opponent argued with the fallback");
        }

        private FallbackArgumentSource CreateSource(int seed) =>
            new(new FallbackArgumentLibrary(fallbackLibrary, validator, seed), seed);

        private static Debater Debater(int id) =>
            new(id, Side.Opponent, Profile.Sophist, 50, 5, BattleFactory.OpponentStart, Game.Grid.Facing.SouthWest);

        private List<Argument> Draw(FallbackArgumentSource source, int count)
        {
            var debater = Debater(BattleFactory.OpponentId);
            var drawn = new List<Argument>();
            for (var index = 0; index < count; index++)
            {
                Assert.IsTrue(source.TryNext(debater, out var argument), $"no argument on draw {index}");
                drawn.Add(argument);
            }

            return drawn;
        }
    }
}
