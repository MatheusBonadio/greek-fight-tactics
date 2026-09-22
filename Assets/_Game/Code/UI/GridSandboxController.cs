using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        [SerializeField] private byte[] levels = new byte[0];
        [SerializeField] private int fogRevealDepth = 18;
        [SerializeField] private InputActionAsset controls;
        [SerializeField] private LevelTilemaps highlightTilemap;
        [SerializeField] private LevelTilemaps fogTilemap;
        [SerializeField] private TileBase[] selectionFrames = new TileBase[0];
        [SerializeField] private TileBase fogTile;
        [SerializeField] private TileBase[] fogEdgeTiles = new TileBase[0];
        [SerializeField] private LevelTilemaps blocksTilemap;
        [SerializeField] private TileBase[] blockTiles = new TileBase[0];
        [SerializeField] private LevelTilemaps facesTilemap;
        [SerializeField] private FaceTile[] faceTiles = new FaceTile[0];
        [SerializeField] private LevelTilemaps exitsTilemap;
        [SerializeField] private List<ActorView> actors = new();
        [SerializeField] private GridGizmoView gizmos;
        [SerializeField] private string locationId = DefaultLocationId;
        [SerializeField] private MapExitLink[] exits = new MapExitLink[0];
        [SerializeField] private List<ActorPresenceLink> presences = new();
        [SerializeField] private List<BlockPresenceLink> blockPresences = new();
        [SerializeField] private bool home;

        private readonly PathFinder pathFinder = new();
        private readonly HashSet<Coord> hiddenBlocks = new();
        private readonly List<Coord> path = new();
        private readonly HashSet<Coord> closedExits = new();
        private readonly HashSet<ActorView> departed = new();
        private readonly HashSet<ActorView> arriving = new();
        private readonly List<Coord> walkedPath = new();
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
        private PauseMenuController pause;
        private Vector2 lastPointer;
        private bool navigateHeld;
        private Vector3Int? paintedSelection;
        private double selectionMovedAt;
        private bool exitCheckPending;
        private bool leaving;
        private bool arrived;
        private PendingArrival departure;
        private int nextActorId = FirstSpawnedActorId;

        public GridModel Grid => grid;

        public int Width => width;

        public int Height => height;

        public int FogRevealDepth => fogRevealDepth;

        public IReadOnlyList<TerrainId> Terrain => terrain;

        public IReadOnlyList<byte> Costs => costs;

        public IReadOnlyList<byte> Levels => levels;

        public InputActionAsset Controls => controls;

        public IReadOnlyList<ActorView> Actors => actors;

        public LevelTilemaps FogTilemap => fogTilemap;

        public LevelTilemaps BlocksTilemap => blocksTilemap;

        public IReadOnlyList<TileBase> BlockTiles => blockTiles;

        public LevelTilemaps FacesTilemap => facesTilemap;

        public IReadOnlyList<FaceTile> FaceTiles => faceTiles;

        public LevelTilemaps ExitsTilemap => exitsTilemap;

        public LevelTilemaps HighlightTilemap => highlightTilemap;

        public MapExitLink HoveredExit
        {
            get
            {
                if (!IsReady || !ExplorationInputEnabled || !StateMachine.IsExplorationInputEnabled || !FogTileRules.ShowsWhatStandsOn(grid, cursor.Cell))
                {
                    return null;
                }

                foreach (var exit in exits)
                {
                    if (exit.Cell == cursor.Cell && IsExitOpen(exit))
                    {
                        return exit;
                    }
                }

                return null;
            }
        }

        public bool IsExitOpen(MapExitLink exit) => exit != null && !closedExits.Contains(exit.Cell);

        public GridCursor Cursor => cursor;

        public ActorView Player => player;

        public GameStateMachine StateMachine { get; } = new();

        public System.Func<Coord, bool> Interaction { get; set; }

        public IStoryShortcut StoryShortcut { get; set; }

        public event System.Action<int> FogRevealed;

        public IReadOnlyList<ActorPresenceLink> Presences => presences;

        public bool ExplorationInputEnabled { get; set; } = true;

        public bool CursorInputEnabled { get; set; }

        public bool DebugPanelVisible { get; set; }

        public bool IsOverDebugPanel(Vector2 screenPoint) =>
            DebugPanelVisible && DebugPanelArea.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));

        private static Rect DebugPanelArea => new(HudMargin, HudMargin, HudWidth, HudHeight);

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
            return $"gx {cell.X}  gy {cell.Y}  level {tile.Level}\nterrain {tile.Terrain}  cost {tile.Cost}\noccupant {occupant}\nrevealed {tile.Revealed}";
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

        public bool TryDismiss(ActorView actor)
        {
            if (!Withdraw(actor))
            {
                return false;
            }

            departed.Add(actor);
            return true;
        }

        public bool ApplyStory(Game.Core.SaveData story)
        {
            story ??= new Game.Core.SaveData();
            ApplyGates(story);
            return ApplyPresences(story);
        }

        private bool Withdraw(ActorView actor)
        {
            if (!actor || actor == player || actor.Walker == null)
            {
                return false;
            }

            var walker = actor.Walker;
            walker.Cancel();
            if (grid.TryGet(walker.Cell, out var tile) && tile.OccupantId == walker.ActorId)
            {
                tile.OccupantId = TileData.NoOccupant;
                grid.TrySet(walker.Cell, tile);
            }

            actor.Bind(null);
            actor.gameObject.SetActive(false);
            return true;
        }

        public void ConfigureLayout(int newWidth, int newHeight, TerrainId[] newTerrain, byte[] newCosts, int newFogRevealDepth, byte[] newLevels = null)
        {
            width = newWidth;
            height = newHeight;
            terrain = newTerrain;
            costs = newCosts;
            fogRevealDepth = newFogRevealDepth;
            levels = newLevels ?? new byte[0];
        }

        public void ConfigureViews(
            InputActionAsset newControls,
            LevelTilemaps newHighlightTilemap,
            LevelTilemaps newFogTilemap,
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

        public void ConfigureBlocks(LevelTilemaps newBlocksTilemap, TileBase[] newBlockTiles)
        {
            blocksTilemap = newBlocksTilemap;
            blockTiles = newBlockTiles ?? new TileBase[0];
        }

        public void ConfigureFaces(LevelTilemaps newFacesTilemap, FaceTile[] newFaceTiles)
        {
            facesTilemap = newFacesTilemap;
            faceTiles = newFaceTiles ?? new FaceTile[0];
        }

        public void ConfigureExitMarkers(LevelTilemaps newExitsTilemap)
        {
            exitsTilemap = newExitsTilemap;
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
                        Cost = index < costs.Length ? costs[index] : (byte)PathFinder.MinimumStepCost,
                        Level = index < levels.Length ? levels[index] : TileData.GroundLevel
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
            CloseGatedExits(previewGrid, previewFog);
            if (actors.Count > 0)
            {
                previewFog.Reveal(ToCoord(actors[0].StartCell));
            }

            PaintFog(previewGrid);
            foreach (var actor in actors)
            {
                actor.IsShown = FogTileRules.ShowsWhatStandsOn(previewGrid, ToCoord(actor.StartCell));
            }
        }

        private void Awake()
        {
            grid = BuildGrid();
            fog = new FogOfWar(grid);
            RevealInitialArea(grid);
            CloseGatedExits(grid, fog);

            for (var index = 0; index < actors.Count; index++)
            {
                if (IsConditional(actors[index]))
                {
                    actors[index].gameObject.SetActive(false);
                    continue;
                }

                actors[index].Bind(new GridWalker(grid, index + 1, ToCoord(actors[index].StartCell), actors[index].StartFacing));
            }

            player = actors.Count > 0 ? actors[0] : null;
            if (player != null)
            {
                session = new ExplorationSession(player.Walker, fog);
                session.FogRevealed += OnFogRevealed;
                player.Walker.CellEntered += OnPlayerCellEntered;
                TryArrive();
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
            if (player != null && player.Walker != null)
            {
                player.Walker.CellEntered -= OnPlayerCellEntered;
            }

            session?.Dispose();
            actionMap?.Disable();
        }

        private void OnPlayerCellEntered(Coord cell)
        {
            exitCheckPending = true;
        }

        private void OnFogRevealed(int count) => FogRevealed?.Invoke(count);

        private bool IsConditional(ActorView actor) => presences.Exists(presence => presence.Actor == actor);

        private bool ApplyPresences(Game.Core.SaveData story)
        {
            var placedEveryone = true;
            foreach (var presence in presences)
            {
                var actor = presence.Actor;
                if (!actor)
                {
                    continue;
                }

                var met = presence.IsMet(story);
                if (!met)
                {
                    Withdraw(actor);
                    continue;
                }

                if (actor.Walker == null && !departed.Contains(actor))
                {
                    placedEveryone &= TryPlace(actor, presence.WalksIn ? presence.Entrance : (Coord?)null);
                }
            }

            return placedEveryone;
        }

        public bool IsArriving(ActorView actor) =>
            actor && arriving.Contains(actor) && actor.Walker != null && actor.Walker.IsMoving;

        public bool IsSettling
        {
            get
            {
                foreach (var actor in arriving)
                {
                    if (actor && actor.Walker != null && actor.Walker.IsMoving)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void SettleArrivals()
        {
            if (arriving.Count == 0)
            {
                return;
            }

            arriving.RemoveWhere(actor => !actor || actor.Walker == null || HasTakenItsPlace(actor));
        }

        private bool HasTakenItsPlace(ActorView actor)
        {
            var walker = actor.Walker;
            if (walker.IsMoving)
            {
                return false;
            }

            var post = ToCoord(actor.StartCell);
            if (walker.Cell == post)
            {
                return true;
            }

            if (pathFinder.FindPath(grid, walker.Cell, post, walkedPath))
            {
                walker.Follow(walkedPath);
            }

            return false;
        }

        private bool TryPlace(ActorView actor, Coord? entrance = null)
        {
            var post = ToCoord(actor.StartCell);
            var cell = entrance ?? post;
            if (!grid.TryGet(cell, out var tile) || !tile.IsPassable || tile.IsOccupied)
            {
                return false;
            }

            var walker = new GridWalker(grid, actors.IndexOf(actor) + 1, cell, actor.StartFacing);
            actor.gameObject.SetActive(true);
            actor.Bind(walker);
            if (entrance.HasValue && pathFinder.FindPath(grid, cell, post, walkedPath))
            {
                walker.Follow(walkedPath);
                arriving.Add(actor);
            }

            return true;
        }

        private void CloseGatedExits(GridModel model, FogOfWar modelFog)
        {
            foreach (var exit in exits)
            {
                if (exit != null && exit.IsGated && CellGate.Close(model, modelFog, exit.Cell) && model == grid)
                {
                    closedExits.Add(exit.Cell);
                }
            }
        }

        private void ApplyGates(Game.Core.SaveData story)
        {
            foreach (var exit in exits)
            {
                if (exit == null || !exit.IsGated)
                {
                    continue;
                }

                var open = exit.IsOpenFor(story);
                if (open && closedExits.Remove(exit.Cell))
                {
                    CellGate.Open(grid, fog, exit.Cell, CostAt(exit.Cell));
                }
                else if (!open && closedExits.Add(exit.Cell))
                {
                    CellGate.Close(grid, fog, exit.Cell);
                }
            }
        }

        private byte CostAt(Coord cell)
        {
            var index = cell.Y * width + cell.X;
            return index < costs.Length ? costs[index] : (byte)PathFinder.MinimumStepCost;
        }

        private void TryArrive()
        {
            if (!SceneArrival.TryConsume(locationId, out var arrival)
                || !grid.TryGet(arrival.Cell, out var tile)
                || !tile.IsPassable
                || (tile.IsOccupied && tile.OccupantId != player.Walker.ActorId))
            {
                return;
            }

            player.Walker.Teleport(arrival.Cell, arrival.Facing);
            fog.Reveal(arrival.Cell);
            arrived = true;
            departure = arrival;
        }

        private void TryLeaveThroughExit()
        {
            if (!exitCheckPending || leaving || player == null || player.Walker.IsMoving)
            {
                return;
            }

            exitCheckPending = false;
            foreach (var exit in exits)
            {
                if (exit.Cell != player.Walker.Cell || !IsExitOpen(exit))
                {
                    continue;
                }

                leaving = true;
                SceneArrival.Set(new PendingArrival(exit.Location, exit.Arrival, exit.Facing, locationId, session.Capture().CopyFogBitmask()));
                SceneManager.LoadScene(LocationScenes.SceneFor(exit.Location));
                return;
            }
        }

        private bool IsReady => grid != null && cursor != null && actionMap != null;

        public const string DefaultLocationId = "sandbox";

        public string LocationId => locationId;

        public void ConfigureLocation(string id)
        {
            locationId = string.IsNullOrWhiteSpace(id) ? DefaultLocationId : id;
        }

        public IReadOnlyList<MapExitLink> Exits => exits;

        public void ConfigureExits(MapExitLink[] links)
        {
            exits = links ?? new MapExitLink[0];
        }

        public void ConfigurePresences(List<ActorPresenceLink> links)
        {
            presences = links ?? new List<ActorPresenceLink>();
        }

        public IReadOnlyList<BlockPresenceLink> BlockPresences => blockPresences;

        public bool IsHome => home;

        public bool IsBlockShown(Coord cell) => grid != null && grid.Contains(cell) && BlockTileAt(cell.X, cell.Y) != null;

        public void ConfigureBlockPresences(List<BlockPresenceLink> links)
        {
            blockPresences = links ?? new List<BlockPresenceLink>();
        }

        public void ConfigureHome(bool isHome)
        {
            home = isHome;
        }

        private void ShowConditionalBlocks(Game.Core.SaveData story)
        {
            foreach (var presence in blockPresences)
            {
                var cell = presence.Cell;
                if (presence.IsMet(story) || !grid.TryGet(cell, out var tile) || tile.IsOccupied)
                {
                    continue;
                }

                tile.Cost = presence.OpenCost;
                grid.TrySet(cell, tile);
                hiddenBlocks.Add(cell);
            }
        }

        private bool restoreChecked;

        public DuelProgress Duels { get; } = new();

        public bool ProgressLoaded => restoreChecked;

        public bool ArrivedThroughExit => arrived;

        public Game.Core.SaveData CaptureSave()
        {
            var services = FindAnyObjectByType<GameServices>();
            var previous = services && services.TryLoadActiveSave(out var existing) ? existing : null;
            var playtime = services ? services.PlaytimeSeconds : 0;
            var unlockedFallacies = services ? services.Compendium.UnlockedIds : null;
            var save = ExplorationSaveMapper.ToSave(session.Capture(), previous, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), LocationId, playtime, unlockedFallacies, Duels);
            if (!string.IsNullOrEmpty(departure.DepartureLocationId) && departure.DepartureLocationId != LocationId && departure.DepartureFog != null)
            {
                save.FogByLocation[departure.DepartureLocationId] = departure.DepartureFog;
            }

            return save;
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
            var story = services && services.TryLoadActiveSave(out var progress) ? progress : null;
            if (story != null)
            {
                Duels.Load(story.Duels);
            }

            var cameHome = arrived && home;
            story ??= new Game.Core.SaveData();
            if (cameHome)
            {
                StoryDays.Pass(story);
                StoryStamina.Rest(story);
            }

            ShowConditionalBlocks(story);
            RestorePlayer(services, cameHome);
            ApplyStory(story);
        }

        private void RestorePlayer(GameServices services, bool cameHome)
        {
            if (arrived)
            {
                if (services && services.IsBooted)
                {
                    if (services.TryLoadActiveSave(out var saved) && ExplorationRestore.TryMergeFog(session, saved, LocationId))
                    {
                        PaintFog(grid);
                    }

                    var arrival = CaptureSave();
                    if (cameHome)
                    {
                        StoryDays.Pass(arrival);
                        StoryStamina.Rest(arrival);
                    }

                    services.Autosave(arrival);
                }

                return;
            }

            if (services && player != null && services.TryLoadActiveSave(out var save) && ExplorationRestore.TryApply(session, save, LocationId, player.Walker.Facing))
            {
                cursor.TryPoint(player.Walker.Cell);
                PaintFog(grid);
            }
        }

        private void Start()
        {
            pause = FindAnyObjectByType<PauseMenuController>();
            if (!pause)
            {
                return;
            }

            pause.StateMachine = StateMachine;
            if (session != null)
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
            SettleArrivals();
            if (Debug.isDebugBuild && Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                DebugPanelVisible = !DebugPanelVisible;
            }

            var menuCapturesInput = pause && pause.CapturesInput;
            var explorationInput = ExplorationInputEnabled && StateMachine.IsExplorationInputEnabled && !menuCapturesInput;
            if (menuCapturesInput)
            {
                SkipCursorInput();
            }
            else if (explorationInput || CursorInputEnabled)
            {
                ReadPointer();
                ReadNavigation();
            }

            if (explorationInput)
            {
                if (ConfirmPressedOutsideDebugPanel())
                {
                    ConfirmCursor();
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
            ShowActorsOnRevealedCells();
            PaintSelection();
            TryLeaveThroughExit();
        }

        private void OnGUI()
        {
            if (!IsReady || !DebugPanelVisible)
            {
                return;
            }

            var cell = cursor.Cell;
            GUILayout.BeginArea(DebugPanelArea, GUI.skin.box);
            GUILayout.Label(DescribeCell(cell));

            if (GUILayout.Button("Spawn test actor"))
            {
                TrySpawnTestActor(cell);
            }

            if (GUILayout.Button("Remove test actor"))
            {
                TryRemoveTestActor(cell);
            }

            if (StoryShortcut != null)
            {
                GUILayout.Label($"story: {StoryShortcut.ReachedLabel}");
                var next = StoryShortcut.NextLabel;
                if (next != null && GUILayout.Button($"F4  skip to {next}"))
                {
                    StoryShortcut.SkipToNextCheckpoint();
                }
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
            if (IsOverDebugPanel(screen) || IsPointerOverUi())
            {
                return;
            }

            var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            cursor.TryHover(world, Vector2.zero);
        }

        private void SkipCursorInput()
        {
            lastPointer = pointAction.ReadValue<Vector2>();
            navigateHeld = navigateAction.ReadValue<Vector2>().magnitude >= NavigateThreshold;
        }

        private bool ConfirmPressedOutsideDebugPanel()
        {
            if (!confirmAction.WasPressedThisFrame())
            {
                return false;
            }

            var mouse = Mouse.current;
            return mouse == null || !mouse.leftButton.wasPressedThisFrame || (!IsOverDebugPanel(mouse.position.ReadValue()) && !IsPointerOverUi());
        }

        public static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

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

        public void ConfirmCursor()
        {
            if (Interaction == null || !Interaction(cursor.Cell))
            {
                TryMovePlayerTo(cursor.Cell);
            }
        }

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
            for (var y = 0; y < model.Height; y++)
            {
                for (var x = 0; x < model.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var position = new Vector3Int(x, y, 0);
                    if (fogTilemap)
                    {
                        fogTilemap.SetTile(position, FogTileFor(FogTileRules.KindAt(model, cell)));
                    }

                    if (blocksTilemap)
                    {
                        blocksTilemap.SetTile(position, FogTileRules.ShowsWhatStandsOn(model, cell) ? BlockTileAt(x, y) : null);
                    }
                }
            }

            PaintFaces(model);
            PaintExitMarkers(model);
        }

        private void PaintFaces(GridModel model)
        {
            if (!facesTilemap)
            {
                return;
            }

            foreach (var face in faceTiles)
            {
                facesTilemap.SetTile(face.Position, face.Level, FogTileRules.ShowsWhatStandsOn(model, face.Cell) ? face.Tile : null);
            }
        }

        private void PaintExitMarkers(GridModel model)
        {
            if (!exitsTilemap)
            {
                return;
            }

            foreach (var exit in exits)
            {
                exitsTilemap.SetTile(new Vector3Int(exit.Cell.X, exit.Cell.Y, 0), IsExitOpen(exit) && FogTileRules.ShowsWhatStandsOn(model, exit.Cell) ? exit.Marker : null);
            }
        }

        private TileBase BlockTileAt(int x, int y)
        {
            var index = y * width + x;
            return index < blockTiles.Length && !hiddenBlocks.Contains(new Coord(x, y)) ? blockTiles[index] : null;
        }

        private void ShowActorsOnRevealedCells()
        {
            foreach (var actor in actors)
            {
                if (actor && actor.Walker != null)
                {
                    actor.IsShown = FogTileRules.ShowsWhatStandsAt(grid, actor.Walker.WorldPosition);
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
            if (!highlightTilemap)
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
