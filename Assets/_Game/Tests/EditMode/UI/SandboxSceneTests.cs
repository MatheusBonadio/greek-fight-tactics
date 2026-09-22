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
        private const int WaterFrames = 8;

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
        public void Water_PlaysOneAnimationInStepOnEveryWaterCell()
        {
            var floor = Tilemap(SandboxTools.FloorTilemapName);
            var map = MapLayout.Load(SandboxTools.MapPath);
            var water = Enumerable.Range(0, map.Width)
                .SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new Vector3Int(x, y, 0)))
                .Where(cell => map.FloorAt(cell.x, cell.y) == MapLayout.WaterTile)
                .ToList();
            Assert.IsNotEmpty(water);

            var tiles = water.Select(cell => floor.GetTile(cell)).Distinct().ToList();
            Assert.AreEqual(1, tiles.Count, "every water cell must share one tile, so the frames change together");
            var animated = tiles[0] as AnimatedTile;
            Assert.IsNotNull(animated, "water must be an animated tile");
            Assert.AreEqual(WaterFrames, animated.m_AnimatedSprites.Length);
            Assert.AreEqual(SandboxTools.AnimatedTileFramesPerSecond, animated.m_MinSpeed);
            Assert.AreEqual(animated.m_MinSpeed, animated.m_MaxSpeed, "a random speed per cell would break the wave between tiles");
            Assert.AreEqual(0f, animated.m_AnimationStartTime);
        }

        [Test]
        public void Blocks_KeepEveryBlockTileOfTheMapAndShowOnlyThoseOnRevealedCells()
        {
            var controller = Find<GridSandboxController>();
            var map = MapLayout.Load(SandboxTools.MapPath);
            var blocks = Tilemap(SandboxTools.BlocksTilemapName);
            var revealed = PreviewRevealed(controller);

            Assert.AreSame(blocks, controller.BlocksTilemap);
            Assert.AreEqual(map.Width * map.Height, controller.BlockTiles.Count);
            CollectionAssert.AreEquivalent(map.BlockLegend.Values.Distinct(), controller.BlockTiles.Where(tile => tile).Select(tile => tile.name).Distinct());
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var source = controller.BlockTiles[y * map.Width + x];
                    Assert.AreEqual(map.BlockAt(x, y), source ? source.name : null, $"({x}, {y})");
                    var expected = revealed[new Coord(x, y)] ? source : null;
                    Assert.AreEqual(expected, blocks.GetTile(new Vector3Int(x, y, 0)), $"the block at ({x}, {y}) must show only once its cell is revealed");
                }
            }

            Assert.Less(CountTiles(blocks), controller.BlockTiles.Count(tile => tile), "the back of the agora starts under the fog, so some blocks start hidden");
        }

        [Test]
        public void Actors_StartHiddenUnderTheFogAndShownOnRevealedCells()
        {
            var controller = Find<GridSandboxController>();
            var revealed = PreviewRevealed(controller);

            Assert.IsTrue(controller.Actors.Any(actor => !revealed[new Coord(actor.StartCell.x, actor.StartCell.y)]), "the agora hides someone under the fog");
            foreach (var actor in controller.Actors)
            {
                Assert.AreEqual(revealed[new Coord(actor.StartCell.x, actor.StartCell.y)], actor.IsShown, actor.name);
            }
        }

        [Test]
        public void EveryTilemapRenderer_SortsTilesIndividually()
        {
            var renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TilemapRenderer>()).ToList();

            Assert.AreEqual(7, renderers.Count, "floor, range, highlight, blocks, fog and the two arena veils");
            Assert.IsTrue(renderers.All(renderer => renderer.mode == TilemapRenderer.Mode.Individual));
        }

        [Test]
        public void ArenaVeil_DarkensFloorAboveTheFloorAndBlocksRightAfterEachBlock()
        {
            var director = Find<SandboxBattleDirector>();
            var veil = director.Veil;
            var floor = Tilemap(SandboxTools.FloorTilemapName).GetComponent<TilemapRenderer>();
            var range = Tilemap(SandboxTools.RangeTilemapName).GetComponent<TilemapRenderer>();
            var blocks = Tilemap(SandboxTools.BlocksTilemapName);

            Assert.IsNotNull(veil, "the battle director must darken the world outside the arena");
            Assert.AreSame(Tilemap(SandboxTools.ArenaVeilTilemapName), veil.FloorVeil);
            Assert.AreSame(Tilemap(SandboxTools.BlocksVeilTilemapName), veil.BlocksVeil);
            Assert.AreEqual(0, CountTiles(veil.FloorVeil), "the veil only exists during a battle");
            Assert.AreEqual(0, CountTiles(veil.BlocksVeil));

            var floorVeil = veil.FloorVeil.GetComponent<TilemapRenderer>();
            Assert.AreEqual(floor.sortingLayerID, floorVeil.sortingLayerID);
            Assert.Greater(floorVeil.sortingOrder, floor.sortingOrder, "the veil covers the floor");
            Assert.Less(floorVeil.sortingOrder, range.sortingOrder, "range highlights stay readable over the veil");

            var blocksVeil = veil.BlocksVeil.GetComponent<TilemapRenderer>();
            var blockLift = blocks.transform.localPosition.z * RenderSetupTools.TransparencySortAxis.z;
            var veilLift = veil.BlocksVeil.transform.localPosition.z * RenderSetupTools.TransparencySortAxis.z;
            Assert.AreEqual(blocks.GetComponent<TilemapRenderer>().sortingOrder, blocksVeil.sortingOrder);
            Assert.Less(veilLift, blockLift, "each block veil is drawn right after its block");
            Assert.Greater(veilLift, blockLift - IsoMath.CellHeight, "and before the actor on the cell in front");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Shader>(SandboxTools.ArenaVeilShaderPath));
        }

        [Test]
        public void BattleDirector_BringsACitizenCrowdAndFramesTheDuelWithoutZooming()
        {
            var director = Find<SandboxBattleDirector>();
            var crowd = director.Crowd;

            Assert.IsNotNull(crowd, "the battle director must gather a crowd");
            Assert.IsNotNull(crowd.Template);
            Assert.IsFalse(crowd.Template.gameObject.activeSelf, "the template never walks on the map itself");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTools.SheetPathFor(SandboxTools.AudienceCharacter)), crowd.Template.SpriteRenderer.sprite.texture);
            Assert.AreEqual(SpriteSortPoint.Pivot, crowd.Template.SpriteRenderer.spriteSortPoint);
            CollectionAssert.DoesNotContain(Find<GridSandboxController>().Actors, crowd.Template, "spectators are not map actors");

            var explorationCamera = director.ExplorationCamera;
            Assert.IsNotNull(explorationCamera.DuelFocus);
            Assert.AreEqual(SandboxTools.ExplorationOrthographicSize, explorationCamera.VirtualCamera.Lens.OrthographicSize);
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
        public void Fog_CoversTheFloorAndItsOverlaysButNotWhatStandsOnRevealedCells()
        {
            var fogRenderer = Tilemap(SandboxTools.FogTilemapName).GetComponent<TilemapRenderer>();
            var director = Find<SandboxBattleDirector>();

            Assert.AreEqual(Tilemap(SandboxTools.FloorTilemapName).GetComponent<TilemapRenderer>().sortingLayerID, fogRenderer.sortingLayerID, "fog, floor, blocks and actors share one sorting layer");
            foreach (var overlay in new[] { SandboxTools.FloorTilemapName, SandboxTools.RangeTilemapName, SandboxTools.HighlightTilemapName, SandboxTools.ArenaVeilTilemapName })
            {
                Assert.Greater(fogRenderer.sortingOrder, Tilemap(overlay).GetComponent<TilemapRenderer>().sortingOrder, $"the fog covers {overlay}");
            }

            Assert.Less(fogRenderer.sortingOrder, Tilemap(SandboxTools.BlocksTilemapName).GetComponent<TilemapRenderer>().sortingOrder, "a revealed block is never cut by the fog behind it");
            Assert.Less(fogRenderer.sortingOrder, director.Veil.BlocksVeil.GetComponent<TilemapRenderer>().sortingOrder);
            Assert.IsTrue(Find<GridSandboxController>().Actors.All(actor => fogRenderer.sortingOrder < actor.SpriteRenderer.sortingOrder), "a revealed actor is never cut by the fog behind him");
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
            var explorationCamera = Find<ExplorationCamera>();
            var virtualCamera = explorationCamera.VirtualCamera;
            var brain = camera.GetComponent<CinemachineBrain>();

            Assert.IsNotNull(camera.GetComponent<PixelPerfectCamera>());
            Assert.IsNotNull(brain);
            Assert.AreEqual(CinemachineBlendDefinition.Styles.EaseInOut, brain.DefaultBlend.Style);
            Assert.AreEqual(ExplorationCamera.DefaultGlideSeconds, brain.DefaultBlend.Time, "the camera glides to the duel and back");
            Assert.AreEqual(PixelArtImporter.PixelsPerUnit, camera.GetComponent<PixelSnappedCamera>().PixelsPerUnit);
            Assert.AreEqual(RenderSetupTools.MainCameraTag, camera.tag);
            Assert.AreSame(explorationCamera.gameObject, virtualCamera.gameObject);
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachinePositionComposer>());
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachinePixelPerfect>());
            Assert.IsNotNull(virtualCamera.GetComponent<CinemachineConfiner2D>().BoundingShape2D);
            Assert.AreEqual(CinemachineVirtualCameraBase.StandbyUpdateMode.Always, virtualCamera.StandbyUpdate, "the bounded camera keeps tracking Kleia during the duel so the way back is exact");
            Assert.AreEqual(Find<GridSandboxController>().Actors[0].transform, virtualCamera.Follow);
            Assert.IsTrue(virtualCamera.enabled);
        }

        [Test]
        public void DuelCamera_WaitsOffAndFramesTheDuelFocusWithoutMapBounds()
        {
            var explorationCamera = Find<ExplorationCamera>();
            var duelCamera = explorationCamera.DuelCamera;

            Assert.IsNotNull(duelCamera);
            Assert.AreEqual(ExplorationCamera.DuelCameraName, duelCamera.name);
            Assert.IsFalse(duelCamera.enabled, "the duel camera only turns on during a duel");
            Assert.AreSame(explorationCamera.DuelFocus, duelCamera.Follow);
            Assert.Greater((int)duelCamera.Priority, (int)explorationCamera.VirtualCamera.Priority);
            Assert.IsNull(duelCamera.GetComponent<CinemachineConfiner2D>(), "the duel is centred even beside the map edge");
            Assert.IsNotNull(duelCamera.GetComponent<CinemachinePositionComposer>());
            Assert.IsNotNull(duelCamera.GetComponent<CinemachinePixelPerfect>());
            Assert.AreEqual(SandboxTools.ExplorationOrthographicSize, duelCamera.Lens.OrthographicSize, "the duel keeps the exploration zoom");
        }

        [Test]
        public void BuildSettings_StartWithTheMainMenuFollowedByTheSandbox()
        {
            Assert.AreEqual(SystemMenuTools.MainMenuScenePath, EditorBuildSettings.scenes[0].path);
            Assert.AreEqual(SandboxTools.ScenePath, EditorBuildSettings.scenes[1].path);
        }

        private static System.Collections.Generic.Dictionary<Coord, bool> PreviewRevealed(GridSandboxController controller)
        {
            var kleia = new Coord(controller.Actors[0].StartCell.x, controller.Actors[0].StartCell.y);
            var revealed = new System.Collections.Generic.Dictionary<Coord, bool>();
            for (var y = 0; y < controller.Height; y++)
            {
                for (var x = 0; x < controller.Width; x++)
                {
                    var cell = new Coord(x, y);
                    revealed[cell] = x + y < controller.FogRevealDepth || System.Math.Abs(x - kleia.X) + System.Math.Abs(y - kleia.Y) <= FogOfWar.DefaultRevealRadius;
                }
            }

            return revealed;
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
