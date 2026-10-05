using System;
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
            Assert.IsNotNull(opponent.Sheet, $"{id} has no placeholder sheet; add art/placeholders/characters/{opponent.SheetId}_sheet.png and run ArtTools.SliceSheets");
            Assert.AreEqual(ArtTools.SheetPathFor(opponent.SheetId), AssetDatabase.GetAssetPath(opponent.Sheet));
            Assert.IsNotEmpty(opponent.FallacyPool);
            Assert.LessOrEqual(opponent.FallacyPool.Count, MaxPoolSize);
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

        [TestCaseSource(nameof(OpponentIds))]
        public void Pool_MixesArgumentsOnlyARefutationAnswersWithOnesAQuestionAnswers(string id)
        {
            var opponent = Load(id);
            if (opponent.Act != Act.One || opponent.Phases.ToDuel() != null)
            {
                return;
            }

            var arguments = opponent.FallacyPool.SelectMany(fallacy => Bank(fallacy.Id)).ToList();
            var onlyRefutable = arguments.Count(argument => argument.IsOnlyRefutable);
            Assert.Greater(arguments.Count - onlyRefutable, 0, $"{id} never leans on a weak premise, so the question never works");
            Assert.GreaterOrEqual(onlyRefutable * 2, arguments.Count, $"{id}: at least half of the arguments keep sound premises and break the reasoning, so the question is not the answer to everything");
        }

        [Test]
        public void FormalFallacies_KeepSoundPremisesAndBreakTheForm()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var formal = catalog.All.Where(fallacy => fallacy.Category == FallacyCategory.Formal).ToList();

            Assert.IsNotEmpty(formal);
            foreach (var fallacy in formal)
            {
                Assert.IsTrue(Bank(fallacy.Id).All(argument => argument.IsOnlyRefutable), $"{fallacy.Id}: the error of a formal fallacy is in its form, not in a premise");
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
                var phases = opponent.Phases.ToDuel();
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
                    Seed = Seed + battle,
                    Rules = phases?.Rules()
                }, source);

                IPlayerPolicy player = phases == null ? new StrategicPlayerPolicy() : new CounterPlayerPolicy();
                IOpponentPolicy brain = phases == null ? new OpponentBrain(new System.Random(Seed * 31 + battle)) : new PhasedOpponent(phases);
                var result = BattleSimulator.Run(session.State, player, brain, source);
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

        [Test]
        public void SimulatedDuels_OnlyTheRightAnswerToEachArgumentWinsReliably()
        {
            var duels = 0;
            var guessedWins = 0;
            var askedWins = 0;
            var refutedWins = 0;
            var readWins = 0;
            var askingToll = 0;
            var refutingToll = 0;
            var readingToll = 0;
            foreach (var id in OpponentIds())
            {
                var ground = DuelGround.For(id, Load(id));
                if (ground.Opponent.Phases.ToDuel() != null)
                {
                    continue;
                }

                for (var battle = 0; battle < BattlesPerOpponent; battle++)
                {
                    duels++;
                    guessedWins += Won(Simulate(ground, new MaieuticsPolicy(new System.Random(Seed * 7 + battle)), battle));

                    var asked = Simulate(ground, new MaieuticsPolicy(), battle);
                    askedWins += Won(asked);
                    askingToll += Spent(asked);

                    var refuted = Simulate(ground, new KnownFallacyPolicy(), battle);
                    refutedWins += Won(refuted);
                    refutingToll += Spent(refuted);

                    var read = Simulate(ground, new ReadingPolicy(), battle);
                    readWins += Won(read);
                    readingToll += Spent(read);
                }
            }

            TestContext.WriteLine($"of {duels} duels: guessing the premise won {guessedWins}, asking about everything won {askedWins}, refuting everything won {refutedWins}, reading each argument won {readWins}; coherence spent asking {askingToll}, refuting {refutingToll}, reading {readingToll}");
            Assert.Less(askedWins * 2, duels, "the question is not the answer to everything: sound premises only fall to a refutation");
            Assert.Less(refutedWins * 5, duels * 3, "naming the fallacy is not the answer to everything either: a false premise only falls to a question");
            Assert.Less(guessedWins * 4, duels, "guessing the premise every turn loses three duels in four");
            Assert.Greater(readWins * 10, duels * 9, "asking where a premise is false and refuting where the reasoning is wins");
            Assert.Less(readingToll, Math.Min(askingToll, refutingToll), "the right answer to each argument is also the cheapest way through the duel");
        }

        private static int Spent(BattleState state) => state.Player.MaxCoherence - state.Player.Coherence;

        private static int Won(BattleState state) => state.Status == BattleStatus.PlayerWon ? 1 : 0;

        private BattleState Simulate(DuelGround ground, IPlayerPolicy player, int battle, BattleRules rules = null)
        {
            var source = new ShuffledPremiseSource(new LibraryArgumentSource(library, ground.Pool, Seed + battle), new System.Random(Seed + battle));
            var session = BattleSession.Start(ground.Grid, new BattleSetup
            {
                PlayerId = PlayerId,
                PlayerCell = ground.PlayerCell,
                OpponentId = OpponentId,
                OpponentCell = ground.OpponentCell,
                OpponentFacing = ground.Placement.Facing,
                OpponentProfile = ground.Opponent.Profile,
                OpponentMaxCoherence = ground.Opponent.MaxCoherence,
                InitialAudience = ground.Opponent.InitialAudience,
                Act = ground.Opponent.Act,
                AudienceIds = ground.Audience,
                Spectators = ground.Spectators,
                Seed = Seed + battle,
                Rules = rules
            }, source);
            BattleSimulator.Run(session.State, player, new OpponentBrain(new System.Random(Seed * 31 + battle)), source);
            return session.State;
        }

        private sealed class DuelGround
        {
            public OpponentSO Opponent { get; private set; }
            public MapActor Placement { get; private set; }
            public GridModel Grid { get; private set; }
            public List<int> Audience { get; private set; }
            public Coord PlayerCell { get; private set; }
            public Coord OpponentCell => new(Placement.X, Placement.Y);
            public List<KeyValuePair<int, Coord>> Spectators { get; private set; }
            public List<string> Pool { get; private set; }

            public static DuelGround For(string id, OpponentSO opponent)
            {
                var (map, placement) = OpponentRosterTests.Placement(id);
                var grid = BuildGrid(map, placement, out var audience);
                var playerCell = ChallengeCell(map, grid, placement);
                var arena = DuelCrowdPlanner.Plan(grid, playerCell, new Coord(placement.X, placement.Y), null, Seed);
                return new DuelGround
                {
                    Opponent = opponent,
                    Placement = placement,
                    Grid = grid,
                    Audience = audience,
                    PlayerCell = playerCell,
                    Spectators = arena.Seats.Select((seat, index) => new KeyValuePair<int, Coord>(BattleCrowd.IdFor(index), seat.Seat)).ToList(),
                    Pool = opponent.FallacyPool.Select(fallacy => fallacy.Id).ToList()
                };
            }
        }

        private static IEnumerable<string> OpponentIds() => DataTools.OpponentIds();

        private static List<Argument> Bank(string fallacyId)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var arguments = new List<Argument>();
            ArgumentJson.ParseValidList(System.IO.File.ReadAllText($"{DataTools.FallbackFolder}/{fallacyId}.json"), new ArgumentValidator(catalog.RefutableIds), arguments);
            return arguments;
        }

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
                        Level = (byte)map.LevelAt(x, y),
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
