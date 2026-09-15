using System;
using System.Collections.Generic;
using System.Linq;
using Game.EditorTools;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class MapLayoutTests
    {
        private const string PlayerCharacter = "kleia";
        private const string StallTile = "prop_market_stall";
        private const int StallDistance = 2;
        private const int AudienceDistance = 3;
        private const int MinimumAudience = 2;

        [Test]
        public void Agora_HasTheSandboxSizeAndUsesOnlyKnownTiles()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);

            Assert.AreEqual(SandboxTools.Size, map.Width);
            Assert.AreEqual(SandboxTools.Size, map.Height);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    CollectionAssert.Contains(SandboxTools.FloorTileNames, map.FloorAt(x, y));
                    var block = map.BlockAt(x, y);
                    if (block != null)
                    {
                        CollectionAssert.Contains(SandboxTools.BlockTileNames, block);
                    }
                }
            }
        }

        [Test]
        public void Agora_StartsWithKleiaAndStagesThrasybiosAtTheMarketWithAnAudience()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var thrasybios = map.Actors.Single(actor => actor.Character == SandboxTools.BattleOpponentId);

            Assert.AreEqual(PlayerCharacter, map.Actors[0].Character);

            var nearStall = Cells(map).Any(cell => map.BlockAt(cell.X, cell.Y) == StallTile && Distance(cell, (thrasybios.X, thrasybios.Y)) <= StallDistance);
            Assert.IsTrue(nearStall, "Thrasybios sells his lessons beside a market stall");

            var audience = map.Actors.Count(actor => actor.Character == SandboxTools.AudienceCharacter && Distance((actor.X, actor.Y), (thrasybios.X, thrasybios.Y)) <= AudienceDistance);
            Assert.GreaterOrEqual(audience, MinimumAudience);
        }

        [Test]
        public void Agora_EveryWalkableCellIsReachableFromKleia()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var start = (map.Actors[0].X, map.Actors[0].Y);
            var reached = new HashSet<(int X, int Y)> { start };
            var frontier = new Queue<(int X, int Y)>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var (x, y) = frontier.Dequeue();
                foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (next.Item1 >= 0 && next.Item2 >= 0 && next.Item1 < map.Width && next.Item2 < map.Height && map.IsWalkable(next.Item1, next.Item2) && reached.Add(next))
                    {
                        frontier.Enqueue(next);
                    }
                }
            }

            var unreachable = Cells(map).Where(cell => map.IsWalkable(cell.X, cell.Y) && !reached.Contains(cell)).ToList();
            CollectionAssert.IsEmpty(unreachable, $"sealed cells: {string.Join(", ", unreachable)}");
        }

        [Test]
        public void Validate_RejectsRaggedRowsUnknownSymbolsAndBadActors()
        {
            Assert.DoesNotThrow(() => Tiny().Validate());

            var ragged = Tiny();
            ragged.Floor[1] = ".";
            Assert.Throws<InvalidOperationException>(() => ragged.Validate());

            var unknown = Tiny();
            unknown.Blocks[0] = "-?";
            Assert.Throws<InvalidOperationException>(() => unknown.Validate());

            var onWater = Tiny();
            onWater.Floor[0] = "w.";
            Assert.Throws<InvalidOperationException>(() => onWater.Validate());

            var onBlock = Tiny();
            onBlock.Blocks[0] = "C-";
            Assert.Throws<InvalidOperationException>(() => onBlock.Validate());

            var shared = Tiny();
            shared.Actors.Add(new MapActor { Character = "citizen", X = 0, Y = 0 });
            Assert.Throws<InvalidOperationException>(() => shared.Validate());
        }

        private static MapLayout Tiny() => new()
        {
            Id = "tiny",
            FloorLegend = new Dictionary<char, string> { ['.'] = "floor_marble", ['w'] = MapLayout.WaterTile },
            BlockLegend = new Dictionary<char, string> { ['C'] = "block_doric_column" },
            Floor = new List<string> { "..", ".." },
            Blocks = new List<string> { "--", "--" },
            Actors = new List<MapActor> { new() { Character = PlayerCharacter, X = 0, Y = 0 } }
        };

        private static IEnumerable<(int X, int Y)> Cells(MapLayout map) =>
            Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y)));

        private static int Distance((int X, int Y) a, (int X, int Y) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
