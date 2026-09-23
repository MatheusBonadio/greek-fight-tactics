using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Grid;
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
        private const int AgoraSize = 20;
        private const int KerameikosSize = 16;
        private const int InteriorSize = 8;
        private const string AmphoraTile = "prop_amphora_stack";
        private const string StreetTile = "floor_dirt";
        private const string AristeuCharacter = "aristeu";
        private const string MenexenoCharacter = "menexeno";
        private const int MinimumAmphoraStacks = 3;
        private const string MenexenoCutscene = "menexeno_follows_home";
        private const string DamarisCutscene = "damaris_seeks_kleia";
        private const string DamarisCharacter = "damaris";
        private const int MaxDamarisWalk = 2;

        [TestCase(SandboxTools.MapPath, AgoraSize)]
        [TestCase(SandboxTools.KerameikosMapPath, KerameikosSize)]
        [TestCase(SandboxTools.ArchiveMapPath, InteriorSize)]
        [TestCase(SandboxTools.AcademyMapPath, InteriorSize)]
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
            var aristeu = map.Actors.Single(actor => actor.Character == AristeuCharacter);
            var menexeno = map.Actors.Single(actor => actor.Character == MenexenoCharacter);
            var scenes = map.Triggers.Where(trigger => trigger.Cutscene == MenexenoCutscene).ToList();
            var wonDuel = new StoryCondition(DuelStoryFlags.FlagFor(ThrasybiosCharacter), DuelStoryFlags.Won).ToString();
            var untaught = StoryCondition.Lacks(ActionUnlocks.MaieuticsFlag).ToString();

            Assert.IsFalse(aristeu.IsConditional, "Aristeu is always home");
            Assert.IsNotEmpty(aristeu.Dialogue);
            Assert.IsTrue(string.IsNullOrEmpty(menexeno.Dialogue), "Menexeno speaks through the door cutscene");
            CollectionAssert.AreEquivalent(new[] { wonDuel, untaught }, menexeno.StoryConditions.Select(condition => condition.ToString()), "Menexeno waits at the door between the Thrasybios win and the lesson");

            var beside = new[] { (menexeno.X + 1, menexeno.Y), (menexeno.X - 1, menexeno.Y), (menexeno.X, menexeno.Y + 1), (menexeno.X, menexeno.Y - 1) }
                .Where(cell => cell.Item1 >= 0 && cell.Item2 >= 0 && cell.Item1 < map.Width && cell.Item2 < map.Height && map.IsWalkable(cell.Item1, cell.Item2))
                .ToList();
            CollectionAssert.AreEquivalent(beside, scenes.Select(scene => (scene.X, scene.Y)), "every free cell beside Menexeno starts the door scene");
            foreach (var scene in scenes)
            {
                Assert.AreEqual(map.Cutscenes.Single(cutscene => cutscene.Id == MenexenoCutscene).Id, scene.Cutscene);
                Assert.AreEqual(scenes[0].Flag, scene.Flag, "the door scene plays once, whichever side Kleia comes from");
                CollectionAssert.IsSubsetOf(new[] { wonDuel, untaught }, scene.StoryConditions.Select(condition => condition.ToString()).ToList());
            }

            var home = Flood(map, (map.Actors[0].X, map.Actors[0].Y), cell => scenes.All(scene => (scene.X, scene.Y) != cell));
            Assert.IsFalse(map.Exits.Any(exit => home.Contains((exit.X, exit.Y))), "coming home from the agora always crosses the door scene");
        }

        [Test]
        public void Kerameikos_StagesMenexenosVisitOnATimelineThatEndsWithHimWalkingAway()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var steps = map.Cutscenes.Single(cutscene => cutscene.Id == MenexenoCutscene).Steps;
            var dialogues = steps.Where(step => step.Action == CutsceneAction.Dialogue).Select(step => step.Dialogue).ToList();

            CollectionAssert.AreEqual(new[] { "menexeno_door_offer", "menexeno_door_glance", "menexeno_door_lesson", "menexeno_door_learned" }, dialogues);
            Assert.AreEqual(CutsceneAction.Face, steps[0].Action, "Kleia turns to the old man first");
            Assert.AreEqual(PlayerCharacter, steps[0].Actor);
            Assert.AreEqual(MenexenoCharacter, steps[0].Toward);

            var glance = steps.FindIndex(step => step.Dialogue == "menexeno_door_glance");
            Assert.AreEqual(CutsceneAction.Face, steps[glance - 1].Action, "he looks away before the narration says so");
            Assert.AreEqual(MenexenoCharacter, steps[glance - 1].Actor);
            Assert.IsTrue(steps[glance - 1].HasCell);
            Assert.AreEqual(StreetTile, map.FloorAt(steps[glance - 1].X.Value, steps[glance - 1].Y.Value), "he looks down the street, toward the courts");

            var leave = steps.FindIndex(step => step.Action == CutsceneAction.Leave);
            Assert.AreEqual(CutsceneAction.Walk, steps[leave - 1].Action);
            Assert.AreEqual(MenexenoCharacter, steps[leave].Actor);
            Assert.AreEqual(StreetTile, map.FloorAt(steps[leave - 1].X.Value, steps[leave - 1].Y.Value), "he leaves by the street");
            Assert.Less(steps.FindIndex(step => step.Dialogue == "menexeno_door_lesson"), leave, "he says good night before walking away");
            Assert.Less(leave, steps.FindIndex(step => step.Dialogue == "menexeno_door_learned"), "the lesson sinks in after he is gone");
        }

        [Test]
        public void Agora_PointsKleiaHomeOnceSheBeatsThrasybios()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var cue = map.Cues.Single(candidate => candidate.Dialogue == "agora_after_thrasybios");

            Assert.AreEqual(ExplorationCue.Idle, cue.On, "the hint plays as soon as the battle hands control back");
            CollectionAssert.Contains(cue.StoryConditions.Select(condition => condition.ToString()).ToList(), new StoryCondition(DuelStoryFlags.FlagFor(ThrasybiosCharacter), DuelStoryFlags.Won).ToString());
            CollectionAssert.Contains(cue.StoryConditions.Select(condition => condition.ToString()).ToList(), StoryCondition.Lacks(ActionUnlocks.MaieuticsFlag).ToString());
            Assert.IsTrue(map.Exits.Any(exit => exit.Location == SandboxTools.KerameikosLocationId), "the agora has a road home");
        }

        [Test]
        public void Agora_PlacesEveryActOneOpponentOnce()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var actOne = DataTools.OpponentIds().Where(id => LoadOpponent(id).Act == Game.Core.Act.One).ToList();

            CollectionAssert.AreEquivalent(actOne, Opponents(map).Select(actor => actor.Character));
        }

        [Test]
        public void Agora_KleiaCanStandBesideEveryoneSheTalksToOrChallenges()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var standing = new HashSet<(int X, int Y)>(map.Actors.Skip(1).Select(actor => (actor.X, actor.Y)));
            var reached = Flood(map, (map.Actors[0].X, map.Actors[0].Y), cell => !standing.Contains(cell));

            foreach (var actor in map.Actors.Skip(1).Where(candidate => !string.IsNullOrEmpty(candidate.Dialogue)).Concat(Opponents(map)))
            {
                var beside = new[] { (actor.X + 1, actor.Y), (actor.X - 1, actor.Y), (actor.X, actor.Y + 1), (actor.X, actor.Y - 1) }
                    .Where(reached.Contains)
                    .Where(cell => IsBesideOnFoot(map, cell, (actor.X, actor.Y)))
                    .ToList();

                Assert.IsNotEmpty(beside, $"nobody can stand beside {actor.Character}");
            }
        }

        [TestCase(SandboxTools.MapPath)]
        [TestCase(SandboxTools.KerameikosMapPath)]
        [TestCase(SandboxTools.ArchiveMapPath)]
        [TestCase(SandboxTools.AcademyMapPath)]
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

            foreach (var opponent in Opponents(map))
            {
                Assert.IsTrue(reached.Any(cell => IsBesideOnFoot(map, cell, (opponent.X, opponent.Y))), $"Kleia can never walk up to {opponent.Character}");
            }
        }

        [TestCase(SandboxTools.MapPath)]
        [TestCase(SandboxTools.KerameikosMapPath)]
        [TestCase(SandboxTools.ArchiveMapPath)]
        [TestCase(SandboxTools.AcademyMapPath)]
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
        public void Agora_ClimbsFromTheRoadIntoTheSquareAndUpToTheStoaAndTheOliveTerrace()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var kleia = map.Actors[0];
            var menexeno = map.Actors.Single(actor => actor.Character == MenexenoCharacter);
            var phaidra = map.Actors.Single(actor => actor.Character == "phaidra");
            var cliffs = Cells(map)
                .SelectMany(cell => new[] { (cell.X + 1, cell.Y), (cell.X, cell.Y + 1) }.Where(next => map.Contains(next.Item1, next.Item2)).Select(next => (cell, next)))
                .Count(pair => Math.Abs(map.LevelAt(pair.cell.X, pair.cell.Y) - map.LevelAt(pair.next.Item1, pair.next.Item2)) > TerrainSteps.MaxClimb);

            Assert.GreaterOrEqual(map.MaxLevel, 2, "the agora has at least three levels");
            Assert.AreEqual(0, map.LevelAt(kleia.X, kleia.Y), "the road from the Kerameikos starts at the bottom");
            Assert.Greater(map.LevelAt(menexeno.X, menexeno.Y), map.LevelAt(kleia.X, kleia.Y), "the stoa stands above the road");
            Assert.Greater(map.LevelAt(phaidra.X, phaidra.Y), 1, "the olive terrace stands high");
            Assert.Greater(cliffs, 0, "somewhere the terrain drops more than a step and walls off a level");
            foreach (var exit in map.Exits)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(exit.Label), $"the exit at ({exit.X}, {exit.Y}) names where it leads");
            }
        }

        [Test]
        public void Levels_ReadDigitRowsAndLetKleiaClimbOnlyOneStepAtATime()
        {
            var map = Tiny();
            Assert.AreEqual(0, map.LevelAt(1, 1), "a map without heights is flat");
            map.Heights = new List<string> { "01", "13" };
            map.Validate();

            Assert.AreEqual(1, map.LevelAt(1, 0));
            Assert.AreEqual(3, map.LevelAt(1, 1));
            Assert.AreEqual(3, map.MaxLevel);
            Assert.IsTrue(map.CanStep((0, 0), (1, 0)), "one level is a step");
            Assert.IsFalse(map.CanStep((1, 0), (1, 1)), "two levels are a cliff");
            Assert.IsFalse(map.CanStep((0, 0), (1, 1)), "only side neighbours are steps");
        }

        [Test]
        public void Validate_RejectsHeightsThatDoNotMatchTheFloorOrLeaveTheSortedRange()
        {
            var ragged = Tiny();
            ragged.Heights = new List<string> { "00", "0" };
            Assert.Throws<InvalidOperationException>(() => ragged.Validate(), "ragged heights");

            var missingRow = Tiny();
            missingRow.Heights = new List<string> { "00" };
            Assert.Throws<InvalidOperationException>(() => missingRow.Validate(), "one row too few");

            var tooHigh = Tiny();
            tooHigh.Heights = new List<string> { "00", "0" + (char)('0' + TerrainSorting.MaxLevel + 1) };
            Assert.Throws<InvalidOperationException>(() => tooHigh.Validate(), "above the highest sorted level");

            var letter = Tiny();
            letter.Heights = new List<string> { "0a", "00" };
            Assert.Throws<InvalidOperationException>(() => letter.Validate(), "not a digit");
        }

        [Test]
        public void Exits_PointOffTheEdgeTheyStandOnUnlessTheMapSaysOtherwise()
        {
            var map = new MapLayout { Floor = new List<string> { "...", "...", "..." } };

            Assert.AreEqual(Facing.SouthWest, map.HeadingOf(new MapExit { X = 0, Y = 1 }));
            Assert.AreEqual(Facing.SouthEast, map.HeadingOf(new MapExit { X = 1, Y = 0 }));
            Assert.AreEqual(Facing.NorthEast, map.HeadingOf(new MapExit { X = 2, Y = 1 }));
            Assert.AreEqual(Facing.NorthWest, map.HeadingOf(new MapExit { X = 1, Y = 2 }));
            Assert.AreEqual(Facing.SouthEast, map.HeadingOf(new MapExit { X = 0, Y = 0, Heading = Facing.SouthEast }));

            var blank = Tiny();
            blank.Exits.Add(new MapExit { X = 1, Y = 0, Location = "agora", Label = " " });
            Assert.Throws<InvalidOperationException>(() => blank.Validate(), "a blank label");
        }

        [Test]
        public void FaceSegments_HangFromEachCellDownToItsLowerFrontNeighboursAndTheMapBase()
        {
            var map = Tiny();
            map.Heights = new List<string> { "00", "02" };
            var faces = SandboxTools.FaceSegmentsOf(map).ToList();
            var baseLevels = SandboxTools.BaseDepthLevels;

            var raised = faces.Where(face => face.Cell == new Coord(1, 1)).ToList();
            CollectionAssert.AreEqual(new[] { 2, 1 }, raised.Select(face => face.Level), "a two level cliff hangs two segments");
            Assert.AreEqual(SideFaceTiles.TileName(SideFaceTiles.Stone, SideFaceTiles.LipBand, true, true), raised[0].Tile, "the top segment carries the lip on both lower sides");
            Assert.AreEqual(SideFaceTiles.TileName(SideFaceTiles.Stone, SideFaceTiles.BodyBand, true, true), raised[1].Tile);

            var corner = faces.Where(face => face.Cell == new Coord(0, 0)).ToList();
            Assert.AreEqual(baseLevels, corner.Count, "the front corner shows the whole base");
            Assert.IsTrue(corner.All(face => face.Tile.EndsWith("_sw_se")));

            var beside = faces.Where(face => face.Cell == new Coord(1, 0)).ToList();
            Assert.AreEqual(baseLevels, beside.Count);
            Assert.IsTrue(beside.All(face => face.Tile.EndsWith("_se") && !face.Tile.EndsWith("_sw_se")), "the cell beside the corner only hangs over the south east edge");
            CollectionAssert.AreEqual(Enumerable.Range(0, baseLevels).Select(step => -step), beside.Select(face => face.Level));

            CollectionAssert.IsEmpty(faces.Where(face => face.Cell == new Coord(0, 1) && face.Level > 0), "a ground cell beside a lower neighbour off the map only shows the base");
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

            var badExit = Tiny();
            badExit.Exits.Add(new MapExit { X = 1, Y = 0, Location = "agora", Conditions = { new MapCondition { Flag = "met", EqualsValue = "yes", NotValue = "no" } } });
            Assert.Throws<InvalidOperationException>(() => badExit.Validate(), "an exit condition cannot ask for a value and forbid another");

            var unknownAct = Tiny();
            unknownAct.Act = (Act)3;
            Assert.Throws<InvalidOperationException>(() => unknownAct.Validate(), "there are only two acts");

            var blankSheet = Tiny();
            blankSheet.Actors.Add(new MapActor { Character = "archivist", Sheet = " ", X = 1, Y = 1 });
            Assert.Throws<InvalidOperationException>(() => blankSheet.Validate(), "an actor that names a sheet must name one");

            var negativeFog = Tiny();
            negativeFog.FogRevealDepth = -1;
            Assert.Throws<InvalidOperationException>(() => negativeFog.Validate());
        }

        [Test]
        public void Validate_AcceptsCutscenesAndTheTriggersThatPlayThem()
        {
            var map = WithCast();
            map.Cutscenes.Add(Visit());
            map.Triggers.Add(new MapTrigger { X = 1, Y = 0, Cutscene = "visit", Flag = "saw_visit" });

            Assert.DoesNotThrow(() => map.Validate());
            CollectionAssert.AreEquivalent(new[] { "hello" }, map.DialogueIds, "the dialogue steps of a cutscene are checked against the library");
            CollectionAssert.AreEquivalent(new[] { PlayerCharacter, "citizen" }, map.Cutscenes[0].ActorIds);
            Assert.AreEqual(2d, map.Cutscenes[0].LastStepAt);
        }

        [Test]
        public void Validate_RejectsCutscenesThatCannotPlay()
        {
            AssertRejects(cutscene => cutscene.Id = "Not Snake", "id in snake case");
            AssertRejects(cutscene => cutscene.Steps.Clear(), "a cutscene needs steps");
            AssertRejects(cutscene => cutscene.Steps[0].At = 0d, "steps start after 0s");
            AssertRejects(cutscene => cutscene.Steps[1].At = cutscene.Steps[0].At, "steps move forward in time");
            AssertRejects(cutscene => cutscene.Steps[0].Actor = "nobody", "unknown actor");
            AssertRejects(cutscene => cutscene.Steps[0].Toward = null, "facing needs a target");
            AssertRejects(cutscene => (cutscene.Steps[0].X, cutscene.Steps[0].Y) = (0, 1), "facing an actor and a cell at once");
            AssertRejects(cutscene => cutscene.Steps[1].Dialogue = " ", "dialogue step without dialogue");
            AssertRejects(cutscene => cutscene.Steps[2].X = 5, "walking off the map");
            AssertRejects(cutscene => cutscene.Steps[2].Y = null, "walking without a destination");
            AssertRejects(cutscene => cutscene.Steps[3].Actor = PlayerCharacter, "the player never leaves the map");
            AssertRejects(cutscene => cutscene.Steps[3].Action = (CutsceneAction)99, "unknown action");

            var blocked = WithCast();
            blocked.Blocks = new List<string> { "--", "C-" };
            blocked.Cutscenes.Add(Visit());
            Assert.Throws<InvalidOperationException>(() => blocked.Validate(), "walking into a block");

            var crowd = WithCast();
            crowd.Actors.Add(new MapActor { Character = "citizen", X = 0, Y = 1 });
            crowd.Cutscenes.Add(Visit());
            crowd.Cutscenes[0].Steps[2].X = 1;
            crowd.Cutscenes[0].Steps[2].Y = 0;
            Assert.Throws<InvalidOperationException>(() => crowd.Validate(), "a cast member must be unique on the map");

            var twice = WithCast();
            twice.Cutscenes.Add(Visit());
            twice.Cutscenes.Add(Visit());
            Assert.Throws<InvalidOperationException>(() => twice.Validate(), "two cutscenes share an id");

            var unknownScene = WithCast();
            unknownScene.Triggers.Add(new MapTrigger { X = 1, Y = 0, Cutscene = "missing", Flag = "saw_visit" });
            Assert.Throws<InvalidOperationException>(() => unknownScene.Validate(), "trigger plays an unknown cutscene");

            var both = WithCast();
            both.Cutscenes.Add(Visit());
            both.Triggers.Add(new MapTrigger { X = 1, Y = 0, Dialogue = "hello", Cutscene = "visit", Flag = "saw_visit" });
            Assert.Throws<InvalidOperationException>(() => both.Validate(), "a trigger plays a dialogue or a cutscene, not both");
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

        [Test]
        public void Json_ReadsNotConditionsSheetsGatedExitsAndTheMapAct()
        {
            const string json = "{\"id\":\"tiny\",\"act\":2,\"floorLegend\":{\".\":\"floor_marble\"},\"blockLegend\":{}," +
                "\"floor\":[\"..\",\"..\"],\"blocks\":[\"--\",\"--\"]," +
                "\"actors\":[{\"character\":\"kleia\",\"x\":0,\"y\":0},{\"character\":\"archivist\",\"sheet\":\"citizen\",\"x\":1,\"y\":1,\"conditions\":[{\"flag\":\"damaris_branch\",\"not\":\"undecided\"}]}]," +
                "\"exits\":[{\"x\":1,\"y\":0,\"location\":\"agora\",\"label\":\"Para a ágora\",\"conditions\":[{\"flag\":\"searching_menexeno\"}]}]}";

            var map = Newtonsoft.Json.JsonConvert.DeserializeObject<MapLayout>(json);
            map.Validate();

            Assert.AreEqual(Act.Two, map.Act);
            Assert.AreEqual("citizen", map.Actors[1].SheetId);
            Assert.AreEqual(PlayerCharacter, map.Actors[0].SheetId, "without a sheet the character names it");
            Assert.AreEqual("damaris_branch!=undecided", map.Actors[1].StoryConditions.Single().ToString());
            Assert.AreEqual("searching_menexeno", map.Exits[0].StoryConditions.Single().ToString());
            Assert.AreEqual(Act.One, Tiny().Act, "a map without an act belongs to the first");
        }

        [Test]
        public void Json_ReadsCutscenesAndCutsceneTriggers()
        {
            const string json = "{\"id\":\"tiny\",\"floorLegend\":{\".\":\"floor_marble\"},\"blockLegend\":{}," +
                "\"floor\":[\"..\",\"..\"],\"blocks\":[\"--\",\"--\"]," +
                "\"actors\":[{\"character\":\"kleia\",\"x\":0,\"y\":0},{\"character\":\"citizen\",\"x\":1,\"y\":1}]," +
                "\"triggers\":[{\"x\":1,\"y\":0,\"cutscene\":\"visit\",\"flag\":\"saw_visit\"}]," +
                "\"cutscenes\":[{\"id\":\"visit\",\"steps\":[{\"at\":0.5,\"action\":\"Face\",\"actor\":\"citizen\",\"x\":0,\"y\":1},{\"at\":1,\"action\":\"Leave\",\"actor\":\"citizen\"}]}]}";

            var map = Newtonsoft.Json.JsonConvert.DeserializeObject<MapLayout>(json);
            map.Validate();

            Assert.AreEqual("visit", map.Triggers.Single().Cutscene);
            Assert.IsTrue(map.Triggers.Single().IsCutscene);
            var steps = map.Cutscenes.Single().Steps;
            Assert.AreEqual(CutsceneAction.Face, steps[0].Action);
            Assert.AreEqual(new Coord(0, 1), steps[0].Cell);
            Assert.AreEqual(0.5d, steps[0].At);
            Assert.AreEqual(CutsceneAction.Leave, steps[1].Action);
            Assert.IsNull(steps[1].Cell);
        }

        [Test]
        public void Validate_ShowsOrHidesOnlyRealBlocksThroughStoryFlags()
        {
            var map = Tiny();
            map.Blocks[1] = "-C";
            map.ConditionalBlocks.Add(new MapConditionalBlock { X = 1, Y = 1, Conditions = { MapCondition.Lacks("sold") } });
            Assert.DoesNotThrow(() => map.Validate());

            var empty = Tiny();
            empty.ConditionalBlocks.Add(new MapConditionalBlock { X = 1, Y = 1, Conditions = { MapCondition.Lacks("sold") } });
            Assert.Throws<InvalidOperationException>(() => empty.Validate(), "a cell without a block has nothing to hide");

            var outside = Tiny();
            outside.ConditionalBlocks.Add(new MapConditionalBlock { X = 4, Y = 1, Conditions = { MapCondition.Lacks("sold") } });
            Assert.Throws<InvalidOperationException>(() => outside.Validate(), "outside the map");

            var unconditional = Tiny();
            unconditional.Blocks[1] = "-C";
            unconditional.ConditionalBlocks.Add(new MapConditionalBlock { X = 1, Y = 1 });
            Assert.Throws<InvalidOperationException>(() => unconditional.Validate(), "a conditional block waits for a flag");

            var badFlag = Tiny();
            badFlag.Blocks[1] = "-C";
            badFlag.ConditionalBlocks.Add(new MapConditionalBlock { X = 1, Y = 1, Conditions = { MapCondition.Has("Not Snake") } });
            Assert.Throws<InvalidOperationException>(() => badFlag.Validate(), "bad flag");

            var twice = Tiny();
            twice.Blocks[1] = "-C";
            twice.ConditionalBlocks.Add(new MapConditionalBlock { X = 1, Y = 1, Conditions = { MapCondition.Lacks("sold") } });
            twice.ConditionalBlocks.Add(new MapConditionalBlock { X = 1, Y = 1, Conditions = { MapCondition.Has("bought") } });
            Assert.Throws<InvalidOperationException>(() => twice.Validate(), "one cell, one rule");
        }

        [Test]
        public void Json_ReadsConditionalBlocksAndTheHomeMap()
        {
            const string json = "{\"id\":\"tiny\",\"home\":true,\"floorLegend\":{\".\":\"floor_marble\"},\"blockLegend\":{\"C\":\"block_doric_column\"}," +
                "\"floor\":[\"..\",\"..\"],\"blocks\":[\"--\",\"-C\"]," +
                "\"actors\":[{\"character\":\"kleia\",\"x\":0,\"y\":0}]," +
                "\"conditionalBlocks\":[{\"x\":1,\"y\":1,\"conditions\":[{\"flag\":\"sold\",\"absent\":true}]}]}";

            var map = Newtonsoft.Json.JsonConvert.DeserializeObject<MapLayout>(json);
            map.Validate();

            Assert.IsTrue(map.Home);
            Assert.AreEqual("!sold", map.ConditionalBlocks.Single().StoryConditions.Single().ToString());
            Assert.IsFalse(MapLayout.Load(SandboxTools.MapPath).Home, "only the workshop is home");
            Assert.IsTrue(MapLayout.Load(SandboxTools.KerameikosMapPath).Home, "coming back to the workshop ends the day");
        }

        [Test]
        public void Kerameikos_LosesItsCustomersAndThePottersWheelAsThePressureGrows()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var deserted = StoryCondition.Lacks(StoryPressure.DesertedFlag).ToString();

            var customers = map.Actors.Where(actor => actor.Character == SandboxTools.AudienceCharacter && actor.IsConditional).ToList();
            Assert.GreaterOrEqual(customers.Count, 2, "customers wait at the workshop door");
            foreach (var customer in customers)
            {
                Assert.IsNotEmpty(customer.Dialogue, "every customer has something to say about the rumor");
                CollectionAssert.AreEqual(new[] { deserted }, customer.StoryConditions.Select(condition => condition.ToString()), "the customers leave once the workshop is deserted");
            }

            var wheel = map.ConditionalBlocks.Single(block => map.BlockAt(block.X, block.Y) == WheelTile);
            CollectionAssert.AreEqual(new[] { StoryCondition.Lacks(StoryPressure.ToolsFlag).ToString() }, wheel.StoryConditions.Select(condition => condition.ToString()), "Aristeu sells the wheel at the last stage");

            var unsold = map.ConditionalBlocks.Where(block => map.BlockAt(block.X, block.Y) == AmphoraTile).ToList();
            Assert.IsNotEmpty(unsold, "unsold amphorae pile up at the door");
            Assert.IsTrue(unsold.All(block => block.StoryConditions.Single().ToString() == StoryPressure.DesertedFlag));

            var wheelCue = map.Triggers.Single(trigger => trigger.Dialogue == "kerameikos_wheel_sold");
            CollectionAssert.Contains(wheelCue.StoryConditions.Select(condition => condition.ToString()).ToList(), StoryPressure.ToolsFlag);
            var inside = Flood(map, (map.Actors[0].X, map.Actors[0].Y), cell => cell != (wheelCue.X, wheelCue.Y));
            Assert.IsFalse(map.Exits.Any(exit => inside.Contains((exit.X, exit.Y))), "the doorway separates the workshop from the street, so Kleia always sees the empty corner on her way in");
            Assert.IsTrue(map.Cues.Any(cue => cue.On == ExplorationCue.Idle && cue.StoryConditions.Any(condition => condition.Flag == StoryPressure.DesertedFlag)), "the empty door is pointed out");
        }

        [Test]
        public void Kerameikos_SendsDamarisToTheStreetOnceTheWheelIsSoldAndUntilKleiaAnswers()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var damaris = map.Actors.Single(actor => actor.Character == DamarisCharacter);
            var expected = new[]
            {
                StoryPressure.ToolsFlag,
                ActionUnlocks.MaieuticsFlag,
                StoryCondition.Is(SaveFlags.DamarisBranch, "undecided").ToString()
            };

            Assert.IsTrue(string.IsNullOrEmpty(damaris.Dialogue), "Damaris speaks through her cutscene");
            CollectionAssert.AreEquivalent(expected, damaris.StoryConditions.Select(condition => condition.ToString()));
            Assert.AreEqual(StreetTile, map.FloorAt(damaris.X, damaris.Y), "she waits in the street, where Kleia comes home");

            var scenes = map.Triggers.Where(trigger => trigger.Cutscene == DamarisCutscene).ToList();
            var beside = new[] { (damaris.X + 1, damaris.Y), (damaris.X - 1, damaris.Y), (damaris.X, damaris.Y + 1), (damaris.X, damaris.Y - 1) }
                .Where(cell => map.Contains(cell.Item1, cell.Item2) && map.IsWalkable(cell.Item1, cell.Item2))
                .ToList();
            CollectionAssert.AreEquivalent(beside, scenes.Select(scene => (scene.X, scene.Y)), "every free cell beside Damaris starts her offer");
            Assert.IsTrue(scenes.All(scene => scene.Flag == scenes[0].Flag), "the offer plays once");
            Assert.IsTrue(scenes.All(scene => expected.All(condition => scene.StoryConditions.Select(item => item.ToString()).Contains(condition))));

            var steps = map.Cutscenes.Single(cutscene => cutscene.Id == DamarisCutscene).Steps;
            CollectionAssert.AreEqual(new[] { "damaris_offer", "damaris_technique" }, steps.Where(step => step.Action == CutsceneAction.Dialogue).Select(step => step.Dialogue));
            var leave = steps.FindIndex(step => step.Action == CutsceneAction.Leave);
            Assert.AreEqual(DamarisCharacter, steps[leave].Actor, "she leaves after the answer");
            Assert.Less(steps.FindIndex(step => step.Dialogue == "damaris_offer"), leave);
            Assert.Less(leave, steps.FindIndex(step => step.Dialogue == "damaris_technique"), "the technique sinks in after she is gone");

            var arrival = MapLayout.Load(SandboxTools.MapPath).Exits.Single(exit => exit.Location == SandboxTools.KerameikosLocationId);
            Assert.LessOrEqual(Distance((damaris.X, damaris.Y), (arrival.ArrivalX, arrival.ArrivalY)), MaxDamarisWalk, "coming home from the agora, Kleia finds Damaris waiting a step away");
            Assert.IsFalse(scenes.Any(scene => (scene.X, scene.Y) == (arrival.ArrivalX, arrival.ArrivalY)), "the offer waits for Kleia to walk up to her");
        }

        [Test]
        public void Agora_SpreadsTheRumorStageByStageAndMissesMenexenoWhileHeIsAway()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            var menexeno = map.Actors.Single(actor => actor.Character == MenexenoCharacter);
            CollectionAssert.Contains(menexeno.StoryConditions.Select(condition => condition.ToString()).ToList(), StoryCondition.Lacks(DamarisOffer.MenexenoAbsentDaysFlag).ToString(), "Menexeno stays away while his absence lasts");

            string Stage(string dialogue) => map.Cues.Single(cue => cue.Dialogue == dialogue).StoryConditions.Select(condition => condition.ToString()).Single(StoryPressure.IsStageFlag);
            Assert.AreEqual(StoryPressure.RumorFlag, Stage("agora_asebeia_rumor"), "Thrasybios accuses her after the first duel past his");
            Assert.AreEqual(StoryPressure.DesertedFlag, Stage("agora_asebeia_spreads"));
            Assert.AreEqual(StoryPressure.ToolsFlag, Stage("agora_pressure_home"), "the last stage sends Kleia home, where Damaris waits");
            Assert.IsTrue(map.Cues.Where(cue => cue.Dialogue.StartsWith("agora_")).All(cue => cue.On == ExplorationCue.Idle));

            var gone = map.Cues.Single(cue => cue.Dialogue == "agora_menexeno_away");
            CollectionAssert.Contains(gone.StoryConditions.Select(condition => condition.ToString()).ToList(), DamarisOffer.MenexenoAbsentDaysFlag);
        }

        [Test]
        public void ConditionalNeighbours_WalkInFromTheRoadInsteadOfAppearing()
        {
            var workshop = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var agora = MapLayout.Load(SandboxTools.MapPath);
            var road = workshop.Exits.Where(exit => exit.Location == SandboxTools.AgoraLocationId).Select(exit => (exit.X, exit.Y)).ToList();

            foreach (var character in new[] { MenexenoCharacter, DamarisCharacter })
            {
                var actor = workshop.Actors.Single(entry => entry.Character == character);
                Assert.IsTrue(actor.WalksIn, $"{character} comes from the agora, so he walks in");
                CollectionAssert.Contains(road, (actor.Enters.X, actor.Enters.Y), $"{character} enters by the road to the agora");
            }

            var trial = agora.Actors.Single(entry => entry.Character == "thrasybios_street_trial");
            var market = agora.Actors.Single(entry => entry.Character == "thrasybios");
            Assert.IsTrue(trial.WalksIn, "the herald calls him, and he climbs the steps in front of everyone");
            Assert.LessOrEqual(Distance((trial.Enters.X, trial.Enters.Y), (market.X, market.Y)), 2, "he starts from the market he was working");

            var goodbye = workshop.Cutscenes.Single(cutscene => cutscene.Id == "menexeno_follows_home").Steps;
            var walk = goodbye.Single(step => step.Action == CutsceneAction.Walk && step.Actor == MenexenoCharacter);
            CollectionAssert.Contains(road, (walk.X.Value, walk.Y.Value), "when the lesson ends he takes the road back to the agora");
            Assert.Less(goodbye.IndexOf(walk), goodbye.FindIndex(step => step.Action == CutsceneAction.Leave), "he only leaves the map once he is at the road");
        }

        [Test]
        public void AnEntrance_MustBeAWalkableCellThatLeadsToThePlaceTheActorWaitsIn()
        {
            AssertRejectsEntrance(actor => actor.Enters = new MapCell { X = 5, Y = 0 }, "an entrance outside the map is refused");
            AssertRejectsEntrance(actor => actor.Enters = new MapCell { X = 1, Y = 1 }, "an entrance on the cell the actor waits in is refused");
            AssertRejectsEntrance(actor => actor.Enters = new MapCell { X = 0, Y = 0 }, "an entrance on another actor is refused");

            var blocked = Tiny();
            blocked.Floor = new List<string> { "..", ".w" };
            blocked.Actors.Add(new MapActor
            {
                Character = "citizen",
                X = 1,
                Y = 0,
                Enters = new MapCell { X = 1, Y = 1 },
                Conditions = new List<MapCondition> { new() { Flag = "met_him" } }
            });
            Assert.Throws<InvalidOperationException>(() => blocked.Validate(), "an entrance in the water is refused");

            var always = Tiny();
            always.Actors.Add(new MapActor { Character = "citizen", X = 1, Y = 1, Enters = new MapCell { X = 1, Y = 0 } });
            Assert.Throws<InvalidOperationException>(() => always.Validate(), "an actor who never waits for the story has nowhere to walk in from");
        }

        private static void AssertRejectsEntrance(Action<MapActor> spoil, string reason)
        {
            var map = Tiny();
            var actor = new MapActor
            {
                Character = "citizen",
                X = 1,
                Y = 1,
                Conditions = new List<MapCondition> { new() { Flag = "met_him" } }
            };
            spoil(actor);
            map.Actors.Add(actor);
            Assert.Throws<InvalidOperationException>(() => map.Validate(), reason);
        }

        private static void AssertRejects(Action<MapCutscene> spoil, string reason)
        {
            var map = WithCast();
            var cutscene = Visit();
            spoil(cutscene);
            map.Cutscenes.Add(cutscene);
            Assert.Throws<InvalidOperationException>(() => map.Validate(), reason);
        }

        private static MapLayout WithCast()
        {
            var map = Tiny();
            map.Actors.Add(new MapActor { Character = "citizen", X = 1, Y = 1 });
            return map;
        }

        private static MapCutscene Visit() => new()
        {
            Id = "visit",
            Steps = new List<MapCutsceneStep>
            {
                new() { At = 0.5d, Action = CutsceneAction.Face, Actor = PlayerCharacter, Toward = "citizen" },
                new() { At = 1d, Action = CutsceneAction.Dialogue, Dialogue = "hello" },
                new() { At = 1.5d, Action = CutsceneAction.Walk, Actor = "citizen", X = 0, Y = 1 },
                new() { At = 2d, Action = CutsceneAction.Leave, Actor = "citizen" }
            }
        };

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
                    if (map.CanStep((x, y), next) && canEnter(next) && reached.Add(next))
                    {
                        frontier.Enqueue(next);
                    }
                }
            }

            return reached;
        }

        private static List<MapActor> Opponents(MapLayout map)
        {
            var ids = new HashSet<string>(DataTools.OpponentIds());
            return map.Actors.Skip(1).Where(actor => ids.Contains(actor.Character)).ToList();
        }

        private static OpponentSO LoadOpponent(string id)
        {
            var opponent = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(id));
            Assert.IsNotNull(opponent, $"run DataTools.Regenerate to create {DataTools.OpponentPath(id)}");
            return opponent;
        }

        private static int Distance((int X, int Y) a, (int X, int Y) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private static bool IsBesideOnFoot(MapLayout map, (int X, int Y) cell, (int X, int Y) actor) =>
            Distance(cell, actor) == 1 && Math.Abs(map.LevelAt(cell.X, cell.Y) - map.LevelAt(actor.X, actor.Y)) <= TerrainSteps.MaxClimb;
    }
}
