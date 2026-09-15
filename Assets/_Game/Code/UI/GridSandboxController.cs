using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using TileData = Game.Grid.TileData;

namespace Game.UI
{
    public sealed class GridSandboxController : MonoBehaviour
    {
        public const string ActionMapName = "Grid";
        public const float NavigateThreshold = 0.5f;

        private const int FirstSpawnedActorId = 1000;
        private const float HudWidth = 260f;
        private const float HudHeight = 190f;
        private const float HudMargin = 8f;
        private const float PointerMoveThreshold = 0.01f;

        [SerializeField] private int width = 12;
        [SerializeField] private int height = 12;
        [SerializeField] private TerrainId[] terrain = new TerrainId[0];
        [SerializeField] private byte[] costs = new byte[0];
        [SerializeField] private int fogRevealDepth = 18;
        [SerializeField] private InputActionAsset controls;
        [SerializeField] private Tilemap highlightTilemap;
        [SerializeField] private Tilemap fogTilemap;
        [SerializeField] private TileBase[] selectionFrames = new TileBase[0];
        [SerializeField] private TileBase fogTile;
        [SerializeField] private TileBase[] fogEdgeTiles = new TileBase[0];
        [SerializeField] private List<ActorView> actors = new();
        [SerializeField] private GridGizmoView gizmos;

        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> path = new();
        private GridModel grid;
        private FogOfWar fog;
        private GridCursor cursor;
        private ExplorationSession session;
        private ActorView player;
        private InputActionMap actionMap;
        private InputAction pointAction;
        private InputAction navigateAction;
        private InputAction confirmAction;
        private InputAction cancelAction;
        private Vector2 lastPointer;
        private bool navigateHeld;
        private Vector3Int? paintedSelection;
        private double selectionMovedAt;
        private int nextActorId = FirstSpawnedActorId;

        public GridModel Grid => grid;

        public int Width => width;

        public int Height => height;

        public int FogRevealDepth => fogRevealDepth;

        public IReadOnlyList<TerrainId> Terrain => terrain;

        public IReadOnlyList<byte> Costs => costs;

        public InputActionAsset Controls => controls;

        public IReadOnlyList<ActorView> Actors => actors;

        public Tilemap FogTilemap => fogTilemap;

        public GridCursor Cursor => cursor;

        public ActorView Player => player;

        public bool ExplorationInputEnabled { get; set; } = true;

        public bool CursorInputEnabled { get; set; }

        public bool GizmosVisible
        {
            get => gizmos != null && gizmos.DrawGizmos;
            set
            {
                if (gizmos != null)
                {
                    gizmos.DrawGizmos = value;
                }
            }
        }

        public string DescribeCell(Coord cell)
        {
            if (grid == null || !grid.TryGet(cell, out var tile))
            {
                return $"gx {cell.X}  gy {cell.Y}\noutside the grid";
            }

            var occupant = tile.IsOccupied ? tile.OccupantId.ToString() : "none";
            return $"gx {cell.X}  gy {cell.Y}\nterrain {tile.Terrain}  cost {tile.Cost}\noccupant {occupant}\nrevealed {tile.Revealed}";
        }

        public bool TryMovePlayerTo(Coord cell)
        {
            if (player == null || !pathFinder.FindPath(grid, player.Walker.Cell, cell, path))
            {
                return false;
            }

            player.Walker.Follow(path);
            return true;
        }

        public bool TrySpawnTestActor(Coord cell)
        {
            if (actors.Count == 0 || !grid.TryGet(cell, out var tile) || !tile.IsPassable || tile.IsOccupied)
            {
                return false;
            }

            var template = actors[actors.Count > 1 ? 1 : 0];
            var clone = Instantiate(template, template.transform.parent);
            clone.Bind(new GridWalker(grid, nextActorId++, cell));
            actors.Add(clone);
            return true;
        }

        public int ClearTestActors()
        {
            var spawned = actors.FindAll(actor => actor.Walker != null && actor.Walker.ActorId >= FirstSpawnedActorId);
            foreach (var actor in spawned)
            {
                TryRemoveTestActor(actor.Walker.Cell);
            }

            return spawned.Count;
        }

        public bool TryRemoveTestActor(Coord cell)
        {
            var target = actors.Find(actor => actor != player && actor.Walker != null && actor.Walker.Cell == cell);
            if (target == null)
            {
                return false;
            }

            var tile = grid[cell];
            tile.OccupantId = TileData.NoOccupant;
            grid.TrySet(cell, tile);
            actors.Remove(target);
            Destroy(target.gameObject);
            return true;
        }

        public void ConfigureLayout(int newWidth, int newHeight, TerrainId[] newTerrain, byte[] newCosts, int newFogRevealDepth)
        {
            width = newWidth;
            height = newHeight;
            terrain = newTerrain;
            costs = newCosts;
            fogRevealDepth = newFogRevealDepth;
        }

        public void ConfigureViews(
            InputActionAsset newControls,
            Tilemap newHighlightTilemap,
            Tilemap newFogTilemap,
            TileBase[] newSelectionFrames,
            TileBase newFogTile,
            TileBase[] newFogEdgeTiles,
            List<ActorView> newActors,
            GridGizmoView newGizmos)
        {
            controls = newControls;
            highlightTilemap = newHighlightTilemap;
            fogTilemap = newFogTilemap;
            selectionFrames = newSelectionFrames;
            fogTile = newFogTile;
            fogEdgeTiles = newFogEdgeTiles;
            actors = newActors;
            gizmos = newGizmos;
        }

        public GridModel BuildGrid()
        {
            var model = new GridModel(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    model.TrySet(new Coord(x, y), new TileData
                    {
                        Terrain = index < terrain.Length ? terrain[index] : TerrainId.Marble,
                        Cost = index < costs.Length ? costs[index] : (byte)PathFinder.MinimumStepCost
                    });
                }
            }

            return model;
        }

        public void PaintFogPreview()
        {
            var previewGrid = BuildGrid();
            var previewFog = new FogOfWar(previewGrid);
            RevealInitialArea(previewGrid);
            if (actors.Count > 0)
            {
                previewFog.Reveal(ToCoord(actors[0].StartCell));
            }

            PaintFog(previewGrid);
        }

        private void Awake()
        {
            grid = BuildGrid();
            fog = new FogOfWar(grid);
            RevealInitialArea(grid);

            for (var index = 0; index < actors.Count; index++)
            {
                actors[index].Bind(new GridWalker(grid, index + 1, ToCoord(actors[index].StartCell), actors[index].StartFacing));
            }

            player = actors.Count > 0 ? actors[0] : null;
            if (player != null)
            {
                session = new ExplorationSession(player.Walker, fog);
            }

            cursor = new GridCursor(grid, player != null ? player.Walker.Cell : new Coord(0, 0));
            if (gizmos != null)
            {
                gizmos.Model = grid;
            }

            actionMap = controls.FindActionMap(ActionMapName, true);
            pointAction = actionMap.FindAction("Point", true);
            navigateAction = actionMap.FindAction("Navigate", true);
            confirmAction = actionMap.FindAction("Confirm", true);
            cancelAction = actionMap.FindAction("Cancel", true);
            actionMap.Enable();

            PaintFog(grid);
        }

        private void OnDestroy()
        {
            session?.Dispose();
            actionMap?.Disable();
        }

        private bool IsReady => grid != null && cursor != null && actionMap != null;

        public const string LocationId = "sandbox";

        private bool restoreChecked;

        public Game.Core.SaveData CaptureSave()
        {
            var services = FindAnyObjectByType<GameServices>();
            var previous = services && services.TryLoadActiveSave(out var existing) ? existing : null;
            var playtime = services ? services.PlaytimeSeconds : 0;
            return ExplorationSaveMapper.ToSave(session.Capture(), previous, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), LocationId, playtime);
        }

        private void TryRestoreActiveSave()
        {
            if (restoreChecked)
            {
                return;
            }

            var services = FindAnyObjectByType<GameServices>();
            if (services && !services.IsBooted)
            {
                return;
            }

            restoreChecked = true;
            if (services && player != null && services.TryLoadActiveSave(out var save) && ExplorationRestore.TryApply(session, save, LocationId, player.Walker.Facing))
            {
                cursor.TryHover(IsoMath.GridToWorld(player.Walker.Cell), Vector2.zero);
                PaintFog(grid);
            }
        }

        private void Start()
        {
            var pause = FindAnyObjectByType<PauseMenuController>();
            if (pause && session != null)
            {
                pause.SaveProvider = CaptureSave;
            }
        }

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            TryRestoreActiveSave();
            if (ExplorationInputEnabled || CursorInputEnabled)
            {
                ReadPointer();
                ReadNavigation();
            }

            if (ExplorationInputEnabled)
            {
                if (confirmAction.WasPressedThisFrame())
                {
                    MovePlayerToCursor();
                }

                if (cancelAction.WasPressedThisFrame())
                {
                    player?.Walker.Cancel();
                }
            }

            foreach (var actor in actors)
            {
                actor.Walker?.Advance(Time.deltaTime);
            }

            PaintFog(grid);
            PaintSelection();
        }

        private void OnGUI()
        {
            if (!IsReady)
            {
                return;
            }

            var cell = cursor.Cell;
            GUILayout.BeginArea(new Rect(HudMargin, HudMargin, HudWidth, HudHeight), GUI.skin.box);
            GUILayout.Label(DescribeCell(cell));

            if (GUILayout.Button("Spawn test actor"))
            {
                TrySpawnTestActor(cell);
            }

            if (GUILayout.Button("Remove test actor"))
            {
                TryRemoveTestActor(cell);
            }

            GizmosVisible = GUILayout.Toggle(GizmosVisible, "Grid gizmos");
            GUILayout.EndArea();
        }

        private void ReadPointer()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var screen = pointAction.ReadValue<Vector2>();
            if ((screen - lastPointer).sqrMagnitude < PointerMoveThreshold)
            {
                return;
            }

            lastPointer = screen;
            var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            cursor.TryHover(world, Vector2.zero);
        }

        private void ReadNavigation()
        {
            var direction = navigateAction.ReadValue<Vector2>();
            if (direction.magnitude < NavigateThreshold)
            {
                navigateHeld = false;
                return;
            }

            if (!navigateHeld)
            {
                cursor.TryStep(direction);
            }

            navigateHeld = true;
        }

        private void MovePlayerToCursor() => TryMovePlayerTo(cursor.Cell);

        private void RevealInitialArea(GridModel model)
        {
            for (var y = 0; y < model.Height; y++)
            {
                for (var x = 0; x < model.Width; x++)
                {
                    if (x + y >= fogRevealDepth)
                    {
                        continue;
                    }

                    var cell = new Coord(x, y);
                    var tile = model[cell];
                    tile.Revealed = true;
                    model.TrySet(cell, tile);
                }
            }
        }

        private void PaintFog(GridModel model)
        {
            if (fogTilemap == null)
            {
                return;
            }

            for (var y = 0; y < model.Height; y++)
            {
                for (var x = 0; x < model.Width; x++)
                {
                    fogTilemap.SetTile(new Vector3Int(x, y, 0), FogTileFor(FogTileRules.KindAt(model, new Coord(x, y))));
                }
            }
        }

        private TileBase FogTileFor(FogTileKind kind)
        {
            var edgeIndex = kind switch
            {
                FogTileKind.EdgeNorthEast => 0,
                FogTileKind.EdgeNorthWest => 1,
                FogTileKind.EdgeSouthEast => 2,
                FogTileKind.EdgeSouthWest => 3,
                _ => -1
            };

            if (edgeIndex >= 0)
            {
                return edgeIndex < fogEdgeTiles.Length ? fogEdgeTiles[edgeIndex] : fogTile;
            }

            return kind == FogTileKind.Full ? fogTile : null;
        }

        private void PaintSelection()
        {
            if (highlightTilemap == null)
            {
                return;
            }

            var target = new Vector3Int(cursor.Cell.X, cursor.Cell.Y, 0);
            if (paintedSelection != target)
            {
                if (paintedSelection.HasValue)
                {
                    highlightTilemap.SetTile(paintedSelection.Value, null);
                }

                selectionMovedAt = Time.timeAsDouble;
            }

            var frame = SelectionPulse.FrameSince(Time.timeAsDouble, selectionMovedAt);
            highlightTilemap.SetTile(target, frame < selectionFrames.Length ? selectionFrames[frame] : null);
            paintedSelection = target;
        }

        private static Coord ToCoord(Vector2Int cell) => new(cell.x, cell.y);
    }
}
