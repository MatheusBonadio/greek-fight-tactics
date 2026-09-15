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
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        public const string TileAssetFolder = ArtTools.PlaceholderFolder + "/TileAssets";
        public const string ControlsPath = "Assets/_Game/Settings/GridControls.inputactions";
        public const string NetworkConfigPath = "Assets/_Game/Data/NetworkConfig.asset";
        public const string CapturePath = "artifacts/reference/grid_sandbox.png";
        public const string FogSortingLayer = "Fog";
        public const string FloorTilemapName = "Floor";
        public const string HighlightTilemapName = "Highlight";
        public const string BlocksTilemapName = "Blocks";
        public const string FogTilemapName = "Fog";
        public const int Size = 12;
        public const int FloorSortingOrder = -20;
        public const int HighlightSortingOrder = -10;
        public const int ActorSortingOrder = 0;
        public const int FogRevealDepth = 18;
        public const byte BlockedCost = TileData.ImpassableCost;
        public const byte StoneCost = 2;
        public const float ExplorationOrthographicSize = 7f;
        public const float BattleOrthographicSize = 5f;
        public const int CaptureWidth = 1216;
        public const int CaptureHeight = 672;
        public const int CapturePixelsPerUnit = 48;
        public const float CaptureCenterY = 6.5f;

        public static readonly Vector3 CellSize = new(2f, 1f, 1f);
        public static readonly Vector3 TileAnchor = new(-0.5f, 0.5f, 0f);
        public static readonly Vector3 GridPosition = new(0f, -IsoMath.CellHeight, 0f);
        public static readonly Vector3 CameraDamping = new(0.2f, 0.2f, 0f);

        public static readonly string[] FloorTileNames = { "floor_marble", "floor_marble_ochre", "floor_dirt", "floor_grass", "floor_stone", "floor_water", "floor_mosaic" };
        public static readonly string[] BlockTileNames = { "wall_marble", "wall_brick", "block_doric_column", "block_step", "prop_market_stall", "prop_statue_base", "prop_olive_trunk" };
        public static readonly string[] FogEdgeNames = { "fog_edge_ne", "fog_edge_nw", "fog_edge_se", "fog_edge_sw" };

        private static readonly Dictionary<string, TerrainId> FloorTerrain = new()
        {
            ["floor_marble"] = TerrainId.Marble,
            ["floor_marble_ochre"] = TerrainId.OchreMarble,
            ["floor_dirt"] = TerrainId.Dirt,
            ["floor_grass"] = TerrainId.Grass,
            ["floor_stone"] = TerrainId.Stone,
            ["floor_water"] = TerrainId.Water,
            ["floor_mosaic"] = TerrainId.Diagram
        };

        private static readonly (string Tile, int X, int Y)[] Blocks =
        {
            ("block_doric_column", 6, 6),
            ("prop_olive_trunk", 3, 8),
            ("wall_marble", 1, 9),
            ("wall_brick", 2, 9),
            ("block_step", 5, 2),
            ("prop_market_stall", 8, 4),
            ("prop_statue_base", 9, 6)
        };

        private static readonly (string Character, int X, int Y, Facing Facing)[] Actors =
        {
            ("kleia", 2, 2, Facing.SouthEast),
            ("eudoro", 7, 6, Facing.SouthWest),
            ("citizen", 5, 6, Facing.SouthEast),
            ("menexeno", 3, 7, Facing.SouthEast),
            ("thrasybios", 4, 8, Facing.SouthWest),
            ("citizen", 9, 1, Facing.SouthEast),
            ("damaris", 10, 2, Facing.NorthWest),
            ("pythia", 11, 3, Facing.NorthEast),
            ("citizen", 2, 10, Facing.SouthEast)
        };

        public static void BuildSandbox() => BatchRunner.Run(BuildAll);

        public static void CaptureReference() => BatchRunner.Run(CaptureReferenceImage);

        public static string FloorTileAt(int x, int y)
        {
            if (Blocks.Any(block => block.X == x && block.Y == y) || Actors.Any(actor => actor.X == x && actor.Y == y))
            {
                return FloorTileNames[0];
            }

            return FloorTileNames[(x / 3 + y / 3 * 2) % FloorTileNames.Length];
        }

        public static void BuildAll()
        {
            GenerateFogEdgeSources();
            ArtTools.RegenerateAll();
            EnsureSortingLayer(FogSortingLayer);
            RenderSetupTools.CreateCameraRigPrefab();
            EnsureTileAssets();
            AssetDatabase.SaveAssets();
            BuildScene();
            SystemMenuTools.ApplyBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SandboxTools] built {ScenePath}");
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

        private static void EnsureSortingLayer(string layerName)
        {
            if (SortingLayer.layers.Any(layer => layer.name == layerName))
            {
                return;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            layers.InsertArrayElementAtIndex(layers.arraySize);
            var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            layer.FindPropertyRelative("name").stringValue = layerName;
            layer.FindPropertyRelative("uniqueID").intValue = Math.Abs(layerName.GetHashCode());
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<string, Tile> EnsureTileAssets()
        {
            AssetFolders.Ensure(TileAssetFolder);
            var tiles = new Dictionary<string, Tile>();
            foreach (var path in Directory.GetFiles(ArtTools.TileFolder, "*.png", SearchOption.AllDirectories).Select(file => file.Replace('\\', '/')))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException($"missing sprite {path}");
                var tile = AssetFolders.LoadOrCreate<Tile>($"{TileAssetFolder}/{name}.asset");
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tiles[name] = tile;
            }

            return tiles;
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var tiles = EnsureTileAssets();

            var gridObject = new GameObject("Grid");
            gridObject.transform.position = GridPosition;
            var unityGrid = gridObject.AddComponent<UnityEngine.Grid>();
            unityGrid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
            unityGrid.cellSize = CellSize;

            var floor = CreateTilemap(gridObject, FloorTilemapName, SortingLayer.IDToName(0), FloorSortingOrder);
            var highlight = CreateTilemap(gridObject, HighlightTilemapName, SortingLayer.IDToName(0), HighlightSortingOrder);
            var blocks = CreateTilemap(gridObject, BlocksTilemapName, SortingLayer.IDToName(0), ActorSortingOrder);
            var fog = CreateTilemap(gridObject, FogTilemapName, FogSortingLayer, 0);

            var terrain = new TerrainId[Size * Size];
            var costs = new byte[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var floorName = FloorTileAt(x, y);
                    floor.SetTile(new Vector3Int(x, y, 0), tiles[floorName]);
                    terrain[y * Size + x] = FloorTerrain[floorName];
                    costs[y * Size + x] = floorName switch
                    {
                        "floor_water" => BlockedCost,
                        "floor_stone" => StoneCost,
                        _ => (byte)PathFinder.MinimumStepCost
                    };
                }
            }

            foreach (var block in Blocks)
            {
                blocks.SetTile(new Vector3Int(block.X, block.Y, 0), tiles[block.Tile]);
                costs[block.Y * Size + block.X] = BlockedCost;
            }

            var sandbox = new GameObject("GridSandbox");
            var gizmos = sandbox.AddComponent<GridGizmoView>();
            var controller = sandbox.AddComponent<GridSandboxController>();
            var actors = CreateActors();

            controller.ConfigureLayout(Size, Size, terrain, costs, FogRevealDepth);
            controller.ConfigureViews(
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath),
                highlight,
                fog,
                new TileBase[] { tiles["hl_select"], null },
                tiles["fog"],
                FogEdgeNames.Select(name => (TileBase)tiles[name]).ToArray(),
                actors,
                gizmos);
            controller.PaintFogPreview();

            CreateCamera(actors[0].transform);
            CreateGameServices();
            SystemMenuTools.CreateEventSystem();
            SystemMenuTools.InstantiatePauseMenu();

            AssetFolders.Ensure(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"could not save {ScenePath}");
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

        private static List<ActorView> CreateActors()
        {
            var root = new GameObject("Actors");
            var views = new List<ActorView>();
            foreach (var actor in Actors)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(ArtTools.SheetPathFor(actor.Character)).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                var row = FacingRules.SheetFacing(actor.Facing) == Facing.NorthEast ? "ne" : "se";

                var actorObject = new GameObject($"{actor.Character}_{actor.X}_{actor.Y}");
                actorObject.transform.SetParent(root.transform, false);

                var renderer = actorObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[$"{actor.Character}_{row}_0"];
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                renderer.sortingOrder = ActorSortingOrder;

                var animator = actorObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArtTools.ControllerPathFor(actor.Character));

                var view = actorObject.AddComponent<ActorView>();
                view.Configure(new Vector2Int(actor.X, actor.Y), actor.Facing, renderer, animator);
                views.Add(view);
            }

            return views;
        }

        private static void CreateCamera(Transform followTarget)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RenderSetupTools.CameraRigPrefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.position = new Vector3(followTarget.position.x, followTarget.position.y, RenderSetupTools.CameraDepth);
            rig.AddComponent<CinemachineBrain>();

            var boundsObject = new GameObject("CameraBounds");
            var bounds = boundsObject.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true;
            bounds.points = MapOutline();

            var virtualCameraObject = new GameObject("ExplorationCamera");
            var virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = ExplorationOrthographicSize;
            virtualCamera.Lens = lens;
            virtualCamera.Follow = followTarget;

            var composer = virtualCameraObject.AddComponent<CinemachinePositionComposer>();
            composer.Damping = CameraDamping;
            composer.CameraDistance = -RenderSetupTools.CameraDepth;

            virtualCameraObject.AddComponent<CinemachinePixelPerfect>();
            var confiner = virtualCameraObject.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = bounds;

            virtualCameraObject.AddComponent<ExplorationCamera>().Configure(virtualCamera, ExplorationOrthographicSize, BattleOrthographicSize);
        }

        private static Vector2[] MapOutline()
        {
            var min = -0.5f;
            var max = Size - 0.5f;
            return new[] { ToWorld(min, min), ToWorld(max, min), ToWorld(max, max), ToWorld(min, max) };
        }

        private static Vector2 ToWorld(float gridX, float gridY) =>
            new((gridX - gridY) * IsoMath.CellWidth, (gridX + gridY) * IsoMath.CellHeight);

        private static void CaptureReferenceImage()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
            camera.orthographicSize = CaptureHeight / (2f * CapturePixelsPerUnit);
            camera.transform.position = new Vector3(0f, CaptureCenterY, RenderSetupTools.CameraDepth);

            var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                texture.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(CapturePath) ?? ".");
                File.WriteAllBytes(CapturePath, texture.EncodeToPNG());
                Debug.Log($"[SandboxTools] captured {CapturePath}");
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
