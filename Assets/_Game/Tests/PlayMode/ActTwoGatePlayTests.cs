using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class ActTwoGatePlayTests
    {
        private const float TimeoutSeconds = 10f;
        private const float CutsceneTimeoutSeconds = 60f;
        private const int MaxConfirms = 256;
        private const int IdleFrames = 30;

        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreData()
        {
            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AcademyRoad_IsOutOfReachInTheFirstActAndOpensWithTheSecond()
        {
            var agora = default(Map);
            yield return Enter(LocationScenes.AgoraScene, loaded => agora = loaded);
            var road = agora.Sandbox.Exits.Single(exit => exit.Location == LocationScenes.Academy);
            var player = agora.Sandbox.Player.Walker;
            Assert.AreEqual(Act.One, agora.Story.Story.ActFlag, "a new game is in the first act");

            AssertClosed(agora, road, "a new game");
            var beside = new Coord(road.Cell.X, road.Cell.Y - 1);
            Assert.IsTrue(agora.Sandbox.Grid[beside].IsPassable);
            player.Teleport(beside, Facing.NorthWest);
            yield return Frames(IdleFrames);
            Assert.IsFalse(agora.Sandbox.Grid[road.Cell].Revealed, "standing next to the road does not lift its fog in the first act");
            Assert.IsFalse(agora.Sandbox.TryMovePlayerTo(road.Cell), "ActFlag = 1: Kleia cannot walk onto the Academy road");
            Assert.IsTrue(agora.Sandbox.Cursor.TryPoint(road.Cell));
            Assert.IsNull(agora.Sandbox.HoveredExit, "a closed exit has no name to show");
            agora.Sandbox.ConfirmCursor();
            yield return Frames(IdleFrames);
            Assert.AreEqual(LocationScenes.AgoraScene, SceneManager.GetActiveScene().name, "the Academy never loads in the first act");
            Assert.AreEqual(beside, player.Cell);

            Assert.IsTrue(agora.Story.Remember(new StoryEffect(SaveFlags.Act, "2")), "the seal of the archive sets ActFlag = 2");
            Assert.IsTrue(agora.Sandbox.IsExitOpen(road), "ActFlag = 2 opens the road");
            Assert.IsTrue(agora.Sandbox.Grid[road.Cell].Revealed, "the road comes out of the fog");
            Assert.IsTrue(agora.Sandbox.Grid[road.Cell].IsPassable);
            yield return PlayCues(agora);
            Assert.AreSame(road.Marker, agora.Sandbox.ExitsTilemap.GetTile(new Vector3Int(road.Cell.X, road.Cell.Y, 0)), "the arrow points the way");
            Assert.IsTrue(StoryCondition.Has("saw_academy_road").IsMet(agora.Story.Story), "the agora announces the road");

            Assert.IsTrue(agora.Sandbox.TryMovePlayerTo(road.Cell));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == LocationScenes.AcademyScene, TimeoutSeconds);
            Assert.AreEqual(LocationScenes.AcademyScene, SceneManager.GetActiveScene().name, "the road leads to the Academy");

            var academy = default(Map);
            yield return Enter(null, loaded => academy = loaded);
            Assert.AreEqual(LocationScenes.Academy, academy.Sandbox.LocationId);
            Assert.AreEqual(road.Arrival, academy.Sandbox.Player.Walker.Cell);
            yield return PlayCues(academy);
            Assert.IsTrue(StoryCondition.Has("reached_academy").IsMet(academy.Story.Story));
            Assert.IsTrue(academy.Services.TryLoadActiveSave(out var save));
            Assert.AreEqual(Act.Two, save.ActFlag, "the second act survives the trip");
            Assert.AreEqual(LocationScenes.Academy, save.Location);
        }

        [UnityTest]
        public IEnumerator Archive_ShowsMenexenosVoteAndTheBronzeSealStartsTheSecondAct()
        {
            var agora = default(Map);
            yield return Enter(LocationScenes.AgoraScene, loaded => agora = loaded);
            var door = agora.Sandbox.Exits.Single(exit => exit.Location == LocationScenes.Archive);
            AssertClosed(agora, door, "the first act before the trial");

            Assert.IsTrue(agora.Story.Remember(new StoryEffect(DuelStoryFlags.FlagFor("thrasybios_street_trial"), DuelStoryFlags.Won)));
            yield return PlayCues(agora);
            Assert.IsTrue(StoryCondition.Has("searching_menexeno").IsMet(agora.Story.Story), "winning the trial sends Kleia after Menexeno");
            Assert.IsTrue(agora.Sandbox.IsExitOpen(door));
            Assert.IsFalse(agora.Story.Talkers.Any(talker => talker.Dialogue == "menexeno_agora" && talker.Actor.Walker != null), "Menexeno is gone");

            agora.Sandbox.Player.Walker.Teleport(new Coord(door.Cell.X - 1, door.Cell.Y), Facing.NorthEast);
            yield return null;
            Assert.IsTrue(agora.Sandbox.TryMovePlayerTo(door.Cell));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == LocationScenes.ArchiveScene, TimeoutSeconds);

            var archive = default(Map);
            yield return Enter(null, loaded => archive = loaded);
            Assert.AreEqual(LocationScenes.Archive, archive.Sandbox.LocationId);
            yield return PlayCues(archive);
            Assert.IsTrue(StoryCondition.Has("entered_archive").IsMet(archive.Story.Story));
            var academic = archive.Story.Cutscenes.Cast["academic"];
            Assert.IsNotNull(academic.Walker, "the academic reads by the columns");

            var archivist = archive.Story.Talkers.Single();
            archive.Sandbox.Cursor.TryPoint(archivist.Actor.Walker.Cell);
            archive.Sandbox.ConfirmCursor();
            yield return WaitFor(() => archive.Story.IsTalking, TimeoutSeconds);
            Assert.AreEqual("archive_archivist", archive.Story.ActiveDialogueId);
            yield return Converse(archive);
            Assert.IsTrue(StoryCondition.Has("asked_archivist").IsMet(archive.Story.Story));

            var trigger = archive.Story.Triggers.Single();
            Assert.IsTrue(archive.Sandbox.TryMovePlayerTo(trigger.Cell));
            yield return WaitFor(() => archive.Story.Cutscenes.IsPlaying, TimeoutSeconds);
            Assert.AreEqual(trigger.Cutscene, archive.Story.Cutscenes.ActiveCutscene, "the juror list plays its scene");

            var seen = new List<string>();
            var deadline = Time.realtimeSinceStartup + CutsceneTimeoutSeconds;
            while (archive.Story.Cutscenes.IsPlaying && Time.realtimeSinceStartup < deadline)
            {
                if (archive.Story.IsTalking)
                {
                    if (seen.Count == 0 || seen[^1] != archive.Story.ActiveDialogueId)
                    {
                        seen.Add(archive.Story.ActiveDialogueId);
                    }

                    archive.Story.Advance();
                }

                yield return null;
            }

            Assert.IsFalse(archive.Story.Cutscenes.IsPlaying, "the archive scene never finished");
            CollectionAssert.AreEqual(new[] { "archive_juror_list", "archive_bronze_seal", "archive_act_two" }, seen);
            Assert.IsTrue(StoryCondition.Has("knows_menexeno_vote").IsMet(archive.Story.Story), "Kleia learns Menexeno voted for the condemnation");
            Assert.IsTrue(StoryCondition.Has("bronze_seal").IsMet(archive.Story.Story));
            Assert.AreEqual(Act.Two, archive.Story.Story.ActFlag, "the seal sets ActFlag = 2");
            Assert.IsNull(academic.Walker, "the academic walks out");
            Assert.IsTrue(archive.Services.TryLoadActiveSave(out var save));
            Assert.AreEqual(Act.Two, save.ActFlag, "ActFlag = 2 goes to the save");
            Assert.IsTrue(save.Flags.ContainsKey("knows_menexeno_vote"));

            var back = archive.Sandbox.Exits.Single();
            archive.Sandbox.Player.Walker.Teleport(new Coord(back.Cell.X + 1, back.Cell.Y), Facing.SouthWest);
            yield return null;
            Assert.IsTrue(archive.Sandbox.TryMovePlayerTo(back.Cell));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == LocationScenes.AgoraScene, TimeoutSeconds);
            yield return Enter(null, loaded => agora = loaded);
            Assert.AreEqual(back.Arrival, agora.Sandbox.Player.Walker.Cell);
            var road = agora.Sandbox.Exits.Single(exit => exit.Location == LocationScenes.Academy);
            Assert.IsTrue(agora.Sandbox.IsExitOpen(road), "back in the agora, the Academy road has come out of the fog");
            Assert.IsTrue(agora.Sandbox.Grid[road.Cell].Revealed);
            yield return PlayCues(agora);
            Assert.IsTrue(StoryCondition.Has("saw_academy_road").IsMet(agora.Story.Story));
        }

        private static void AssertClosed(Map map, MapExitLink exit, string when)
        {
            var tile = map.Sandbox.Grid[exit.Cell];
            Assert.IsTrue(exit.IsGated, $"the exit to {exit.Location} is gated");
            Assert.IsFalse(map.Sandbox.IsExitOpen(exit), $"{when}: the exit to {exit.Location} is closed");
            Assert.IsFalse(tile.Revealed, $"{when}: the exit to {exit.Location} stays under the fog");
            Assert.IsFalse(tile.IsPassable, $"{when}: nobody walks onto the exit to {exit.Location}");
            Assert.IsNull(map.Sandbox.ExitsTilemap.GetTile(new Vector3Int(exit.Cell.X, exit.Cell.Y, 0)), $"{when}: no arrow points to {exit.Location}");
        }

        private static IEnumerator Enter(string scene, Action<Map> loaded)
        {
            if (scene != null)
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            }

            yield return null;
            var map = new Map(Object.FindAnyObjectByType<GridSandboxController>(), Object.FindAnyObjectByType<ExplorationDialogueDirector>(), Object.FindAnyObjectByType<GameServices>());
            yield return WaitFor(() => map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Story.IsStoryLoaded, TimeoutSeconds);
            Assert.IsTrue(map.Sandbox.ProgressLoaded && map.Story.IsStoryLoaded, $"{map.Sandbox.LocationId} never loaded");
            loaded(map);
        }

        private static IEnumerator PlayCues(Map map)
        {
            var quiet = 0;
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (quiet < IdleFrames && Time.realtimeSinceStartup < deadline)
            {
                if (map.Story.IsTalking)
                {
                    yield return Converse(map);
                    quiet = 0;
                    continue;
                }

                quiet++;
                yield return null;
            }
        }

        private static IEnumerator Converse(Map map)
        {
            for (var confirms = 0; map.Story.IsTalking && confirms < MaxConfirms; confirms++)
            {
                map.Story.Advance();
                yield return null;
            }

            Assert.IsFalse(map.Story.IsTalking, "a conversation never closed");
            yield return null;
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private sealed class Map
        {
            public Map(GridSandboxController sandbox, ExplorationDialogueDirector story, GameServices services)
            {
                Sandbox = sandbox;
                Story = story;
                Services = services;
            }

            public GridSandboxController Sandbox { get; }

            public ExplorationDialogueDirector Story { get; }

            public GameServices Services { get; }
        }
    }
}
