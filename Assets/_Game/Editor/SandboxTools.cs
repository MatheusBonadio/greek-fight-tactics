using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Grid;
using Game.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TileData = Game.Grid.TileData;

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
        public const string ArenaVeilShaderPath = "Assets/_Game/Shaders/ArenaVeil.shader";
        public const string CrowdObjectName = "BattleCrowd";
        public const string SpectatorTemplateName = "SpectatorTemplate";
        public const string ArenaVeilTileName = "fog";
        public const int FloorSortingOrder = -20;
        public const int ArenaVeilSortingOrder = -18;
        public const int RangeSortingOrder = -15;
        public const int HighlightSortingOrder = -10;
        public const int FogSortingOrder = -5;
        public const int ActorSortingOrder = 0;
        public const int FogRevealDepth = 18;
        public const byte BlockedCost = TileData.ImpassableCost;
        public const byte StoneCost = 2;
        public const float BlockSortLift = 0.25f;
        public const float BlockVeilSortNudge = 0.01f;
        public const float ExplorationOrthographicSize = 7f;
        public const int CapturePixelsPerUnit = 48;
        public const int CaptureMarginX = 64;
        public const int CaptureMarginY = 96;
        public const float CaptureLiftY = 1f;

        public static readonly Vector3 CellSize = new(2f, 1f, 1f);
        public static readonly Vector3 TileAnchor = new(-0.5f, 0.5f, 0f);
        public static readonly Vector3 GridPosition = new(0f, -IsoMath.CellHeight, 0f);
        public static readonly Vector3 CameraDamping = new(0.2f, 0.2f, 0f);

        public static readonly string[] FloorTileNames = { "floor_marble", "floor_marble_ochre", "floor_dirt", "floor_grass", "floor_stone", "floor_water", "floor_mosaic", "floor_packed_dirt", "floor_dry_clay", "floor_straw" };
        public static readonly string[] BlockTileNames = { "wall_marble", "wall_brick", "block_doric_column", "block_step", "prop_market_stall", "prop_statue_base", "prop_olive_trunk", "block_kiln", "prop_potters_wheel", "prop_amphora_stack" };

        public static readonly (string LocationId, string MapPath, string ScenePath)[] Maps =
        {
            (AgoraLocationId, MapPath, ScenePath),
            (KerameikosLocationId, KerameikosMapPath, KerameikosScenePath)
        };
        public static readonly string[] FogEdgeNames = { "fog_edge_ne", "fog_edge_nw", "fog_edge_se", "fog_edge_sw" };

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
        }


        public static void BuildAll()
        {
            GenerateFogEdgeSources();
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

            var gridObject = new GameObject("Grid");
            gridObject.transform.position = GridPosition;
            var unityGrid = gridObject.AddComponent<UnityEngine.Grid>();
            unityGrid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            unityGrid.cellSize = CellSize;

            var floor = CreateTilemap(gridObject, FloorTilemapName, SortingLayer.IDToName(0), FloorSortingOrder);
            var range = CreateTilemap(gridObject, RangeTilemapName, SortingLayer.IDToName(0), RangeSortingOrder);
            var highlight = CreateTilemap(gridObject, HighlightTilemapName, SortingLayer.IDToName(0), HighlightSortingOrder);
            var blocks = CreateTilemap(gridObject, BlocksTilemapName, SortingLayer.IDToName(0), ActorSortingOrder);
            var fog = CreateTilemap(gridObject, FogTilemapName, SortingLayer.IDToName(0), FogSortingOrder);

            var terrain = new TerrainId[width * height];
            var costs = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var floorName = map.FloorAt(x, y);
                    floor.SetTile(new Vector3Int(x, y, 0), tiles[floorName]);
                    terrain[y * width + x] = FloorTerrain[floorName];
                    costs[y * width + x] = floorName switch
                    {
                        "floor_water" => BlockedCost,
                        "floor_stone" => StoneCost,
                        _ => (byte)PathFinder.MinimumStepCost
                    };
                }
            }

            blocks.transform.localPosition = new Vector3(0f, 0f, BlockSortLift / RenderSetupTools.TransparencySortAxis.z);
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
            var actors = CreateActors(map.Actors);

            controller.ConfigureLayout(width, height, terrain, costs, map.FogRevealDepth ?? FogRevealDepth);
            controller.ConfigureLocation(locationId);
            controller.ConfigureExits(map.Exits
                .Select(exit => new MapExitLink(new Coord(exit.X, exit.Y), exit.Location, new Coord(exit.ArrivalX, exit.ArrivalY), exit.Facing))
                .ToArray());
            controller.ConfigurePresences(map.Actors
                .Select((actor, index) => (actor, index))
                .Where(pair => pair.actor.IsConditional)
                .Select(pair => new ActorPresenceLink(actors[pair.index], pair.actor.StoryConditions))
                .ToList());
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
            controller.PaintFogPreview();

            var explorationCamera = CreateCamera(actors[0].transform, width, height);
            CreateGameServices();
            SystemMenuTools.CreateEventSystem();
            SystemMenuTools.InstantiatePauseMenu();
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
                battleDirector.ConfigureArena(CreateCrowd(), CreateArenaVeil(gridObject, blocks, tiles[ArenaVeilTileName]));
            }

            AssetFolders.Ensure(Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException($"could not save {scenePath}");
            }
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
            return services;
        }

        private static Tilemap CreateTilemap(GameObject gridObject, string name, string sortingLayer, int sortingOrder)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(gridObject.transform, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemap.tileAnchor = TileAnchor;
            var renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.mode = TilemapRenderer.Mode.Individual;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static List<ActorView> CreateActors(IReadOnlyList<MapActor> placements)
        {
            var root = new GameObject("Actors");
            return placements
                .Select(actor => CreateActorView(root.transform, $"{actor.Character}_{actor.X}_{actor.Y}", actor.Character, new Vector2Int(actor.X, actor.Y), actor.Facing))
                .ToList();
        }

        private static ActorView CreateActorView(Transform parent, string name, string character, Vector2Int cell, Facing facing)
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

            var view = actorObject.AddComponent<ActorView>();
            view.Configure(cell, facing, renderer, animator);
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

        private static ArenaVeilView CreateArenaVeil(GameObject gridObject, Tilemap blocks, TileBase veilTile)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ArenaVeilShaderPath)
                ?? throw new InvalidOperationException($"missing {ArenaVeilShaderPath}");
            var floorVeil = CreateTilemap(gridObject, ArenaVeilTilemapName, SortingLayer.IDToName(0), ArenaVeilSortingOrder);
            var blocksVeil = CreateTilemap(gridObject, BlocksVeilTilemapName, SortingLayer.IDToName(0), ActorSortingOrder);
            blocksVeil.transform.localPosition = blocks.transform.localPosition + new Vector3(0f, 0f, BlockVeilSortNudge);

            var veil = floorVeil.gameObject.AddComponent<ArenaVeilView>();
            veil.Configure(floorVeil, blocksVeil, blocks, veilTile, shader);
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
                .Select(pair => new MapOpponent(actors[pair.index], opponents[pair.actor.Character], PortraitFor(pair.actor.Character)))
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
            director.ConfigurePlayerPortrait(PortraitFor(placements[0].Character));
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

        private static ExplorationCamera CreateCamera(Transform followTarget, int mapWidth, int mapHeight)
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
            bounds.points = MapOutline(mapWidth, mapHeight);

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

        private static Vector2[] MapOutline(int width, int height)
        {
            var min = -0.5f;
            var maxX = width - 0.5f;
            var maxY = height - 0.5f;
            return new[] { ToWorld(min, min), ToWorld(maxX, min), ToWorld(maxX, maxY), ToWorld(min, maxY) };
        }

        private static Vector2 ToWorld(float gridX, float gridY) =>
            new((gridX - gridY) * IsoMath.CellWidth, (gridX + gridY) * IsoMath.CellHeight);

        private static void CaptureReferenceImage(string scenePath, string capturePath, MapLayout map)
        {
            var span = map.Width + map.Height;
            var captureWidth = Mathf.RoundToInt(span * IsoMath.CellWidth * CapturePixelsPerUnit) + CaptureMarginX;
            var captureHeight = Mathf.RoundToInt(span * IsoMath.CellHeight * CapturePixelsPerUnit) + CaptureMarginY;
            var center = new Vector2((map.Width - map.Height) * 0.5f * IsoMath.CellWidth, (span - 2) * 0.5f * IsoMath.CellHeight + CaptureLiftY);
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
