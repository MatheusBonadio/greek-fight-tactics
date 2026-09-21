using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class MapLayoutTests
    {
        private const string PlayerCharacter = "kleia";
        private const string StallTile = "prop_market_stall";
        private const string WheelTile = "prop_potters_wheel";
        private const string KilnTile = "block_kiln";
        private const int StallDistance = 2;
        private const int AudienceDistance = 3;
        private const int MinimumAudience = 2;

        [TestCase(SandboxTools.MapPath)]
        [TestCase(SandboxTools.KerameikosMapPath)]
        public void Map_HasTheSandboxSizeAndUsesOnlyKnownTiles(string mapPath)
        {
            var map = MapLayout.Load(mapPath);

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
        public void Kerameikos_StartsKleiaInTheWorkshopBesideThePottersWheel()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var kleia = map.Actors[0];

            Assert.AreEqual(PlayerCharacter, kleia.Character);
            Assert.IsTrue(Cells(map).Any(cell => map.BlockAt(cell.X, cell.Y) == WheelTile && Distance(cell, (kleia.X, kleia.Y)) <= StallDistance), "Kleia paints amphorae beside the wheel");
            Assert.IsTrue(Cells(map).Any(cell => map.BlockAt(cell.X, cell.Y) == KilnTile), "the workshop has a kiln");
            CollectionAssert.DoesNotContain(map.Actors.Select(actor => actor.Character), SandboxTools.BattleOpponentId);
        }

        [TestCase(SandboxTools.MapPath)]
        [TestCase(SandboxTools.KerameikosMapPath)]
        public void Map_EveryWalkableCellIsReachableFromKleia(string mapPath)
        {
            var map = MapLayout.Load(mapPath);
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
        public void Exits_LeadToAFreeArrivalCellInTheDestinationMap()
        {
            foreach (var entry in SandboxTools.Maps)
            {
                var map = MapLayout.Load(entry.MapPath);
                Assert.IsNotEmpty(map.Exits, $"{entry.LocationId} has no way out");
                foreach (var exit in map.Exits)
                {
                    var destination = SandboxTools.Maps.Single(other => other.LocationId == exit.Location);
                    Assert.AreEqual(Path.GetFileNameWithoutExtension(destination.ScenePath), LocationScenes.SceneFor(exit.Location));

                    var target = MapLayout.Load(destination.MapPath);
                    Assert.IsTrue(target.IsWalkable(exit.ArrivalX, exit.ArrivalY), $"the arrival of {entry.LocationId} into {exit.Location} is blocked");
                    Assert.IsFalse(target.Actors.Skip(1).Any(actor => actor.X == exit.ArrivalX && actor.Y == exit.ArrivalY), "the arrival lands on an npc; only the player may stand there");
                    Assert.IsFalse(target.Exits.Any(other => other.X == exit.ArrivalX && other.Y == exit.ArrivalY), "the arrival lands on another exit, which would bounce the player back");
                }
            }
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

        [Test]
        public void Validate_AcceptsTalkingNpcsAndTriggersThatShareTheirOnceFlag()
        {
            var map = Tiny();
            map.Actors.Add(new MapActor { Character = "citizen", X = 1, Y = 1, Dialogue = "chat" });
            map.Triggers.Add(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = "saw_scene" });
            map.Triggers.Add(new MapTrigger { X = 0, Y = 1, Dialogue = "scene", Flag = "saw_scene" });

            Assert.DoesNotThrow(() => map.Validate());
            CollectionAssert.AreEquivalent(new[] { "chat", "scene" }, map.DialogueIds);
        }

        [Test]
        public void Validate_RejectsTriggersThatCannotFireOnceOnAFreeCell()
        {
            Assert.Throws<InvalidOperationException>(() => WithTrigger(new MapTrigger { X = 2, Y = 0, Dialogue = "scene", Flag = "saw_scene" }).Validate(), "outside the map");
            Assert.Throws<InvalidOperationException>(() => WithTrigger(new MapTrigger { X = 0, Y = 0, Dialogue = "scene", Flag = "saw_scene" }).Validate(), "on the player");
            Assert.Throws<InvalidOperationException>(() => WithTrigger(new MapTrigger { X = 1, Y = 0, Dialogue = " ", Flag = "saw_scene" }).Validate(), "no dialogue");
            Assert.Throws<InvalidOperationException>(() => WithTrigger(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = "Saw Scene" }).Validate(), "bad flag");
            Assert.Throws<InvalidOperationException>(() => WithTrigger(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = SaveFlags.Act }).Validate(), "save field instead of a story flag");

            var blocked = WithTrigger(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = "saw_scene" });
            blocked.Blocks[0] = "-C";
            Assert.Throws<InvalidOperationException>(() => blocked.Validate(), "on a block");

            var onExit = WithTrigger(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = "saw_scene" });
            onExit.Exits.Add(new MapExit { X = 1, Y = 0, Location = "agora" });
            Assert.Throws<InvalidOperationException>(() => onExit.Validate(), "on an exit");

            var twice = WithTrigger(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = "saw_scene" });
            twice.Triggers.Add(new MapTrigger { X = 1, Y = 0, Dialogue = "other", Flag = "saw_other" });
            Assert.Throws<InvalidOperationException>(() => twice.Validate(), "two triggers on one cell");

            var talkingPlayer = Tiny();
            talkingPlayer.Actors[0].Dialogue = "monologue";
            Assert.Throws<InvalidOperationException>(() => talkingPlayer.Validate(), "the player talks by walking up to others");
        }

        private static MapLayout WithTrigger(MapTrigger trigger)
        {
            var map = Tiny();
            map.Triggers.Add(trigger);
            return map;
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
