using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.Grid;
using Game.UI;
using TMPro;
using TileData = Game.Grid.TileData;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class SandboxTools
    {
        public const string ScenePath = "Assets/_Game/Scenes/GridSandbox.unity";
        public const string MapPath = "Assets/_Game/Data/Maps/agora.json";
        public const string AgoraLocationId = "agora";
        public const string KerameikosLocationId = "kerameikos";
        public const string KerameikosMapPath = "Assets/_Game/Data/Maps/kerameikos.json";
        public const string KerameikosScenePath = "Assets/_Game/Scenes/Kerameikos.unity";
        public const string KerameikosCapturePath = "artifacts/reference/kerameikos.png";
        public const string ArchiveLocationId = "archive";
        public const string ArchiveMapPath = "Assets/_Game/Data/Maps/archive.json";
        public const string ArchiveScenePath = "Assets/_Game/Scenes/Archive.unity";
        public const string ArchiveCapturePath = "artifacts/reference/archive.png";
        public const string AcademyLocationId = "academy";
        public const string AcademyMapPath = "Assets/_Game/Data/Maps/academy.json";
        public const string AcademyScenePath = "Assets/_Game/Scenes/Academy.unity";
        public const string AcademyCapturePath = "artifacts/reference/academy.png";
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        public const string TileAssetFolder = ArtTools.PlaceholderFolder + "/TileAssets";
        public const string AnimatedTileAssetFolder = TileAssetFolder + "/Animated";
        public const float AnimatedTileFramesPerSecond = 6f;
        public const string ControlsPath = "Assets/_Game/Settings/GridControls.inputactions";
        public const string NetworkConfigPath = "Assets/_Game/Data/NetworkConfig.asset";
        public const string AudienceCharacter = "citizen";
        public const string PortraitRow = "se";
        public const string CapturePath = "artifacts/reference/grid_sandbox.png";
        public const string FloorTilemapName = "Floor";
        public const string HighlightTilemapName = "Highlight";
        public const string RangeTilemapName = "Range";
        public const string BlocksTilemapName = "Blocks";
        public const string FogTilemapName = "Fog";
        public const string ArenaVeilTilemapName = "ArenaVeil";
        public const string BlocksVeilTilemapName = "BlocksVeil";
        public const string FacesTilemapName = "Faces";
        public const string FacesVeilTilemapName = "FacesVeil";
        public const string ExitsTilemapName = "Exits";
        public const string ExitMarkerPrefix = "overlay_exit_";
        public const string ExitLabelObjectName = "ExitLabel";
        public const string ArenaVeilShaderPath = "Assets/_Game/Shaders/ArenaVeil.shader";
        public const string CrowdObjectName = "BattleCrowd";
        public const string SpectatorTemplateName = "SpectatorTemplate";
        public const string ArenaVeilTileName = "fog";
        public const int FloorSortingOrder = -20;
        public const int ExitSortingOrder = -19;
        public const int ArenaVeilSortingOrder = -18;
        public const int RangeSortingOrder = -15;
        public const int HighlightSortingOrder = -10;
        public const int FogSortingOrder = -5;
        public const int ActorSortingOrder = 0;
        public const int FogRevealDepth = 18;
        public const string ShadowName = "Shadow";
        public const string ShadowSpritePath = ArtTools.TileFolder + "/overlays/overlay_shadow.png";
        public const byte BlockedCost = TileData.ImpassableCost;
        public const byte StoneCost = 2;
        public const float BlockSortLift = -TerrainSorting.BlockOffset;
        public const int BaseDepthLevels = 2;
        public const float FaceSpriteDrop = -(SideFaceTiles.CanvasSize - TileHeightPixels) / (float)PixelArtImporter.PixelsPerUnit;
        public const int TileHeightPixels = 16;
        public const int ExitLabelSortingOrder = 1;
        public const int ExitLabelFontSize = 6;
        public const float CaptureMarginBelow = 0.25f;
        public const float CaptureMarginAbove = 2f;
        public const float ExplorationOrthographicSize = 7f;
        public const int CapturePixelsPerUnit = 48;
        public const int CaptureMarginX = 64;

        public static readonly Vector3 CellSize = new(2f, 1f, 1f);
        public static readonly Vector3 TileAnchor = new(-0.5f, 0.5f, 0f);
        public static readonly Vector3 GridPosition = new(0f, -IsoMath.CellHeight, 0f);
        public static readonly Vector3 CameraDamping = new(0.2f, 0.2f, 0f);

        public static readonly string[] FloorTileNames = { "floor_marble", "floor_marble_ochre", "floor_dirt", "floor_grass", "floor_stone", "floor_water", "floor_mosaic", "floor_packed_dirt", "floor_dry_clay", "floor_straw" };
        public static readonly string[] BlockTileNames = { "wall_marble", "wall_brick", "block_doric_column", "block_step", "prop_market_stall", "prop_statue_base", "prop_olive_trunk", "block_kiln", "prop_potters_wheel", "prop_amphora_stack" };

        public static readonly (string LocationId, string MapPath, string ScenePath)[] Maps =
        {
            (AgoraLocationId, MapPath, ScenePath),
            (KerameikosLocationId, KerameikosMapPath, KerameikosScenePath),
            (ArchiveLocationId, ArchiveMapPath, ArchiveScenePath),
            (AcademyLocationId, AcademyMapPath, AcademyScenePath)
        };
        public static readonly string[] FogEdgeNames = { "fog_edge_ne", "fog_edge_nw", "fog_edge_se", "fog_edge_sw" };

        private static readonly Dictionary<string, string> FloorSide = new()
        {
            ["floor_marble"] = SideFaceTiles.Stone,
            ["floor_marble_ochre"] = SideFaceTiles.Stone,
            ["floor_mosaic"] = SideFaceTiles.Stone,
            ["floor_stone"] = SideFaceTiles.Stone,
            ["floor_water"] = SideFaceTiles.Stone,
            ["floor_dirt"] = SideFaceTiles.Earth,
            ["floor_grass"] = SideFaceTiles.Earth,
            ["floor_packed_dirt"] = SideFaceTiles.Earth,
            ["floor_dry_clay"] = SideFaceTiles.Earth,
            ["floor_straw"] = SideFaceTiles.Earth
        };

        private static readonly Dictionary<string, TerrainId> FloorTerrain = new()
        {
            ["floor_marble"] = TerrainId.Marble,
            ["floor_marble_ochre"] = TerrainId.OchreMarble,
            ["floor_dirt"] = TerrainId.Dirt,
            ["floor_grass"] = TerrainId.Grass,
            ["floor_stone"] = TerrainId.Stone,
            ["floor_water"] = TerrainId.Water,
            ["floor_mosaic"] = TerrainId.Diagram,
            ["floor_packed_dirt"] = TerrainId.Dirt,
            ["floor_dry_clay"] = TerrainId.Dirt,
            ["floor_straw"] = TerrainId.Dirt
        };

        public static void BuildSandbox() => BatchRunner.Run(BuildAll);

        public static void CaptureReference() => BatchRunner.Run(CaptureReferenceImages);

        private static void CaptureReferenceImages()
        {
            CaptureReferenceImage(ScenePath, CapturePath, MapLayout.Load(MapPath));
            CaptureReferenceImage(KerameikosScenePath, KerameikosCapturePath, MapLayout.Load(KerameikosMapPath));
            CaptureReferenceImage(ArchiveScenePath, ArchiveCapturePath, MapLayout.Load(ArchiveMapPath));
            CaptureReferenceImage(AcademyScenePath, AcademyCapturePath, MapLayout.Load(AcademyMapPath));
        }

        public static Act ActOf(string locationId)
        {
            var entry = Maps.FirstOrDefault(map => map.LocationId == locationId);
            return entry.MapPath == null ? ActGate.FirstAct : MapLayout.Load(entry.MapPath).Act;
        }


        public static string SideMaterialFor(string floorName) => FloorSide[floorName];

        public static string ExitMarkerFor(Facing heading) => ExitMarkerPrefix + heading switch
        {
            Facing.NorthEast => "ne",
            Facing.NorthWest => "nw",
            Facing.SouthWest => "sw",
            _ => "se"
        };

        public static void BuildAll()
        {
            GenerateFogEdgeSources();
            SideFaceTiles.GenerateSources();
            ArtTools.RegenerateAll();
            RenderSetupTools.CreateCameraRigPrefab();
            EnsureTileAssets();
            AssetDatabase.SaveAssets();
            foreach (var map in Maps)
            {
                BuildScene(map.LocationId, map.MapPath, map.ScenePath);
                Debug.Log($"[SandboxTools] built {map.ScenePath} from {map.MapPath}");
            }

            SystemMenuTools.ApplyBuildScenes();
            AssetDatabase.SaveAssets();
        }

        private static void GenerateFogEdgeSources()
        {
            var overlayFolder = Path.Combine(ArtTools.SourceFolder, "tiles", "overlays");
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(Path.Combine(overlayFolder, "fog.png")));
            var pixels = source.GetPixels32();

            for (var edge = 0; edge < FogEdgeNames.Length; edge++)
            {
                var dithered = new Color32[pixels.Length];
                for (var y = 0; y < source.height; y++)
                {
                    for (var x = 0; x < source.width; x++)
                    {
                        var index = y * source.width + x;
                        var fromTop = source.height - 1 - y;
                        var inEdgeHalf = IsInEdgeHalf(edge, x, fromTop, source.width, source.height);
                        dithered[index] = inEdgeHalf && (x + fromTop) % 2 == 0 ? new Color32(0, 0, 0, 0) : pixels[index];
                    }
                }

                var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                texture.SetPixels32(dithered);
                texture.Apply();
                var bytes = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);

                var destination = Path.Combine(overlayFolder, FogEdgeNames[edge] + ".png");
                if (!File.Exists(destination) || !File.ReadAllBytes(destination).SequenceEqual(bytes))
                {
                    File.WriteAllBytes(destination, bytes);
                }
            }

            UnityEngine.Object.DestroyImmediate(source);
        }

        private static bool IsInEdgeHalf(int edge, int x, int fromTop, int width, int height)
        {
            var right = x >= width / 2;
            var top = fromTop < height / 2;
            return edge switch
            {
                0 => right && top,
                1 => !right && top,
                2 => right && !top,
                _ => !right && !top
            };
        }

        private static Dictionary<string, TileBase> EnsureTileAssets()
        {
            AssetFolders.Ensure(TileAssetFolder);
            var tiles = new Dictionary<string, TileBase>();
            var sprites = new Dictionary<string, Sprite>();
            foreach (var path in Directory.GetFiles(ArtTools.TileFolder, "*.png", SearchOption.AllDirectories).Select(file => file.Replace('\\', '/')))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException($"missing sprite {path}");
                var tile = AssetFolders.LoadOrCreate<Tile>($"{TileAssetFolder}/{name}.asset");
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tiles[name] = tile;
                sprites[name] = sprite;
            }

            foreach (var name in sprites.Keys.ToArray())
            {
                var frames = AnimationFrames(name, sprites);
                if (frames.Length > 1)
                {
                    tiles[name] = EnsureAnimatedTile(name, frames);
                }
            }

            return tiles;
        }

        public static Sprite[] AnimationFrames(string name, IReadOnlyDictionary<string, Sprite> sprites)
        {
            var frames = new List<Sprite> { sprites[name] };
            for (var frame = 1; sprites.TryGetValue($"{name}_{frame}", out var sprite); frame++)
            {
                frames.Add(sprite);
            }

            return frames.ToArray();
        }

        private static AnimatedTile EnsureAnimatedTile(string name, Sprite[] frames)
        {
            AssetFolders.Ensure(AnimatedTileAssetFolder);
            var tile = AssetFolders.LoadOrCreate<AnimatedTile>($"{AnimatedTileAssetFolder}/{name}.asset");
            tile.m_AnimatedSprites = frames;
            tile.m_MinSpeed = AnimatedTileFramesPerSecond;
            tile.m_MaxSpeed = AnimatedTileFramesPerSecond;
            tile.m_AnimationStartTime = 0f;
            tile.m_TileColliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static void BuildScene(string locationId, string mapPath, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var tiles = EnsureTileAssets();
            var map = MapLayout.Load(mapPath);
            var width = map.Width;
            var height = map.Height;
            var levels = LevelsOf(map);
            var highest = map.MaxLevel;

            var gridObject = new GameObject("Grid");
            gridObject.transform.position = GridPosition;
            var unityGrid = gridObject.AddComponent<UnityEngine.Grid>();
            unityGrid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            unityGrid.cellSize = CellSize;

            var floor = CreateLayer(gridObject, FloorTilemapName, levels, width, 0, highest, FloorSortingOrder, TerrainSorting.FloorOffset);
            var faces = CreateLayer(gridObject, FacesTilemapName, levels, width, 1 - BaseDepthLevels, highest, null, TerrainSorting.FaceOffset, FaceSpriteDrop);
            var exitMarkers = CreateLayer(gridObject, ExitsTilemapName, levels, width, 0, highest, ExitSortingOrder, TerrainSorting.ExitOffset);
            var range = CreateLayer(gridObject, RangeTilemapName, levels, width, 0, highest, RangeSortingOrder, TerrainSorting.RangeOffset);
            var highlight = CreateLayer(gridObject, HighlightTilemapName, levels, width, 0, highest, HighlightSortingOrder, TerrainSorting.HighlightOffset);
            var blocks = CreateLayer(gridObject, BlocksTilemapName, levels, width, 0, highest, null, TerrainSorting.BlockOffset);
            var fog = CreateLayer(gridObject, FogTilemapName, levels, width, 0, highest, FogSortingOrder, TerrainSorting.FogOffset);

            var terrain = new TerrainId[width * height];
            var costs = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var floorName = map.FloorAt(x, y);
                    floor.SetTile(new Vector3Int(x, y, 0), tiles[floorName]);
                    terrain[y * width + x] = FloorTerrain[floorName];
                    costs[y * width + x] = FloorCost(floorName);
                }
            }

            var blockTiles = new TileBase[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var blockName = map.BlockAt(x, y);
                    if (blockName == null)
                    {
                        continue;
                    }

                    blockTiles[y * width + x] = tiles[blockName];
                    costs[y * width + x] = BlockedCost;
                }
            }

            var sandbox = new GameObject("GridSandbox");
            var gizmos = sandbox.AddComponent<GridGizmoView>();
            var controller = sandbox.AddComponent<GridSandboxController>();
            var actors = CreateActors(map);

            controller.ConfigureLayout(width, height, terrain, costs, map.FogRevealDepth ?? FogRevealDepth, levels);
            controller.ConfigureLocation(locationId);
            controller.ConfigureExits(map.Exits
                .Select(exit => new MapExitLink(
                    new Coord(exit.X, exit.Y),
                    exit.Location,
                    new Coord(exit.ArrivalX, exit.ArrivalY),
                    exit.Facing,
                    map.HeadingOf(exit),
                    exit.Label,
                    tiles[ExitMarkerFor(map.HeadingOf(exit))])
                    .Gated(ActOf(exit.Location), exit.StoryConditions))
                .ToArray());
            controller.ConfigurePresences(map.Actors
                .Select((actor, index) => (actor, index))
                .Where(pair => pair.actor.IsConditional)
                .Select(pair => new ActorPresenceLink(
                    actors[pair.index],
                    pair.actor.StoryConditions,
                    pair.actor.WalksIn ? new Coord(pair.actor.Enters.X, pair.actor.Enters.Y) : null))
                .ToList());
            controller.ConfigureBlockPresences(map.ConditionalBlocks
                .Select(block => new BlockPresenceLink(new Coord(block.X, block.Y), FloorCost(map.FloorAt(block.X, block.Y)), block.StoryConditions))
                .ToList());
            controller.ConfigureHome(map.Home);
            controller.ConfigureViews(
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath),
                highlight,
                fog,
                new TileBase[] { tiles["hl_select"], null },
                tiles["fog"],
                FogEdgeNames.Select(name => (TileBase)tiles[name]).ToArray(),
                actors,
                gizmos);
            controller.ConfigureBlocks(blocks, blockTiles);
            controller.ConfigureFaces(faces, FacesOf(map, tiles).ToArray());
            controller.ConfigureExitMarkers(exitMarkers);
            controller.PaintFogPreview();

            var explorationCamera = CreateCamera(actors[0].transform, width, height, highest);
            CreateGameServices();
            SystemMenuTools.CreateEventSystem();
            SystemMenuTools.InstantiatePauseMenu();
            CreateExitLabel(controller);
            var dialogues = CreateDialogueDirector(controller, map, actors);
            CreateCueDirector(controller, dialogues, map);
            CutsceneTools.CreateDirector(controller, dialogues, map, actors);
            var opponents = MapOpponentsFor(map.Actors, actors);
            if (opponents.Count > 0)
            {
                var rangeView = range.gameObject.AddComponent<RangeHighlightView>();
                rangeView.Configure(range, tiles["hl_move"], tiles["hl_refute"], tiles["hl_maieutics"]);
                var battleDirector = CreateBattleDirector(controller, map.Actors, actors, opponents, explorationCamera);
                battleDirector.ConfigureRange(rangeView);
                battleDirector.ConfigureStory(dialogues);
                battleDirector.ConfigureArena(CreateCrowd(), CreateArenaVeil(gridObject, levels, width, highest, blocks, faces, tiles[ArenaVeilTileName]));
            }

            AssetFolders.Ensure(Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException($"could not save {scenePath}");
            }
        }

        public static byte FloorCost(string floorName) => floorName switch
        {
            "floor_water" => BlockedCost,
            "floor_stone" => StoneCost,
            _ => (byte)PathFinder.MinimumStepCost
        };

        public static byte[] LevelsOf(MapLayout map)
        {
            var levels = new byte[map.Width * map.Height];
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    levels[y * map.Width + x] = (byte)map.LevelAt(x, y);
                }
            }

            return levels;
        }

        public static IEnumerable<(Coord Cell, int Level, string Tile)> FaceSegmentsOf(MapLayout map)
        {
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var level = map.LevelAt(x, y);
                    var southWest = x == 0 ? -BaseDepthLevels : map.LevelAt(x - 1, y);
                    var southEast = y == 0 ? -BaseDepthLevels : map.LevelAt(x, y - 1);
                    var material = SideMaterialFor(map.FloorAt(x, y));
                    for (var face = level; face > Math.Min(southWest, southEast); face--)
                    {
                        var band = face == level ? SideFaceTiles.LipBand : SideFaceTiles.BodyBand;
                        yield return (new Coord(x, y), face, SideFaceTiles.TileName(material, band, southWest < face, southEast < face));
                    }
                }
            }
        }

        private static IEnumerable<FaceTile> FacesOf(MapLayout map, IReadOnlyDictionary<string, TileBase> tiles) =>
            FaceSegmentsOf(map).Select(face => new FaceTile(face.Cell, face.Level, tiles[face.Tile]));

        private static LevelTilemaps CreateLayer(GameObject gridObject, string name, byte[] levels, int width, int lowest, int highest, int? groundSortingOrder, float sortOffset, float drop = 0f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(gridObject.transform, false);
            var tilemaps = new Tilemap[highest - lowest + 1];
            for (var level = lowest; level <= highest; level++)
            {
                var sortsWithActors = !groundSortingOrder.HasValue || level > TileData.GroundLevel;
                var tilemap = CreateTilemap(root.transform, $"{name}_{level}", SortingLayer.IDToName(0), sortsWithActors ? ActorSortingOrder : groundSortingOrder.Value);
                tilemap.transform.localPosition = sortsWithActors
                    ? new Vector3(0f, TerrainSorting.TilemapLift(level, drop), TerrainSorting.TilemapDepth(level, sortOffset, drop))
                    : Vector3.zero;
                tilemaps[level - lowest] = tilemap;
            }

            var layer = root.AddComponent<LevelTilemaps>();
            layer.Configure(width, levels, lowest, tilemaps);
            return layer;
        }

        private static void CreateExitLabel(GridSandboxController controller)
        {
            var root = new GameObject(ExitLabelObjectName, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = ExitLabelSortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RenderSetupTools.ReferenceResolution, RenderSetupTools.ReferenceResolution);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = PixelArtImporter.PixelsPerUnit;

            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root.transform, false);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0f);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(24, 24, 40, 255);
            background.raycastTarget = false;

            var text = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            text.SetParent(panel, false);
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = Vector2.zero;
            text.offsetMax = Vector2.zero;
            var label = text.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = ExitLabelFontSize;
            label.color = new Color32(248, 216, 48, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            root.AddComponent<ExitLabelView>().Configure(controller, (RectTransform)root.transform, panel, label);
        }

        internal static GameServices CreateGameServices()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<Game.Content.FallacyCatalogSO>(DataTools.CatalogPath)
                ?? throw new InvalidOperationException($"missing {DataTools.CatalogPath}; run DataTools.Regenerate first");
            var fallback = AssetDatabase.LoadAssetAtPath<Game.Content.FallbackLibrarySO>(DataTools.FallbackLibraryPath)
                ?? throw new InvalidOperationException($"missing {DataTools.FallbackLibraryPath}; run DataTools.Regenerate first");

            var network = AssetDatabase.LoadAssetAtPath<Game.Content.NetworkConfigSO>(NetworkConfigPath);
            if (network == null)
            {
                network = ScriptableObject.CreateInstance<Game.Content.NetworkConfigSO>();
                AssetDatabase.CreateAsset(network, NetworkConfigPath);
                AssetDatabase.SaveAssets();
            }

            var services = new GameObject(nameof(GameServices)).AddComponent<GameServices>();
            services.Configure(network, catalog, fallback, null);
            services.ConfigureAudio(AssetDatabase.LoadAssetAtPath<AudioLibrarySO>(DataTools.AudioLibraryPath));
            return services;
        }

        private static Tilemap CreateTilemap(Transform parent, string name, string sortingLayer, int sortingOrder)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemap.tileAnchor = TileAnchor;
            var renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.mode = TilemapRenderer.Mode.Individual;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static List<ActorView> CreateActors(MapLayout map)
        {
            var root = new GameObject("Actors");
            return map.Actors
                .Select(actor => CreateActorView(root.transform, $"{actor.Character}_{actor.X}_{actor.Y}", actor.SheetId, new Vector2Int(actor.X, actor.Y), actor.Facing, map.LevelAt(actor.X, actor.Y)))
                .ToList();
        }

        private static ActorView CreateActorView(Transform parent, string name, string character, Vector2Int cell, Facing facing, int level = TileData.GroundLevel)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(ArtTools.SheetPathFor(character)).OfType<Sprite>().ToDictionary(sprite => sprite.name);
            var row = FacingRules.SheetFacing(facing) == Facing.NorthEast ? "ne" : "se";

            var actorObject = new GameObject(name);
            actorObject.transform.SetParent(parent, false);

            var renderer = actorObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[$"{character}_{row}_0"];
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.sortingOrder = ActorSortingOrder;

            var animator = actorObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArtTools.ControllerPathFor(character));

            var shadowObject = new GameObject(ShadowName);
            shadowObject.transform.SetParent(actorObject.transform, false);
            var shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSpritePath);
            shadow.spriteSortPoint = SpriteSortPoint.Pivot;
            shadow.sortingOrder = ActorSortingOrder;

            var view = actorObject.AddComponent<ActorView>();
            view.Configure(cell, facing, renderer, animator, level, shadow);
            return view;
        }

        private static BattleCrowd CreateCrowd()
        {
            var crowd = new GameObject(CrowdObjectName).AddComponent<BattleCrowd>();
            var template = CreateActorView(crowd.transform, SpectatorTemplateName, AudienceCharacter, Vector2Int.zero, Facing.SouthEast);
            template.gameObject.SetActive(false);
            crowd.Configure(template);
            return crowd;
        }

        private static ArenaVeilView CreateArenaVeil(GameObject gridObject, byte[] levels, int width, int highest, LevelTilemaps blocks, LevelTilemaps faces, TileBase veilTile)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ArenaVeilShaderPath)
                ?? throw new InvalidOperationException($"missing {ArenaVeilShaderPath}");
            var floorVeil = CreateLayer(gridObject, ArenaVeilTilemapName, levels, width, 0, highest, ArenaVeilSortingOrder, TerrainSorting.FloorVeilOffset);
            var blocksVeil = CreateLayer(gridObject, BlocksVeilTilemapName, levels, width, 0, highest, null, TerrainSorting.BlockVeilOffset);
            var facesVeil = CreateLayer(gridObject, FacesVeilTilemapName, levels, width, faces.LowestLevel, faces.HighestLevel, null, TerrainSorting.FaceVeilOffset, FaceSpriteDrop);

            var veil = floorVeil.gameObject.AddComponent<ArenaVeilView>();
            veil.Configure(floorVeil, blocksVeil, blocks, veilTile, shader);
            veil.ConfigureFaces(facesVeil, faces);
            return veil;
        }

        private static List<MapOpponent> MapOpponentsFor(IReadOnlyList<MapActor> placements, IReadOnlyList<ActorView> actors)
        {
            var opponents = DataTools.OpponentIds().ToDictionary(
                id => id,
                id => AssetDatabase.LoadAssetAtPath<Game.Content.OpponentSO>(DataTools.OpponentPath(id))
                    ?? throw new InvalidOperationException($"missing opponent {id}; run DataTools.Regenerate first"));

            return placements
                .Select((actor, index) => (actor, index))
                .Skip(1)
                .Where(pair => opponents.ContainsKey(pair.actor.Character))
                .Select(pair => new MapOpponent(actors[pair.index], opponents[pair.actor.Character], PortraitFor(pair.actor.SheetId)))
                .ToList();
        }

        private static SandboxBattleDirector CreateBattleDirector(GridSandboxController controller, List<MapActor> placements, List<ActorView> actors, List<MapOpponent> opponents, ExplorationCamera explorationCamera)
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudTools.HudPrefabPath)
                ?? throw new InvalidOperationException($"missing {BattleHudTools.HudPrefabPath}; run BattleHudTools.BuildHud first");

            var hud = ((GameObject)PrefabUtility.InstantiatePrefab(hudPrefab)).GetComponent<BattleHud>();
            hud.gameObject.SetActive(false);

            var audience = placements
                .Select((actor, index) => (actor, index))
                .Where(pair => pair.actor.Character == AudienceCharacter)
                .Select(pair => actors[pair.index])
                .ToList();

            var director = new GameObject(nameof(SandboxBattleDirector)).AddComponent<SandboxBattleDirector>();
            director.Configure(
                controller,
                opponents,
                audience,
                hud,
                explorationCamera,
                AssetDatabase.LoadAssetAtPath<Game.Content.UiTextSO>(DataTools.UiTextPath),
                AssetDatabase.LoadAssetAtPath<Game.Content.FallacyCatalogSO>(DataTools.CatalogPath),
                AssetDatabase.LoadAssetAtPath<Game.Content.FallbackLibrarySO>(DataTools.FallbackLibraryPath));
            director.ConfigurePlayerPortrait(PortraitFor(placements[0].SheetId));
            return director;
        }

        private static ExplorationDialogueDirector CreateDialogueDirector(GridSandboxController controller, MapLayout map, List<ActorView> actors)
        {
            var library = AssetDatabase.LoadAssetAtPath<Game.Content.DialogueLibrarySO>(DataTools.DialogueLibraryPath)
                ?? throw new InvalidOperationException($"missing {DataTools.DialogueLibraryPath}; run DataTools.Regenerate first");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudTools.ExplorationDialoguePrefabPath)
                ?? throw new InvalidOperationException($"missing {BattleHudTools.ExplorationDialoguePrefabPath}; run BattleHudTools.BuildHud first");

            var unknown = map.DialogueIds.FirstOrDefault(id => !library.Contains(id));
            if (unknown != null)
            {
                throw new InvalidOperationException($"map {map.Id} references the unknown dialogue {unknown}");
            }

            var box = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponentInChildren<DialogueBoxView>(true);
            var talkers = map.Actors
                .Select((actor, index) => (actor, index))
                .Where(pair => !string.IsNullOrEmpty(pair.actor.Dialogue))
                .Select(pair => new NpcDialogueLink(actors[pair.index], pair.actor.Dialogue))
                .ToList();
            var triggers = map.Triggers
                .Select(trigger => trigger.IsCutscene
                    ? DialogueTriggerLink.ForCutscene(new Coord(trigger.X, trigger.Y), trigger.Cutscene, trigger.Flag, trigger.StoryConditions)
                    : new DialogueTriggerLink(new Coord(trigger.X, trigger.Y), trigger.Dialogue, trigger.Flag, trigger.StoryConditions))
                .ToList();
            var portraits = library.Speakers
                .Where(pair => pair.Value.HasPortrait)
                .ToDictionary(pair => pair.Key, pair => PortraitFor(pair.Value.Portrait));

            var director = new GameObject(nameof(ExplorationDialogueDirector)).AddComponent<ExplorationDialogueDirector>();
            director.Configure(controller, box, library, talkers, triggers, portraits);
            return director;
        }

        private static void CreateCueDirector(GridSandboxController controller, ExplorationDialogueDirector dialogues, MapLayout map)
        {
            if (map.Cues.Count == 0)
            {
                return;
            }

            var cues = map.Cues
                .Select(cue => new StoryCueLink(cue.On, cue.Dialogue, cue.Flag, cue.StoryConditions))
                .ToList();
            new GameObject(nameof(StoryCueDirector)).AddComponent<StoryCueDirector>().Configure(controller, dialogues, cues);
        }

        public static Sprite PortraitFor(string character)
        {
            var dedicated = AssetDatabase.LoadAssetAtPath<Sprite>(ArtTools.PortraitPathFor(character));
            if (dedicated)
            {
                return dedicated;
            }

            var name = $"{character}_{PortraitRow}_0";
            return AssetDatabase.LoadAllAssetsAtPath(ArtTools.SheetPathFor(character)).OfType<Sprite>().FirstOrDefault(sprite => sprite.name == name)
                ?? throw new InvalidOperationException($"missing portrait sprite {name}; run ArtTools.SliceSheets first");
        }

        private static ExplorationCamera CreateCamera(Transform followTarget, int mapWidth, int mapHeight, int highestLevel)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RenderSetupTools.CameraRigPrefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.position = new Vector3(followTarget.position.x, followTarget.position.y, RenderSetupTools.CameraDepth);
            var brain = rig.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, ExplorationCamera.DefaultGlideSeconds);
            rig.AddComponent<PixelSnappedCamera>();

            var boundsObject = new GameObject("CameraBounds");
            var bounds = boundsObject.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true;
            bounds.points = MapOutline(mapWidth, mapHeight, highestLevel);

            var focus = new GameObject(ExplorationCamera.DuelFocusName).transform;
            focus.position = new Vector3(followTarget.position.x, followTarget.position.y, 0f);

            var virtualCamera = CreateVirtualCamera("ExplorationCamera", followTarget);
            virtualCamera.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            var confiner = virtualCamera.gameObject.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = bounds;

            var duelCamera = CreateVirtualCamera(ExplorationCamera.DuelCameraName, focus);
            var explorationCamera = virtualCamera.gameObject.AddComponent<ExplorationCamera>();
            explorationCamera.Configure(virtualCamera, duelCamera, ExplorationOrthographicSize, focus);
            return explorationCamera;
        }

        private static CinemachineCamera CreateVirtualCamera(string name, Transform followTarget)
        {
            var virtualCameraObject = new GameObject(name);
            var virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = ExplorationOrthographicSize;
            virtualCamera.Lens = lens;
            virtualCamera.Follow = followTarget;

            var composer = virtualCameraObject.AddComponent<CinemachinePositionComposer>();
            composer.Damping = CameraDamping;
            composer.CameraDistance = -RenderSetupTools.CameraDepth;

            virtualCameraObject.AddComponent<CinemachinePixelPerfect>();
            return virtualCamera;
        }

        public static Vector2[] MapOutline(int width, int height, int highestLevel)
        {
            var min = -0.5f;
            var maxX = width - 0.5f;
            var maxY = height - 0.5f;
            var below = new Vector2(0f, -IsoMath.Lift(BaseDepthLevels));
            var above = new Vector2(0f, IsoMath.Lift(highestLevel));
            return new[]
            {
                ToWorld(min, min) + below,
                ToWorld(maxX, min) + below,
                ToWorld(maxX, min) + above,
                ToWorld(maxX, maxY) + above,
                ToWorld(min, maxY) + above,
                ToWorld(min, maxY) + below
            };
        }

        private static Vector2 ToWorld(float gridX, float gridY) =>
            new((gridX - gridY) * IsoMath.CellWidth, (gridX + gridY) * IsoMath.CellHeight);

        private static void CaptureReferenceImage(string scenePath, string capturePath, MapLayout map)
        {
            var span = map.Width + map.Height;
            var bottom = -IsoMath.CellHeight - IsoMath.Lift(BaseDepthLevels) - CaptureMarginBelow;
            var top = (span - 1) * IsoMath.CellHeight + IsoMath.Lift(map.MaxLevel) + CaptureMarginAbove;
            var captureWidth = Mathf.RoundToInt(span * IsoMath.CellWidth * CapturePixelsPerUnit) + CaptureMarginX;
            var captureHeight = Mathf.RoundToInt((top - bottom) * CapturePixelsPerUnit);
            var center = new Vector2((map.Width - map.Height) * 0.5f * IsoMath.CellWidth, (top + bottom) * 0.5f);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>() ?? throw new InvalidOperationException("sandbox has no camera");

            if (camera.TryGetComponent<PixelPerfectCamera>(out var pixelPerfect))
            {
                pixelPerfect.enabled = false;
            }

            if (camera.TryGetComponent<CinemachineBrain>(out var brain))
            {
                brain.enabled = false;
            }

            camera.orthographic = true;
            camera.orthographicSize = captureHeight / (2f * CapturePixelsPerUnit);
            camera.transform.position = new Vector3(center.x, center.y, RenderSetupTools.CameraDepth);

            var renderTexture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
            var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                texture.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(capturePath) ?? ".");
                File.WriteAllBytes(capturePath, texture.EncodeToPNG());
                Debug.Log($"[SandboxTools] captured {capturePath}");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
