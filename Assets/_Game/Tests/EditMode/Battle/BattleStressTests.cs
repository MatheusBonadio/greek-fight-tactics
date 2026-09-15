using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Net;
using Game.Tests.EditMode.Grid;
using NUnit.Framework;
using UnityEditor;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class BattleStressTests
    {
        private const int Seed = 402;
        private const int RandomBattles = 1000;
        private const int StrategicBattlesPerProfile = 50;
        private const double WithdrawChance = 0.01;

        private List<string> refutableIds;
        private List<string> paradoxIds;
        private FallbackArgumentLibrary library;

        [OneTimeSetUp]
        public void LoadContent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var fallback = AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(DataTools.FallbackLibraryPath);
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(fallback);

            refutableIds = catalog.RefutableIds.ToList();
            paradoxIds = catalog.All.Where(fallacy => !fallacy.IsRefutable).Select(fallacy => fallacy.Id).ToList();
            library = new FallbackArgumentLibrary(fallback, new ArgumentValidator(refutableIds), Seed);
        }

        [Test]
        public void ThousandRandomBattles_EndWithoutExceptionsWithinTheTurnLimit()
        {
            var tally = new Dictionary<string, (int Battles, int Wins, int Losses, int Withdrawals, int Stalemates, int Turns)>();

            for (var battle = 0; battle < RandomBattles; battle++)
            {
                var act = battle % 2 == 0 ? Act.One : Act.Two;
                var profile = battle / 2 % 2 == 0 ? Profile.Sophist : Profile.Philosopher;
                var state = CreateArena(act, profile, battle);
                var source = new LibraryArgumentSource(library, act == Act.One ? refutableIds : paradoxIds, battle);
                var player = new RandomPlayerPolicy(new System.Random(battle), refutableIds, WithdrawChance);
                var brain = new OpponentBrain(new System.Random(Seed + battle));

                var result = BattleSimulator.Run(state, player, brain, source);

                Assert.AreNotEqual(BattleStatus.Ongoing, result.Status, $"battle {battle}");
                Assert.LessOrEqual(result.Turns, state.Rules.TurnLimit, $"battle {battle}");

                var key = $"{act}/{profile}";
                tally.TryGetValue(key, out var row);
                tally[key] = (
                    row.Battles + 1,
                    row.Wins + (result.Status == BattleStatus.PlayerWon ? 1 : 0),
                    row.Losses + (result.Status == BattleStatus.PlayerLost ? 1 : 0),
                    row.Withdrawals + (result.Status == BattleStatus.PlayerWithdrew ? 1 : 0),
                    row.Stalemates + (result.Status == BattleStatus.TurnLimitReached ? 1 : 0),
                    row.Turns + result.Turns);
            }

            foreach (var entry in tally.OrderBy(pair => pair.Key))
            {
                var row = entry.Value;
                TestContext.WriteLine($"{entry.Key}: {row.Battles} battles, win {100.0 * row.Wins / row.Battles:F1}%, loss {100.0 * row.Losses / row.Battles:F1}%, withdraw {100.0 * row.Withdrawals / row.Battles:F1}%, turn limit {100.0 * row.Stalemates / row.Battles:F1}%, avg turns {(double)row.Turns / row.Battles:F1}");
            }
        }

        [TestCase(Profile.Sophist)]
        [TestCase(Profile.Philosopher)]
        public void ActTwoOpponents_CanBeBeatenByAStrategicPlayer(Profile profile)
        {
            var wins = 0;
            for (var battle = 0; battle < StrategicBattlesPerProfile; battle++)
            {
                var state = CreateArena(Act.Two, profile, battle);
                var source = new LibraryArgumentSource(library, paradoxIds, battle);
                var result = BattleSimulator.Run(state, new StrategicPlayerPolicy(), new OpponentBrain(new System.Random(Seed + battle)), source);
                wins += result.Status == BattleStatus.PlayerWon ? 1 : 0;
            }

            TestContext.WriteLine($"Act Two {profile}: strategic player won {wins}/{StrategicBattlesPerProfile}");
            Assert.Greater(wins, 0);
        }

        private static BattleState CreateArena(Act act, Profile profile, int battle)
        {
            var random = new System.Random(Seed * 31 + battle);
            var state = Create(
                act,
                profile,
                playerInitiative: random.Next(1, 20),
                opponentInitiative: random.Next(1, 20),
                initialPlayerAudience: random.Next(20, 80),
                audienceMembers: new[] { CitizenId, CitizenId + 1 });

            GridFactory.Occupy(state.Grid, new Coord(1, 5), CitizenId);
            GridFactory.Occupy(state.Grid, new Coord(5, 5), CitizenId + 1);
            return state;
        }
    }
}
