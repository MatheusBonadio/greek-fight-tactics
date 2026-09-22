using System.Linq;
using Game.Core;
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
        public void Floor_CoversTheWholeMapWithEveryFloorTile()
        {
            var floor = Tilemap(SandboxTools.FloorTilemapName);
            var map = MapLayout.Load(SandboxTools.MapPath);

            Assert.AreEqual(map.Width * map.Height, floor.GetUsedTilesCount() == 0 ? 0 : CountTiles(floor));
            CollectionAssert.AreEquivalent(map.FloorLegend.Values.Distinct(), UsedTileNames(floor));
        }

        [Test]
        public void Blocks_UseEveryBlockTile()
        {
            CollectionAssert.AreEquivalent(MapLayout.Load(SandboxTools.MapPath).BlockLegend.Values.Distinct(), UsedTileNames(Tilemap(SandboxTools.BlocksTilemapName)));
        }

        [Test]
        public void EveryTilemapRenderer_SortsTilesIndividually()
        {
            var renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TilemapRenderer>()).ToList();

            Assert.AreEqual(5, renderers.Count);
            Assert.IsTrue(renderers.All(renderer => renderer.mode == TilemapRenderer.Mode.Individual));
        }

        [Test]
        public void Blocks_SortBehindActorsBesideThemAndInFrontOfActorsBehindThem()
        {
            var blocks = Tilemap(SandboxTools.BlocksTilemapName);
            var block = new Coord(6, 6);

            Assert.AreEqual(0f, blocks.transform.localPosition.y, 1e-4f);
            Assert.AreEqual(SandboxTools.BlockSortLift, blocks.transform.localPosition.z * RenderSetupTools.TransparencySortAxis.z, 1e-4f);

            var blockSortY = IsoMath.GridToWorld(block).y - IsoMath.CellHeight + SandboxTools.BlockSortLift;
            foreach (var front in new[] { new Coord(5, 6), new Coord(6, 5), new Coord(5, 5) })
            {
                Assert.Less(IsoMath.GridToWorld(front).y, blockSortY, $"actor at {front} must be drawn in front of the block");
            }

            foreach (var back in new[] { new Coord(7, 6), new Coord(6, 7), new Coord(7, 7) })
            {
                Assert.Greater(IsoMath.GridToWorld(back).y, blockSortY, $"actor at {back} must be drawn behind the block");
            }
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
            var map = MapLayout.Load(SandboxTools.MapPath);

            Assert.AreEqual(map.Width * map.Height, controller.Terrain.Count);
            Assert.AreEqual(map.Width * map.Height, controller.Costs.Count);
            Assert.AreEqual(SandboxTools.FogRevealDepth, controller.FogRevealDepth, "the agora keeps the default fog depth");
            Assert.IsNotNull(controller.Controls);
            Assert.IsNotNull(controller.Controls.FindActionMap(GridSandboxController.ActionMapName));
            Assert.GreaterOrEqual(controller.Actors.Count, 7);
            Assert.IsTrue(controller.Actors.All(actor => actor.SpriteRenderer.spriteSortPoint == SpriteSortPoint.Pivot));
            Assert.IsNotNull(controller.GetComponent<GridGizmoView>());
            Assert.AreEqual(SandboxTools.AgoraLocationId, controller.LocationId);

            var model = controller.BuildGrid();
            foreach (var actor in controller.Actors)
            {
                Assert.IsTrue(model[new Game.Core.Coord(actor.StartCell.x, actor.StartCell.y)].IsPassable, actor.name);
            }
        }

        [Test]
        public void BattleDirector_WiresEveryOpponentOnTheMapWithItsActorAndPortrait()
        {
            var director = Find<SandboxBattleDirector>();
            var controller = Find<GridSandboxController>();
            var ids = DataTools.OpponentIds();
            var placed = MapLayout.Load(SandboxTools.MapPath).Actors.Skip(1).Where(actor => ids.Contains(actor.Character)).ToList();

            Assert.IsNotNull(director.PlayerPortrait);
            Assert.AreEqual(placed.Count, director.Opponents.Count);
            for (var index = 0; index < placed.Count; index++)
            {
                var entry = director.Opponents[index];
                Assert.AreEqual(placed[index].Character, entry.Opponent.Id);
                CollectionAssert.Contains(controller.Actors, entry.Actor);
                Assert.AreEqual(new Vector2Int(placed[index].X, placed[index].Y), entry.Actor.StartCell);
                Assert.AreEqual($"{entry.Opponent.Id}_{SandboxTools.PortraitRow}_0", entry.Portrait.name);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTools.SheetPathFor(entry.Opponent.Id)), entry.Actor.SpriteRenderer.sprite.texture);
            }
        }

        [Test]
        public void Camera_FollowsThePlayerWithPixelPerfectAndMapBounds()
        {
            var camera = Find<Camera>();
            var virtualCamera = Find<CinemachineCamera>();

            Assert.IsNotNull(camera.GetComponent<PixelPerfectCamera>());
            Assert.IsNotNull(camera.GetComponent<CinemachineBrain>());
            Assert.AreEqual(PixelArtImporter.PixelsPerUnit, camera.GetComponent<PixelSnappedCamera>().PixelsPerUnit);
            Assert.AreEqual(RenderSetupTools.MainCameraTag, camera.tag);
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachinePositionComposer>());
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachinePixelPerfect>());
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachineConfiner2D>().BoundingShape2D);
            Assert.AreEqual(Find<GridSandboxController>().Actors[0].transform, virtualCamera.Follow);
            Assert.IsNotNull(virtualCamera.GetComponent<ExplorationCamera>());
        }

        [Test]
        public void BuildSettings_StartWithTheMainMenuFollowedByTheSandbox()
        {
            Assert.AreEqual(SystemMenuTools.MainMenuScenePath, EditorBuildSettings.scenes[0].path);
            Assert.AreEqual(SandboxTools.ScenePath, EditorBuildSettings.scenes[1].path);
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
