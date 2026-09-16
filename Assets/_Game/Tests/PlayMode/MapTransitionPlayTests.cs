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
