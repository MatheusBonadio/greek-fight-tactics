using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.UI
{
    public sealed class MapLayoutTests
    {
        private const string PlayerCharacter = "kleia";
        private const string ThrasybiosCharacter = "thrasybios";
        private const string StallTile = "prop_market_stall";
        private const string WheelTile = "prop_potters_wheel";
        private const string KilnTile = "block_kiln";
        private const int StallDistance = 2;
        private const int AudienceDistance = 3;
        private const int MinimumAudience = 2;
        private const int AgoraSize = 12;
        private const int KerameikosSize = 16;
        private const string AmphoraTile = "prop_amphora_stack";
        private const string StreetTile = "floor_dirt";
        private const string AristeuCharacter = "aristeu";
        private const string MenexenoCharacter = "menexeno";
        private const int MinimumAmphoraStacks = 3;

        [TestCase(SandboxTools.MapPath, AgoraSize)]
        [TestCase(SandboxTools.KerameikosMapPath, KerameikosSize)]
        public void Map_TakesItsSizeFromItsJsonAndUsesOnlyKnownTiles(string mapPath, int size)
        {
            var map = MapLayout.Load(mapPath);

            Assert.AreEqual(size, map.Width);
            Assert.AreEqual(size, map.Height);
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
            var thrasybios = map.Actors.Single(actor => actor.Character == ThrasybiosCharacter);

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
            CollectionAssert.IsEmpty(map.Actors.Select(actor => actor.Character).Intersect(DataTools.OpponentIds()), "the workshop is the tutorial: nobody picks a fight there");
        }

        [Test]
        public void Kerameikos_StacksAmphoraeAndLeavesForTheAgoraFromTheStreetEdge()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);

            Assert.GreaterOrEqual(Cells(map).Count(cell => map.BlockAt(cell.X, cell.Y) == AmphoraTile), MinimumAmphoraStacks);
            Assert.IsTrue(map.Exits.Any(exit => exit.Location == SandboxTools.AgoraLocationId), "the workshop street leads to the agora");
            foreach (var exit in map.Exits)
            {
                Assert.AreEqual(StreetTile, map.FloorAt(exit.X, exit.Y), $"the exit at ({exit.X}, {exit.Y}) leaves from the street");
                Assert.IsTrue(exit.X == 0 || exit.Y == 0 || exit.X == map.Width - 1 || exit.Y == map.Height - 1, $"the exit at ({exit.X}, {exit.Y}) is not on the map edge");
            }
        }

        [Test]
        public void Kerameikos_TeachesEachTutorialStepOnceThroughItsOwnFlag()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var start = map.Cues.Single(cue => cue.On == ExplorationCue.MapStart);
            var fog = map.Cues.Single(cue => cue.On == ExplorationCue.FogRevealed);
            var walk = map.Cues.Single(cue => cue.On == ExplorationCue.WalkFinished);

            Assert.AreEqual(map.Cues.Count, map.Cues.Select(cue => cue.Flag).Distinct().Count(), "every tutorial step has its own once flag");
            Assert.IsTrue(start.Conditions.All(condition => condition.Absent), "the first step waits for nothing");
            Assert.IsTrue(fog.Conditions.Any(condition => condition.Flag == start.Flag && !condition.Absent), "the fog step comes after the first steps");
            Assert.IsTrue(walk.Conditions.Any(condition => condition.Flag == fog.Flag && !condition.Absent), "the talking hint comes after the fog step");
            Assert.AreEqual(0, map.FogRevealDepth, "the workshop starts under the fog and reveals itself as Kleia walks");
        }

        [Test]
        public void Kerameikos_KeepsAristeuInTheWorkshopAndBringsMenexenoToTheDoorOnlyAfterTheThrasybiosDuel()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var duelFlag = DuelStoryFlags.FlagFor(ThrasybiosCharacter);
            var aristeu = map.Actors.Single(actor => actor.Character == AristeuCharacter);
            var menexeno = map.Actors.Single(actor => actor.Character == MenexenoCharacter);
            var scene = map.Triggers.Single(trigger => trigger.Dialogue == menexeno.Dialogue);

            Assert.IsFalse(aristeu.IsConditional, "Aristeu is always home");
            Assert.IsNotEmpty(aristeu.Dialogue);
            var wonDuel = new StoryCondition(duelFlag, DuelStoryFlags.Won);
            CollectionAssert.AreEqual(new[] { wonDuel.ToString() }, menexeno.StoryConditions.Select(condition => condition.ToString()), "Menexeno follows Kleia home only after she beats Thrasybios");
            Assert.AreEqual(1, Distance((scene.X, scene.Y), (menexeno.X, menexeno.Y)), "the door scene plays beside Menexeno");
            CollectionAssert.Contains(scene.StoryConditions.Select(condition => condition.ToString()).ToList(), wonDuel.ToString());
            Assert.IsTrue(scene.StoryConditions.Any(condition => condition.Flag == ActionUnlocks.MaieuticsFlag && condition.Absent), "the door scene stops once Menexeno taught maieutics");

            var home = Flood(map, (map.Actors[0].X, map.Actors[0].Y), cell => cell != (scene.X, scene.Y));
            Assert.IsFalse(map.Exits.Any(exit => home.Contains((exit.X, exit.Y))), "coming home from the agora always crosses the door scene");
        }

        [Test]
        public void Agora_PlacesEveryActOneOpponentOnceWithAggroZonesThatNeverOverlap()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var opponents = Opponents(map);
            var actOne = DataTools.OpponentIds().Where(id => LoadOpponent(id).Act == Game.Core.Act.One).ToList();

            CollectionAssert.AreEquivalent(actOne, opponents.Select(entry => entry.Actor.Character));
            for (var first = 0; first < opponents.Count; first++)
            {
                for (var second = first + 1; second < opponents.Count; second++)
                {
                    var (a, b) = (opponents[first], opponents[second]);
                    Assert.Greater(Distance((a.Actor.X, a.Actor.Y), (b.Actor.X, b.Actor.Y)), a.Radius + b.Radius, $"the aggro zones of {a.Actor.Character} and {b.Actor.Character} overlap");
                }
            }
        }

        [Test]
        public void Agora_TalkingNpcsCanBeReachedWithoutProvokingADuel()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var standing = new HashSet<(int X, int Y)>(map.Actors.Skip(1).Select(actor => (actor.X, actor.Y)));
            var reached = Flood(map, (map.Actors[0].X, map.Actors[0].Y), cell => !standing.Contains(cell));
            var opponents = Opponents(map);

            foreach (var talker in map.Actors.Skip(1).Where(actor => !string.IsNullOrEmpty(actor.Dialogue)))
            {
                var beside = new[] { (talker.X + 1, talker.Y), (talker.X - 1, talker.Y), (talker.X, talker.Y + 1), (talker.X, talker.Y - 1) }
                    .Where(reached.Contains)
                    .ToList();

                Assert.IsNotEmpty(beside, $"nobody can stand beside {talker.Character} to talk");
                foreach (var cell in beside)
                {
                    foreach (var entry in opponents)
                    {
                        Assert.Greater(Distance(cell, (entry.Actor.X, entry.Actor.Y)), entry.Radius, $"talking to {talker.Character} from {cell} would provoke {entry.Actor.Character}");
                    }
                }
            }
        }

        [Test]
        public void Agora_AggroZonesStayClearOfWhereKleiaEntersTheMap()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var entries = SandboxTools.Maps
                .SelectMany(entry => MapLayout.Load(entry.MapPath).Exits)
                .Where(exit => exit.Location == SandboxTools.AgoraLocationId)
                .Select(exit => (X: exit.ArrivalX, Y: exit.ArrivalY))
                .Append((X: map.Actors[0].X, Y: map.Actors[0].Y))
                .Concat(map.Exits.Select(exit => (X: exit.X, Y: exit.Y)))
                .ToList();

            foreach (var entry in Opponents(map))
            {
                foreach (var cell in entries)
                {
                    Assert.Greater(Distance(cell, (entry.Actor.X, entry.Actor.Y)), entry.Radius, $"{entry.Actor.Character} would pick a fight at {cell}, where Kleia enters or leaves the map");
                }
            }
        }

        [TestCase(SandboxTools.MapPath)]
        [TestCase(SandboxTools.KerameikosMapPath)]
        public void Map_StandingActorsHideAtMostTheNookRightBehindThem(string mapPath)
        {
            var map = MapLayout.Load(mapPath);
            var standing = new HashSet<(int X, int Y)>(map.Actors.Skip(1).Select(actor => (actor.X, actor.Y)));
            var reached = Flood(map, (map.Actors[0].X, map.Actors[0].Y), cell => !standing.Contains(cell));

            var sealedOff = Cells(map)
                .Where(cell => map.IsWalkable(cell.X, cell.Y) && !standing.Contains(cell) && !reached.Contains(cell))
                .Where(cell => !standing.Any(actor => Distance(cell, actor) == 1))
                .ToList();
            CollectionAssert.IsEmpty(sealedOff, $"an actor blocks the only way to: {string.Join(", ", sealedOff)}");

            foreach (var entry in Opponents(map))
            {
                Assert.IsTrue(reached.Any(cell => Distance(cell, (entry.Actor.X, entry.Actor.Y)) <= entry.Radius), $"Kleia can never walk up to {entry.Actor.Character}");
            }
        }

        [TestCase(SandboxTools.MapPath)]
        [TestCase(SandboxTools.KerameikosMapPath)]
        public void Map_EveryWalkableCellIsReachableFromKleia(string mapPath)
        {
            var map = MapLayout.Load(mapPath);
            var reached = Flood(map, (map.Actors[0].X, map.Actors[0].Y), _ => true);

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

        [Test]
        public void Validate_AcceptsConditionalNpcsTriggersAndCues()
        {
            var map = Tiny();
            map.Actors.Add(new MapActor { Character = "citizen", X = 1, Y = 1, Dialogue = "chat", Conditions = { MapCondition.Has("met") } });
            map.Triggers.Add(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = "saw_scene", Conditions = { MapCondition.Lacks("left") } });
            map.Cues.Add(new MapCue { On = ExplorationCue.MapStart, Dialogue = "intro", Flag = "saw_intro", Conditions = { MapCondition.Lacks("left") } });

            Assert.DoesNotThrow(() => map.Validate());
            CollectionAssert.AreEquivalent(new[] { "chat", "scene", "intro" }, map.DialogueIds);
        }

        [Test]
        public void Validate_RejectsAWaitingPlayerBadConditionsBadCuesAndNegativeFog()
        {
            var waitingPlayer = Tiny();
            waitingPlayer.Actors[0].Conditions.Add(MapCondition.Has("met"));
            Assert.Throws<InvalidOperationException>(() => waitingPlayer.Validate(), "the player is always on the map");

            var badActor = Tiny();
            badActor.Actors.Add(new MapActor { Character = "citizen", X = 1, Y = 1, Conditions = { MapCondition.Has("Not Snake") } });
            Assert.Throws<InvalidOperationException>(() => badActor.Validate(), "actor condition with a bad flag");

            var badTrigger = WithTrigger(new MapTrigger { X = 1, Y = 0, Dialogue = "scene", Flag = "saw_scene", Conditions = { new MapCondition { Flag = "met", EqualsValue = "yes", Absent = true } } });
            Assert.Throws<InvalidOperationException>(() => badTrigger.Validate(), "absent conditions cannot compare values");

            Assert.Throws<InvalidOperationException>(() => WithCue(new MapCue { On = ExplorationCue.MapStart, Dialogue = " ", Flag = "saw_intro" }).Validate(), "cue without dialogue");
            Assert.Throws<InvalidOperationException>(() => WithCue(new MapCue { On = ExplorationCue.MapStart, Dialogue = "intro", Flag = SaveFlags.Act }).Validate(), "cue flag must be a story flag");
            Assert.Throws<InvalidOperationException>(() => WithCue(new MapCue { On = (ExplorationCue)99, Dialogue = "intro", Flag = "saw_intro" }).Validate(), "unknown cue event");
            Assert.Throws<InvalidOperationException>(() => WithCue(new MapCue { On = ExplorationCue.FogRevealed, Dialogue = "intro", Flag = "saw_intro", Conditions = { MapCondition.Has(null) } }).Validate(), "cue condition without flag");

            var negativeFog = Tiny();
            negativeFog.FogRevealDepth = -1;
            Assert.Throws<InvalidOperationException>(() => negativeFog.Validate());
        }

        [Test]
        public void Json_ReadsConditionsCuesAndFogDepthInTheDialogueConditionShape()
        {
            const string json = "{\"id\":\"tiny\",\"fogRevealDepth\":0,\"floorLegend\":{\".\":\"floor_marble\"},\"blockLegend\":{}," +
                "\"floor\":[\"..\",\"..\"],\"blocks\":[\"--\",\"--\"]," +
                "\"actors\":[{\"character\":\"kleia\",\"x\":0,\"y\":0},{\"character\":\"citizen\",\"x\":1,\"y\":1,\"conditions\":[{\"flag\":\"act\",\"equals\":\"1\"}]}]," +
                "\"cues\":[{\"on\":\"WalkFinished\",\"dialogue\":\"hint\",\"flag\":\"saw_hint\",\"conditions\":[{\"flag\":\"met\",\"absent\":true}]}]}";

            var map = Newtonsoft.Json.JsonConvert.DeserializeObject<MapLayout>(json);
            map.Validate();

            Assert.AreEqual(0, map.FogRevealDepth);
            Assert.AreEqual("act=1", map.Actors[1].StoryConditions.Single().ToString());
            Assert.AreEqual(ExplorationCue.WalkFinished, map.Cues[0].On);
            Assert.AreEqual("!met", map.Cues[0].StoryConditions.Single().ToString());
        }

        private static MapLayout WithCue(MapCue cue)
        {
            var map = Tiny();
            map.Cues.Add(cue);
            return map;
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

        private static HashSet<(int X, int Y)> Flood(MapLayout map, (int X, int Y) start, Func<(int X, int Y), bool> canEnter)
        {
            var reached = new HashSet<(int X, int Y)> { start };
            var frontier = new Queue<(int X, int Y)>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var (x, y) = frontier.Dequeue();
                foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (next.Item1 >= 0 && next.Item2 >= 0 && next.Item1 < map.Width && next.Item2 < map.Height && map.IsWalkable(next.Item1, next.Item2) && canEnter(next) && reached.Add(next))
                    {
                        frontier.Enqueue(next);
                    }
                }
            }

            return reached;
        }

        private static List<(MapActor Actor, int Radius)> Opponents(MapLayout map)
        {
            var ids = new HashSet<string>(DataTools.OpponentIds());
            return map.Actors.Skip(1)
                .Where(actor => ids.Contains(actor.Character))
                .Select(actor => (Actor: actor, Radius: LoadOpponent(actor.Character).AggroRadius))
                .ToList();
        }

        private static OpponentSO LoadOpponent(string id)
        {
            var opponent = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(id));
            Assert.IsNotNull(opponent, $"run DataTools.Regenerate to create {DataTools.OpponentPath(id)}");
            return opponent;
        }

        private static int Distance((int X, int Y) a, (int X, int Y) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
