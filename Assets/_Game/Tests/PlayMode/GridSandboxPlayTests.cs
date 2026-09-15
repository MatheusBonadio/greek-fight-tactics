using System.Collections;
using System.Linq;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class GridSandboxPlayTests
    {
        private const string SceneName = "GridSandbox";
        private const float WalkTimeoutSeconds = 5f;
        private const float PositionTolerance = 0.001f;

        private static readonly Coord FreeCell = new(4, 4);

        private GridSandboxController controller;
        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator LoadSandbox()
        {
            data = IsolatedDataDirectory.Enter();
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.IsNotNull(controller, "GridSandbox scene must contain a GridSandboxController");
        }

        [TearDown]
        public void ReleaseDataDirectory()
        {
            data?.Dispose();
            data = null;
        }

        [UnityTest]
        public IEnumerator Startup_BuildsTheGridBindsActorsAndRevealsAroundThePlayer()
        {
            yield return null;

            Assert.IsNotNull(controller.Grid);
            Assert.AreEqual(12, controller.Grid.Width);
            Assert.IsFalse(controller.DebugPanelVisible, "the debug panel starts hidden so players never see it");
            Assert.IsTrue(controller.Actors.All(actor => actor.Walker != null));

            var playerCell = controller.Player.Walker.Cell;
            Assert.AreEqual(controller.Player.Walker.ActorId, controller.Grid[playerCell].OccupantId);
            Assert.IsTrue(controller.Grid[playerCell].Revealed);
            Assert.AreEqual(playerCell, controller.Cursor.Cell);
            Assert.IsNull(controller.FogTilemap.GetTile(new Vector3Int(playerCell.X, playerCell.Y, 0)));
            var hidden = Enumerable.Range(0, controller.Grid.Width)
                .SelectMany(x => Enumerable.Range(0, controller.Grid.Height).Select(y => new Coord(x, y)))
                .Where(cell => !controller.Grid[cell].Revealed)
                .ToList();
            Assert.IsNotEmpty(hidden);
            Assert.IsTrue(hidden.All(cell => controller.FogTilemap.GetTile(new Vector3Int(cell.X, cell.Y, 0)) != null));
        }

        [UnityTest]
        public IEnumerator Hud_DescribesTheCellUnderTheCursor()
        {
            yield return null;

            var playerCell = controller.Player.Walker.Cell;
            var description = controller.DescribeCell(playerCell);

            StringAssert.Contains($"gx {playerCell.X}  gy {playerCell.Y}", description);
            StringAssert.Contains("terrain", description);
            StringAssert.Contains($"occupant {controller.Player.Walker.ActorId}", description);
            StringAssert.Contains("outside the grid", controller.DescribeCell(new Coord(-1, 0)));
        }

        [UnityTest]
        public IEnumerator Hud_SpawnsAndRemovesTestActors()
        {
            var before = controller.Actors.Count;

            Assert.IsTrue(controller.TrySpawnTestActor(FreeCell));
            yield return null;

            Assert.AreEqual(before + 1, controller.Actors.Count);
            Assert.IsTrue(controller.Grid[FreeCell].IsOccupied);
            Assert.IsFalse(controller.TrySpawnTestActor(FreeCell));
            Assert.IsFalse(controller.TryRemoveTestActor(controller.Player.Walker.Cell));

            Assert.IsTrue(controller.TryRemoveTestActor(FreeCell));
            yield return null;

            Assert.AreEqual(before, controller.Actors.Count);
            Assert.IsFalse(controller.Grid[FreeCell].IsOccupied);
        }

        [UnityTest]
        public IEnumerator Hud_TogglesGridGizmos()
        {
            yield return null;

            controller.GizmosVisible = false;
            Assert.IsFalse(controller.GizmosVisible);
            controller.GizmosVisible = true;
            Assert.IsTrue(controller.GizmosVisible);
        }

        [UnityTest]
        public IEnumerator Player_WalksToTheTargetAndRevealsFogOnTheWay()
        {
            var target = new Coord(2, 6);
            Assert.IsTrue(controller.TryMovePlayerTo(target));

            var elapsed = 0f;
            while (controller.Player.Walker.IsMoving && elapsed < WalkTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return null;

            Assert.AreEqual(target, controller.Player.Walker.Cell);
            Assert.Less(Vector3.Distance(IsoMath.GridToWorld(target), controller.Player.transform.position), PositionTolerance);
            Assert.IsTrue(controller.Grid[new Coord(2, 9)].Revealed);
            Assert.GreaterOrEqual(elapsed, GridWalker.SecondsPerCell * 3f);
        }
    }
}
