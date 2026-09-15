using System;
using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public static class BattleArena
    {
        public const int Size = 8;
        public const int PlayerId = 1;
        public const int OpponentId = 2;
        public const int FirstCitizenId = 100;
        public const int PlayerMaxCoherence = 60;
        public const int OpponentMaxCoherence = 50;
        public const string StarterQuoteId = "heraclitus";
        public const bool OpponentOpens = true;

        public static BattleState CreateDuel(Act act, Profile opponentProfile, int seed, bool opponentOpens = false, BattleRules rules = null)
        {
            var random = new Random(seed);
            var grid = new GridModel(Size, Size);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    grid.TrySet(new Coord(x, y), new TileData { Terrain = TerrainId.Marble, Cost = PathFinder.MinimumStepCost, Revealed = true });
                }
            }

            var playerInitiative = random.Next(1, 20);
            var opponentInitiative = opponentOpens ? playerInitiative + 1 : random.Next(1, 20);
            var player = new Debater(PlayerId, Side.Player, Profile.Philosopher, PlayerMaxCoherence, playerInitiative, new Coord(2, 2), Facing.NorthEast);
            var opponent = new Debater(OpponentId, Side.Opponent, opponentProfile, OpponentMaxCoherence, opponentInitiative, new Coord(4, 2), Facing.SouthWest);

            var citizens = new[] { new Coord(1, 5), new Coord(5, 5) };
            for (var index = 0; index < citizens.Length; index++)
            {
                var tile = grid[citizens[index]];
                tile.OccupantId = FirstCitizenId + index;
                grid.TrySet(citizens[index], tile);
            }

            return new BattleState(
                grid,
                act,
                rules ?? new BattleRules(),
                new[] { player, opponent },
                random.Next(20, 80),
                new[] { new QuoteItem(StarterQuoteId, 10, 5, 1) },
                new[] { FirstCitizenId, FirstCitizenId + 1 });
        }
    }
}
