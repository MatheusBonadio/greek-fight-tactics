using System.Linq;
using Game.EditorTools;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Game.Tests.EditMode.UI
{
    public sealed class SandboxSceneTests
    {
        private Scene scene;

        [OneTimeSetUp]
        public void OpenScene()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxTools.ScenePath), $"run SandboxTools.BuildSandbox to create {SandboxTools.ScenePath}");
            scene = EditorSceneManager.OpenScene(SandboxTools.ScenePath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void Grid_UsesIsometricZAsYWithTileSizedCells()
        {
            var grid = Find<UnityEngine.Grid>();

            Assert.AreEqual(GridLayout.CellLayout.IsometricZAsY, grid.cellLayout);
            Assert.AreEqual(SandboxTools.CellSize, grid.cellSize);
            Assert.AreEqual(SandboxTools.GridPosition, grid.transform.position);
        }

        [Test]
        public void Floor_CoversTwelveByTwelveWithEveryFloorTile()
        {
            var floor = Tilemap(SandboxTools.FloorTilemapName);

            Assert.AreEqual(SandboxTools.Size * SandboxTools.Size, floor.GetUsedTilesCount() == 0 ? 0 : CountTiles(floor));
            CollectionAssert.AreEquivalent(SandboxTools.FloorTileNames, UsedTileNames(floor));
        }

        [Test]
        public void Blocks_UseEveryBlockTile()
        {
            CollectionAssert.AreEquivalent(SandboxTools.BlockTileNames, UsedTileNames(Tilemap(SandboxTools.BlocksTilemapName)));
        }

        [Test]
        public void EveryTilemapRenderer_SortsTilesIndividually()
        {
            var renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TilemapRenderer>()).ToList();

            Assert.AreEqual(4, renderers.Count);
            Assert.IsTrue(renderers.All(renderer => renderer.mode == TilemapRenderer.Mode.Individual));
        }

        [Test]
        public void Fog_RendersOnItsOwnLayerAboveEverything()
        {
            var fogRenderer = Tilemap(SandboxTools.FogTilemapName).GetComponent<TilemapRenderer>();

            Assert.AreEqual(SandboxTools.FogSortingLayer, fogRenderer.sortingLayerName);
            Assert.Greater(SortingLayer.GetLayerValueFromName(SandboxTools.FogSortingLayer), SortingLayer.GetLayerValueFromName("Default"));
            var fogTiles = UsedTileNames(Tilemap(SandboxTools.FogTilemapName));
            Assert.Greater(CountTiles(Tilemap(SandboxTools.FogTilemapName)), 0);
            CollectionAssert.Contains(fogTiles, "fog");
            Assert.IsTrue(fogTiles.Intersect(SandboxTools.FogEdgeNames).Any(), "fog border should use at least one dithered edge tile");
        }

        [Test]
        public void Controller_IsWiredWithLayoutControlsActorsAndHud()
        {
            var controller = Find<GridSandboxController>();

            Assert.AreEqual(SandboxTools.Size * SandboxTools.Size, controller.Terrain.Count);
            Assert.AreEqual(SandboxTools.Size * SandboxTools.Size, controller.Costs.Count);
            Assert.IsNotNull(controller.Controls);
            Assert.IsNotNull(controller.Controls.FindActionMap(GridSandboxController.ActionMapName));
            Assert.GreaterOrEqual(controller.Actors.Count, 7);
            Assert.IsTrue(controller.Actors.All(actor => actor.SpriteRenderer.spriteSortPoint == SpriteSortPoint.Pivot));
            Assert.IsNotNull(controller.GetComponent<GridGizmoView>());

            var model = controller.BuildGrid();
            foreach (var actor in controller.Actors)
            {
                Assert.IsTrue(model[new Game.Core.Coord(actor.StartCell.x, actor.StartCell.y)].IsPassable, actor.name);
            }
        }

        [Test]
        public void Camera_FollowsThePlayerWithPixelPerfectAndMapBounds()
        {
            var camera = Find<Camera>();
            var virtualCamera = Find<CinemachineCamera>();

            Assert.IsNotNull(camera.GetComponent<PixelPerfectCamera>());
            Assert.IsNotNull(camera.GetComponent<CinemachineBrain>());
            Assert.AreEqual(RenderSetupTools.MainCameraTag, camera.tag);
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachinePositionComposer>());
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachinePixelPerfect>());
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachineConfiner2D>().BoundingShape2D);
            Assert.AreEqual(Find<GridSandboxController>().Actors[0].transform, virtualCamera.Follow);
            Assert.IsNotNull(virtualCamera.GetComponent<ExplorationCamera>());
        }

        [Test]
        public void BuildSettings_StartWithTheSandbox()
        {
            Assert.AreEqual(SandboxTools.ScenePath, EditorBuildSettings.scenes[0].path);
        }

        private T Find<T>() where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).First();

        private Tilemap Tilemap(string name) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Tilemap>(true)).First(tilemap => tilemap.name == name);

        private static int CountTiles(Tilemap tilemap)
        {
            var count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(position))
                {
                    count++;
                }
            }

            return count;
        }

        private static string[] UsedTileNames(Tilemap tilemap)
        {
            var used = new TileBase[tilemap.GetUsedTilesCount()];
            tilemap.GetUsedTilesNonAlloc(used);
            return used.Select(tile => tile.name).Distinct().ToArray();
        }
    }
}
