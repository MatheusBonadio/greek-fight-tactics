using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Grid;
using Game.Net;
using Game.UI;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Battle
{
    public sealed class OpponentRosterTests
    {
        private const int Seed = 402;
        private const int BattlesPerOpponent = 40;
        private const int PlayerId = 1;
        private const int OpponentId = 2;
        private const int FirstBystanderId = 100;
        private const int MaxPoolSize = 3;

        private FallbackArgumentLibrary library;

        [OneTimeSetUp]
        public void LoadContent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var fallback = AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(DataTools.FallbackLibraryPath);
            Assert.IsNotNull(catalog, "run DataTools.Regenerate first");
            Assert.IsNotNull(fallback, "run DataTools.Regenerate first");
            library = new FallbackArgumentLibrary(fallback, new ArgumentValidator(catalog.RefutableIds), Seed);
        }

        [TestCaseSource(nameof(OpponentIds))]
        public void GeneratedOpponent_HasItsIdSheetAndASmallPool(string id)
        {
            var opponent = Load(id);

            Assert.AreEqual(id, opponent.Id);
            Assert.IsNotEmpty(opponent.DisplayName);
            Assert.IsNotNull(opponent.Sheet, $"{id} has no placeholder sheet; add art/placeholders/characters/{id}_sheet.png and run ArtTools.SliceSheets");
            Assert.AreEqual(ArtTools.SheetPathFor(id), AssetDatabase.GetAssetPath(opponent.Sheet));
            Assert.IsNotEmpty(opponent.FallacyPool);
            Assert.LessOrEqual(opponent.FallacyPool.Count, MaxPoolSize);
            Assert.AreEqual(id, OpponentContext.From(opponent).Id);
        }

        [TestCaseSource(nameof(OpponentIds))]
        public void Pool_FitsTheActAndIsCoveredByTheFallbackBank(string id)
        {
            var opponent = Load(id);

            foreach (var fallacy in opponent.FallacyPool)
            {
                Assert.AreEqual(opponent.Act == Act.One, fallacy.IsRefutable, $"{id}: act one opponents always commit an error to point out");
                Assert.GreaterOrEqual(library.CountFor(fallacy.Id), DataTools.MinimumFallbackArguments, $"{id}: {fallacy.Id}");
            }
        }

        [TestCase("oron", Profile.Sophist, 40, 60, "false_dilemma")]
        [TestCase("phaidra", Profile.Sophist, 30, 70, "appeal_to_ignorance")]
        [TestCase("kritolaos", Profile.Sophist, 50, 80, "ad_hominem")]
        [TestCase("simo", Profile.Sophist, 45, 65, "straw_man")]
        [TestCase("batilo", Profile.Philosopher, 60, 30, "hasty_generalization")]
        public void NeighbourhoodOpponent_FollowsTheDesignDocument(string id, Profile profile, int coherence, int opponentAudience, string family)
        {
            var opponent = Load(id);

            Assert.AreEqual(profile, opponent.Profile);
            Assert.AreEqual(Act.One, opponent.Act);
            Assert.AreEqual(coherence, opponent.MaxCoherence);
            Assert.AreEqual(opponentAudience, BattleRules.MaxAudience - opponent.InitialAudience, "initial_audience is Kleia's share of the crowd; the GDD lists the opponent's");
            Assert.AreEqual(family, opponent.FallacyPool[0].Id);
        }

        [TestCaseSource(nameof(OpponentIds))]
        public void SimulatedDuelWhereTheOpponentStands_AdmitsAPlayerVictory(string id)
        {
            var opponent = Load(id);
            var (map, placement) = Placement(id);
            var grid = BuildGrid(map, placement, out var audience);
            var playerCell = ChallengeCell(map, grid, placement);
            var pool = opponent.FallacyPool.Select(fallacy => fallacy.Id).ToList();

            var wins = 0;
            var victories = new Dictionary<DuelVictory, int>();
            for (var battle = 0; battle < BattlesPerOpponent; battle++)
            {
                var source = new LibraryArgumentSource(library, pool, Seed + battle);
                var session = BattleSession.Start(grid, new BattleSetup
                {
                    PlayerId = PlayerId,
                    PlayerCell = playerCell,
                    OpponentId = OpponentId,
                    OpponentCell = new Coord(placement.X, placement.Y),
                    OpponentFacing = placement.Facing,
                    OpponentProfile = opponent.Profile,
                    OpponentMaxCoherence = opponent.MaxCoherence,
                    InitialAudience = opponent.InitialAudience,
                    Act = opponent.Act,
                    AudienceIds = audience,
                    Seed = Seed + battle
                }, source);

                var result = BattleSimulator.Run(session.State, new StrategicPlayerPolicy(), new OpponentBrain(new System.Random(Seed * 31 + battle)), source);
                Assert.AreNotEqual(BattleStatus.Ongoing, result.Status, $"{id} battle {battle}");
                if (result.Status != BattleStatus.PlayerWon)
                {
                    continue;
                }

                wins++;
                Assert.IsTrue(DuelVictoryRule.TryClassify(session.State, out var victory));
                victories.TryGetValue(victory, out var count);
                victories[victory] = count + 1;
            }

            TestContext.WriteLine($"{id} at ({placement.X}, {placement.Y}), Kleia at {playerCell}: won {wins}/{BattlesPerOpponent} ({string.Join(", ", victories.Select(pair => $"{pair.Key} {pair.Value}"))})");
            Assert.Greater(wins, 0, $"no simulated duel against {id} ended in a player victory");
        }

        private static IEnumerable<string> OpponentIds() => DataTools.OpponentIds();

        private static OpponentSO Load(string id)
        {
            var opponent = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(id));
            Assert.IsNotNull(opponent, $"run DataTools.Regenerate to create {DataTools.OpponentPath(id)}");
            return opponent;
        }

        private static (MapLayout Map, MapActor Placement) Placement(string id)
        {
            foreach (var entry in SandboxTools.Maps)
            {
                var map = MapLayout.Load(entry.MapPath);
                var placement = map.Actors.Skip(1).SingleOrDefault(actor => actor.Character == id);
                if (placement != null)
                {
                    return (map, placement);
                }
            }

            Assert.Fail($"{id} is not placed on any map");
            return default;
        }

        private static GridModel BuildGrid(MapLayout map, MapActor opponent, out List<int> audience)
        {
            var grid = new GridModel(map.Width, map.Height);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    grid.TrySet(new Coord(x, y), new TileData
                    {
                        Terrain = TerrainId.Marble,
                        Cost = map.IsWalkable(x, y) ? (byte)PathFinder.MinimumStepCost : TileData.ImpassableCost,
                        Revealed = true
                    });
                }
            }

            audience = new List<int>();
            for (var index = 1; index < map.Actors.Count; index++)
            {
                var actor = map.Actors[index];
                var occupant = actor == opponent ? OpponentId : FirstBystanderId + index;
                var cell = new Coord(actor.X, actor.Y);
                var tile = grid[cell];
                tile.OccupantId = occupant;
                grid.TrySet(cell, tile);
                if (actor.Character == SandboxTools.AudienceCharacter)
                {
                    audience.Add(occupant);
                }
            }

            return grid;
        }

        private static Coord ChallengeCell(MapLayout map, GridModel grid, MapActor opponent)
        {
            var start = new Coord(map.Actors[0].X, map.Actors[0].Y);
            var path = new List<Coord>();

            Assert.IsTrue(new PathFinder().FindPathBeside(grid, start, new Coord(opponent.X, opponent.Y), path), $"Kleia cannot walk up to {opponent.Character} to challenge him");
            return path.Count > 0 ? path[^1] : start;
        }
    }
}
