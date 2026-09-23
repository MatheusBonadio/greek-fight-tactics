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
        public void Floor_CoversTheWholeMapWithEveryFloorTileOnTheLevelOfItsCell()
        {
            var floor = Layer(SandboxTools.FloorTilemapName);
            var map = MapLayout.Load(SandboxTools.MapPath);

            Assert.AreEqual(map.Width * map.Height, floor.CountTiles());
            CollectionAssert.AreEquivalent(map.FloorLegend.Values.Distinct(), floor.UsedTileNames().Distinct());
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    Assert.IsTrue(floor.At(map.LevelAt(x, y)).HasTile(new Vector3Int(x, y, 0)), $"({x}, {y}) is drawn on level {map.LevelAt(x, y)}");
                }
            }
        }

        [Test]
        public void Agora_HasSeveralTerrainLevelsAndTheControllerKnowsThem()
        {
            var controller = Find<GridSandboxController>();
            var map = MapLayout.Load(SandboxTools.MapPath);
            var model = controller.BuildGrid();

            Assert.GreaterOrEqual(map.MaxLevel, 2, "the agora climbs at least two levels");
            Assert.AreEqual(map.Width * map.Height, controller.Levels.Count);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    Assert.AreEqual(map.LevelAt(x, y), model[new Coord(x, y)].Level, $"({x}, {y})");
                }
            }

            foreach (var actor in controller.Actors)
            {
                Assert.AreEqual(map.LevelAt(actor.StartCell.x, actor.StartCell.y), actor.StartLevel, actor.name);
                var surface = IsoMath.GridToSurface(new Coord(actor.StartCell.x, actor.StartCell.y), actor.StartLevel);
                Assert.AreEqual(surface.x, actor.transform.position.x, 1e-4f, actor.name);
                Assert.AreEqual(surface.y, actor.transform.position.y, 1e-4f, $"{actor.name} stands on the raised surface");
            }
        }

        [Test]
        public void RaisedLevels_AreLiftedAndSortedWithActorsByTheirGroundCell()
        {
            var map = MapLayout.Load(SandboxTools.MapPath);
            Assert.AreEqual(new Vector3(0f, 1f, -1f), RenderSetupTools.TransparencySortAxis, "TerrainSorting turns sort values into depth along this axis");
            foreach (var (name, offset) in new[]
                     {
                         (SandboxTools.FloorTilemapName, TerrainSorting.FloorOffset),
                         (SandboxTools.ExitsTilemapName, TerrainSorting.ExitOffset),
                         (SandboxTools.RangeTilemapName, TerrainSorting.RangeOffset),
                         (SandboxTools.HighlightTilemapName, TerrainSorting.HighlightOffset),
                         (SandboxTools.FogTilemapName, TerrainSorting.FogOffset),
                         (SandboxTools.ArenaVeilTilemapName, TerrainSorting.FloorVeilOffset)
                     })
            {
                var layer = Layer(name);
                Assert.AreEqual(0, layer.LowestLevel, name);
                Assert.AreEqual(map.MaxLevel, layer.HighestLevel, name);
                Assert.AreEqual(Vector3.zero, layer.At(0).transform.localPosition, $"{name} keeps the ground level where it always was");
                for (var level = 1; level <= map.MaxLevel; level++)
                {
                    var tilemap = layer.At(level);
                    Assert.AreEqual(SandboxTools.ActorSortingOrder, tilemap.GetComponent<TilemapRenderer>().sortingOrder, $"{tilemap.name} sorts with actors and blocks");
                    Assert.AreEqual(IsoMath.Lift(level), tilemap.transform.localPosition.y, 1e-4f, tilemap.name);
                    Assert.AreEqual(TerrainSorting.TilemapDepth(level, offset), tilemap.transform.localPosition.z, 1e-4f, tilemap.name);
                }
            }

            var blocks = Layer(SandboxTools.BlocksTilemapName);
            for (var level = 0; level <= map.MaxLevel; level++)
            {
                Assert.AreEqual(IsoMath.Lift(level), blocks.At(level).transform.localPosition.y, 1e-4f);
                Assert.AreEqual(TerrainSorting.TilemapDepth(level, TerrainSorting.BlockOffset), blocks.At(level).transform.localPosition.z, 1e-4f);
            }
        }

        [Test]
        public void Faces_HangUnderEveryRaisedCellAndTheMapEdgeDownToTheLowerNeighbour()
        {
            var controller = Find<GridSandboxController>();
            var map = MapLayout.Load(SandboxTools.MapPath);
            var faces = controller.FacesTilemap;
            var expected = SandboxTools.FaceSegmentsOf(map).ToList();

            Assert.AreSame(Layer(SandboxTools.FacesTilemapName), faces);
            Assert.AreEqual(1 - SandboxTools.BaseDepthLevels, faces.LowestLevel, "the map edge is a slab two levels thick");
            Assert.AreEqual(expected.Count, controller.FaceTiles.Count);
            Assert.IsTrue(expected.Any(face => face.Cell.X > 0 && face.Cell.Y > 0), "raised cells inside the map show cliff faces");
            Assert.IsTrue(expected.Any(face => face.Cell.X == 0) && expected.Any(face => face.Cell.Y == 0), "both front edges show the ground's thickness");
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(expected[index].Cell, controller.FaceTiles[index].Cell);
                Assert.AreEqual(expected[index].Level, controller.FaceTiles[index].Level);
                Assert.AreEqual(expected[index].Tile, controller.FaceTiles[index].Tile.name);
            }

            var revealed = PreviewRevealed(controller);
            foreach (var face in controller.FaceTiles)
            {
                var shown = faces.GetTile(face.Position, face.Level);
                Assert.AreEqual(revealed[face.Cell] ? face.Tile : null, shown, $"the face of {face.Cell} on level {face.Level} follows the fog like blocks do");
            }

            for (var level = faces.LowestLevel; level <= faces.HighestLevel; level++)
            {
                var tilemap = faces.At(level);
                Assert.AreEqual(SandboxTools.ActorSortingOrder, tilemap.GetComponent<TilemapRenderer>().sortingOrder);
                Assert.AreEqual(IsoMath.Lift(level) + SandboxTools.FaceSpriteDrop, tilemap.transform.localPosition.y, 1e-4f);
            }
        }

        [Test]
        public void Exits_ShowAnAnimatedArrowTowardTheirMapEdgeOnlyOnRevealedCells()
        {
            var controller = Find<GridSandboxController>();
            var map = MapLayout.Load(SandboxTools.MapPath);
            var markers = Layer(SandboxTools.ExitsTilemapName);
            var revealed = PreviewRevealed(controller);

            Assert.AreSame(markers, controller.ExitsTilemap);
            Assert.AreEqual(map.Exits.Count, controller.Exits.Count);
            Assert.Greater(markers.At(0).GetComponent<TilemapRenderer>().sortingOrder, Layer(SandboxTools.FloorTilemapName).At(0).GetComponent<TilemapRenderer>().sortingOrder, "the arrow lies on the floor");
            Assert.Less(markers.At(0).GetComponent<TilemapRenderer>().sortingOrder, Layer(SandboxTools.RangeTilemapName).At(0).GetComponent<TilemapRenderer>().sortingOrder, "and under the range and the cursor");
            foreach (var exit in controller.Exits)
            {
                var source = map.Exits.Single(candidate => candidate.X == exit.Cell.X && candidate.Y == exit.Cell.Y);
                Assert.AreEqual(map.HeadingOf(source), exit.Heading);
                Assert.AreEqual(SandboxTools.ExitMarkerFor(exit.Heading), exit.Marker.name);
                Assert.IsInstanceOf<AnimatedTile>(exit.Marker, "the arrow pulses toward the edge");
                Assert.IsFalse(string.IsNullOrWhiteSpace(exit.Label), "hovering an exit names the place it leads to");
                Assert.AreEqual(revealed[exit.Cell] ? exit.Marker : null, markers.GetTile(new Vector3Int(exit.Cell.X, exit.Cell.Y, 0)));
                Assert.AreEqual(SandboxTools.ActOf(exit.Location), exit.RequiredAct, $"the exit to {exit.Location} waits for the act of the map it leads to");
                Assert.AreEqual(source.Conditions.Count, exit.Conditions.Count, $"the exit to {exit.Location} keeps its story conditions");
            }

            Assert.IsTrue(controller.Exits.Any(exit => exit.RequiredAct == Act.Two), "the road to the Academy waits for the second act");
            Assert.IsTrue(controller.Exits.Where(exit => exit.IsGated).All(exit => markers.GetTile(new Vector3Int(exit.Cell.X, exit.Cell.Y, 0)) == null), "a closed exit shows no arrow");

            var label = Find<ExitLabelView>();
            Assert.IsFalse(label.IsShown, "the label waits for the cursor");
            Assert.IsFalse(label.Panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(graphic => graphic.raycastTarget), "the label never swallows a click on the map");
        }

        [Test]
        public void Water_PlaysOneAnimationInStepOnEveryWaterCell()
        {
            var floor = Layer(SandboxTools.FloorTilemapName);
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
            var blocks = Layer(SandboxTools.BlocksTilemapName);
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
                    if (source)
                    {
                        Assert.AreEqual(revealed[new Coord(x, y)] ? source : null, blocks.At(map.LevelAt(x, y)).GetTile(new Vector3Int(x, y, 0)), $"the block at ({x}, {y}) stands on level {map.LevelAt(x, y)}");
                    }
                }
            }

            Assert.Less(blocks.CountTiles(), controller.BlockTiles.Count(tile => tile), "the back of the agora starts under the fog, so some blocks start hidden");
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
            var layers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelTilemaps>()).ToList();

            CollectionAssert.AreEquivalent(
                new[] { SandboxTools.FloorTilemapName, SandboxTools.FacesTilemapName, SandboxTools.ExitsTilemapName, SandboxTools.RangeTilemapName, SandboxTools.HighlightTilemapName, SandboxTools.BlocksTilemapName, SandboxTools.FogTilemapName, SandboxTools.ArenaVeilTilemapName, SandboxTools.BlocksVeilTilemapName, SandboxTools.FacesVeilTilemapName },
                layers.Select(layer => layer.name));
            Assert.AreEqual(layers.Sum(layer => layer.Tilemaps.Count), renderers.Count, "every tilemap belongs to one level of one layer");
            Assert.IsTrue(renderers.All(renderer => renderer.mode == TilemapRenderer.Mode.Individual));
        }

        [Test]
        public void ArenaVeil_DarkensFloorAboveTheFloorAndBlocksRightAfterEachBlock()
        {
            var director = Find<SandboxBattleDirector>();
            var veil = director.Veil;
            var floor = Layer(SandboxTools.FloorTilemapName).At(0).GetComponent<TilemapRenderer>();
            var range = Layer(SandboxTools.RangeTilemapName).At(0).GetComponent<TilemapRenderer>();
            var blocks = Layer(SandboxTools.BlocksTilemapName);

            Assert.IsNotNull(veil, "the battle director must darken the world outside the arena");
            Assert.AreSame(Layer(SandboxTools.ArenaVeilTilemapName), veil.FloorVeil);
            Assert.AreSame(Layer(SandboxTools.BlocksVeilTilemapName), veil.BlocksVeil);
            Assert.AreSame(Layer(SandboxTools.FacesVeilTilemapName), veil.FacesVeil);
            Assert.AreEqual(0, veil.FloorVeil.CountTiles(), "the veil only exists during a battle");
            Assert.AreEqual(0, veil.BlocksVeil.CountTiles());
            Assert.AreEqual(0, veil.FacesVeil.CountTiles());

            var floorVeil = veil.FloorVeil.At(0).GetComponent<TilemapRenderer>();
            Assert.AreEqual(floor.sortingLayerID, floorVeil.sortingLayerID);
            Assert.Greater(floorVeil.sortingOrder, floor.sortingOrder, "the veil covers the floor");
            Assert.Less(floorVeil.sortingOrder, range.sortingOrder, "range highlights stay readable over the veil");

            for (var level = 0; level <= blocks.HighestLevel; level++)
            {
                var blockTilemap = blocks.At(level);
                var veilTilemap = veil.BlocksVeil.At(level);
                var blockLift = blockTilemap.transform.localPosition.z * RenderSetupTools.TransparencySortAxis.z;
                var veilLift = veilTilemap.transform.localPosition.z * RenderSetupTools.TransparencySortAxis.z;
                Assert.AreEqual(blockTilemap.transform.localPosition.y, veilTilemap.transform.localPosition.y, 1e-4f, "the veil is drawn over its block");
                Assert.AreEqual(blockTilemap.GetComponent<TilemapRenderer>().sortingOrder, veilTilemap.GetComponent<TilemapRenderer>().sortingOrder);
                Assert.Less(veilLift, blockLift, "each block veil is drawn right after its block");
                Assert.Greater(veilLift, blockLift - IsoMath.CellHeight, "and before the actor on the cell in front");
            }

            var faces = Layer(SandboxTools.FacesTilemapName);
            for (var level = faces.LowestLevel; level <= faces.HighestLevel; level++)
            {
                Assert.AreEqual(faces.At(level).transform.localPosition.y, veil.FacesVeil.At(level).transform.localPosition.y, 1e-4f);
                Assert.Greater(faces.At(level).transform.localPosition.z * RenderSetupTools.TransparencySortAxis.z, veil.FacesVeil.At(level).transform.localPosition.z * RenderSetupTools.TransparencySortAxis.z, "each face veil is drawn right after its face");
            }

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
            var blocks = Layer(SandboxTools.BlocksTilemapName).At(0);
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
            var fogRenderer = Layer(SandboxTools.FogTilemapName).At(0).GetComponent<TilemapRenderer>();
            var director = Find<SandboxBattleDirector>();

            Assert.AreEqual(Layer(SandboxTools.FloorTilemapName).At(0).GetComponent<TilemapRenderer>().sortingLayerID, fogRenderer.sortingLayerID, "fog, floor, blocks and actors share one sorting layer");
            foreach (var overlay in new[] { SandboxTools.FloorTilemapName, SandboxTools.ExitsTilemapName, SandboxTools.RangeTilemapName, SandboxTools.HighlightTilemapName, SandboxTools.ArenaVeilTilemapName })
            {
                Assert.Greater(fogRenderer.sortingOrder, Layer(overlay).At(0).GetComponent<TilemapRenderer>().sortingOrder, $"the fog covers {overlay}");
            }

            Assert.Less(fogRenderer.sortingOrder, Layer(SandboxTools.BlocksTilemapName).At(0).GetComponent<TilemapRenderer>().sortingOrder, "a revealed block is never cut by the fog behind it");
            Assert.Less(fogRenderer.sortingOrder, director.Veil.BlocksVeil.At(0).GetComponent<TilemapRenderer>().sortingOrder);
            Assert.IsTrue(Find<GridSandboxController>().Actors.All(actor => fogRenderer.sortingOrder < actor.SpriteRenderer.sortingOrder), "a revealed actor is never cut by the fog behind him");
            var fog = Layer(SandboxTools.FogTilemapName);
            var fogTiles = fog.UsedTileNames().Distinct().ToArray();
            Assert.Greater(fog.CountTiles(), 0);
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
            Assert.AreEqual(MapLayout.Load(SandboxTools.MapPath).FogRevealDepth, controller.FogRevealDepth,
                "the agora carries the fog depth its own json declares");
            var agora = MapLayout.Load(SandboxTools.MapPath);
            Assert.Less(controller.FogRevealDepth, agora.Width + agora.Height - 2,
                "the far corner of the map must start hidden");
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
                Assert.AreEqual(placed[index].SheetId, entry.Opponent.SheetId, $"{entry.Opponent.Id} wears the same sheet on the map and in the data");
                CollectionAssert.Contains(controller.Actors, entry.Actor);
                Assert.AreEqual(new Vector2Int(placed[index].X, placed[index].Y), entry.Actor.StartCell);
                Assert.AreEqual($"{entry.Opponent.SheetId}_{SandboxTools.PortraitRow}_0", entry.Portrait.name);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTools.SheetPathFor(entry.Opponent.SheetId)), entry.Actor.SpriteRenderer.sprite.texture);
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

            foreach (var gate in controller.Exits.Where(exit => exit.IsGated))
            {
                revealed[gate.Cell] = false;
            }

            return revealed;
        }

        private T Find<T>() where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).First();

        private LevelTilemaps Layer(string name) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelTilemaps>(true)).First(layer => layer.name == name);
    }
}
