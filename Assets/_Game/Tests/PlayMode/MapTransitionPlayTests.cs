using System;
using System.Collections;
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
    public sealed class MapTransitionPlayTests
    {
        private const float TimeoutSeconds = 10f;

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
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingOntoTheAgoraRoadExit_LoadsTheWorkshopWithKleiaOnTheStreet()
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return null;

            var agora = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.AreEqual(LocationScenes.Agora, agora.LocationId);
            var exit = agora.Exits.Single(link => link.Location == LocationScenes.Kerameikos);
            var player = agora.Player.Walker;
            var beside = new[] { new Coord(exit.Cell.X + 1, exit.Cell.Y), new Coord(exit.Cell.X, exit.Cell.Y + 1) }
                .First(cell => agora.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == player.ActorId));
            player.Teleport(beside, Facing.SouthWest);
            Assert.IsTrue(agora.TryMovePlayerTo(exit.Cell));

            yield return WaitFor(() => SceneManager.GetActiveScene().name == LocationScenes.KerameikosScene);
            Assert.AreEqual(LocationScenes.KerameikosScene, SceneManager.GetActiveScene().name, "walking onto the exit did not load the workshop");
            yield return null;

            var workshop = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.AreEqual(LocationScenes.Kerameikos, workshop.LocationId);
            Assert.AreEqual(exit.Arrival, workshop.Player.Walker.Cell);
            Assert.AreEqual(exit.Facing, workshop.Player.Walker.Facing);
            Assert.IsTrue(workshop.Grid[exit.Arrival].Revealed);
            Assert.IsFalse(SceneArrival.HasPending);
        }

        [UnityTest]
        public IEnumerator GoingToTheWorkshopAndBack_KeepsTheFogRevealedOnTheAgora()
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return WaitForBoot();

            var agora = Object.FindAnyObjectByType<GridSandboxController>();
            var lastColumn = agora.Grid.Width - 1;
            var farCell = Enumerable.Range(0, agora.Grid.Height)
                .Select(y => new Coord(lastColumn, agora.Grid.Height - 1 - y))
                .First(cell => !agora.Grid[cell].Revealed);
            var fog = new FogOfWar(agora.Grid);
            fog.Reveal(farCell, 0);

            var exit = agora.Exits.Single(link => link.Location == LocationScenes.Kerameikos);
            yield return WalkOnto(agora, exit.Cell);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == LocationScenes.KerameikosScene);
            yield return WaitForBoot();

            var workshop = Object.FindAnyObjectByType<GridSandboxController>();
            var back = workshop.Exits.First(link => link.Location == LocationScenes.Agora);
            yield return WalkOnto(workshop, back.Cell);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == LocationScenes.AgoraScene);
            yield return WaitForBoot();

            var returned = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.AreEqual(back.Arrival, returned.Player.Walker.Cell);
            Assert.IsTrue(returned.Grid[farCell].Revealed, $"the agora forgot the fog revealed at {farCell} before the trip");
            Assert.IsNull(returned.FogTilemap.GetTile(new Vector3Int(farCell.X, farCell.Y, 0)), "the remembered fog must be painted too");
        }

        private static IEnumerator WalkOnto(GridSandboxController map, Coord exitCell)
        {
            var player = map.Player.Walker;
            var beside = new[] { new Coord(exitCell.X + 1, exitCell.Y), new Coord(exitCell.X, exitCell.Y + 1), new Coord(exitCell.X - 1, exitCell.Y), new Coord(exitCell.X, exitCell.Y - 1) }
                .First(cell => map.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == player.ActorId)
                    && map.Exits.All(link => link.Cell != cell));
            player.Teleport(beside, Facing.SouthWest);
            yield return null;
            Assert.IsTrue(map.TryMovePlayerTo(exitCell));
        }

        private static IEnumerator WaitForBoot()
        {
            yield return null;
            var services = Object.FindAnyObjectByType<GameServices>();
            yield return WaitFor(() => services == null || services.IsBooted);
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
