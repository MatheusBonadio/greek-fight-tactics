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

namespace Game.Tests.PlayMode
{
    public sealed class GridSandboxPlayTests
    {
        private const string SceneName = "GridSandbox";
        private const float WalkTimeoutSeconds = 5f;
        private const float PositionTolerance = 0.001f;

        private const float ArtPixelsPerUnit = 16f;
        private const int SettleFrames = 60;
        private const int MinimumSampledFrames = 10;
        private const string MapBoundsComponent = "CinemachineConfiner2D";

        private static readonly Coord FreeCell = new(4, 4);
        private static readonly Coord JitterWalkStart = new(0, 1);
        private static readonly Coord JitterWalkEnd = new(0, 6);

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
        public IEnumerator Fog_HidesBlocksAndActorsUntilTheirCellIsRevealed()
        {
            yield return null;

            var grid = controller.Grid;
            var blocks = controller.BlocksTilemap;
            var hiddenActor = controller.Actors.First(actor => actor.Walker != null && !grid[actor.Walker.Cell].Revealed);
            var hiddenBlocks = BlockCells().Where(cell => !grid[cell].Revealed).ToList();
            Assert.IsFalse(hiddenActor.IsShown, $"{hiddenActor.name} stands under the fog, so nothing of him may show");
            Assert.IsNotEmpty(hiddenBlocks, "the back of the agora starts under the fog");
            foreach (var cell in BlockCells())
            {
                var expected = grid[cell].Revealed ? BlockTileAt(cell) : null;
                Assert.AreEqual(expected, blocks.GetTile(new Vector3Int(cell.X, cell.Y, 0)), $"the block at {cell} must follow the fog");
            }

            Assert.IsTrue(controller.Actors.Where(actor => actor.Walker != null && grid[actor.Walker.Cell].Revealed).All(actor => actor.IsShown), "everyone on a revealed cell shows");

            var fog = new FogOfWar(grid);
            fog.Reveal(hiddenActor.Walker.Cell);
            foreach (var cell in hiddenBlocks)
            {
                fog.Reveal(cell, 0);
            }

            yield return null;

            Assert.IsTrue(hiddenActor.IsShown, $"{hiddenActor.name} shows once his cell is revealed");
            foreach (var cell in hiddenBlocks)
            {
                Assert.AreEqual(BlockTileAt(cell), blocks.GetTile(new Vector3Int(cell.X, cell.Y, 0)), $"the block at {cell} shows once its cell is revealed");
            }
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
        public IEnumerator Camera_FollowsAStraightWalkWithoutPixelJitter()
        {
            var camera = Camera.main;
            Assert.IsNotNull(camera, "GridSandbox scene must have a main camera");
            var confiner = Object.FindAnyObjectByType<ExplorationCamera>().GetComponent(MapBoundsComponent) as Behaviour;
            Assert.IsNotNull(confiner, "the exploration camera must be confined to the map");
            confiner.enabled = false;
            controller.Player.Walker.Teleport(JitterWalkStart, Facing.NorthWest);
            for (var frame = 0; frame < SettleFrames; frame++)
            {
                yield return null;
            }

            var cameraStart = camera.transform.position;
            var probe = new GameObject("jitter probe").AddComponent<LateFrameProbe>();
            probe.Watch(controller.Player.transform, camera.transform);
            Assert.IsTrue(controller.TryMovePlayerTo(JitterWalkEnd));

            var elapsed = 0f;
            while (controller.Player.Walker.IsMoving && elapsed < WalkTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            probe.enabled = false;
            var offsets = probe.Offsets;
            Object.Destroy(probe.gameObject);

            Assert.GreaterOrEqual(Vector2.Distance(cameraStart, camera.transform.position), 1f, "the camera must travel at least one unit");
            Assert.Greater(offsets.Count, MinimumSampledFrames, "the walk must be sampled on every frame");
            AssertMovesOneWay(offsets.Select(offset => offset.x).ToList(), "x");
            AssertMovesOneWay(offsets.Select(offset => offset.y).ToList(), "y");
        }

        [UnityTest]
        public IEnumerator Camera_StaysOnOnePixelWhileConfinedToASmallMap()
        {
            var camera = Camera.main;
            controller.Player.Walker.Teleport(JitterWalkStart, Facing.NorthWest);
            for (var frame = 0; frame < SettleFrames; frame++)
            {
                yield return null;
            }

            var start = camera.transform.position;
            Assert.IsTrue(controller.TryMovePlayerTo(JitterWalkEnd));

            var elapsed = 0f;
            while (controller.Player.Walker.IsMoving && elapsed < WalkTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                Assert.AreEqual(start, camera.transform.position, "the confined camera must not wiggle while the player walks");
            }
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

        private IEnumerable<Coord> BlockCells() =>
            Enumerable.Range(0, controller.Grid.Height)
                .SelectMany(y => Enumerable.Range(0, controller.Grid.Width).Select(x => new Coord(x, y)))
                .Where(cell => BlockTileAt(cell) != null);

        private UnityEngine.Tilemaps.TileBase BlockTileAt(Coord cell) => controller.BlockTiles[cell.Y * controller.Grid.Width + cell.X];

        private static void AssertMovesOneWay(List<int> offsets, string axis)
        {
            var direction = 0;
            for (var index = 1; index < offsets.Count; index++)
            {
                var step = System.Math.Sign(offsets[index] - offsets[index - 1]);
                if (step == 0)
                {
                    continue;
                }

                Assert.IsTrue(direction == 0 || step == direction,
                    $"the player sprite jittered on {axis} relative to the camera at frame {index}: {string.Join(", ", offsets)}");
                direction = step;
            }
        }

        [DefaultExecutionOrder(int.MaxValue)]
        private sealed class LateFrameProbe : MonoBehaviour
        {
            private Transform sprite;
            private Transform view;

            public List<Vector2Int> Offsets { get; } = new();

            public void Watch(Transform target, Transform camera)
            {
                sprite = target;
                view = camera;
            }

            private void LateUpdate()
            {
                var offset = (sprite.position - view.position) * ArtPixelsPerUnit;
                Offsets.Add(new Vector2Int(Mathf.RoundToInt(offset.x), Mathf.RoundToInt(offset.y)));
            }
        }
    }
}
