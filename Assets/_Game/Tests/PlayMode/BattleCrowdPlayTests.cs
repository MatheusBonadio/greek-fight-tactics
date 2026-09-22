using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class BattleCrowdPlayTests
    {
        private const string SandboxScene = "GridSandbox";
        private const string MapBoundsComponent = "CinemachineConfiner2D";
        private const string PixelPerfectComponent = "PixelPerfectCamera";
        private const string BrainComponent = "CinemachineBrain";
        private const string CapturePath = "artifacts/reference/battle_crowd.png";
        private const string ArrivalCapturePath = "artifacts/reference/battle_crowd_arriving.png";
        private const float ArrivalCaptureProgress = 0.5f;
        private const float StartTimeoutSeconds = 5f;
        private const float BattleTimeoutSeconds = 90f;
        private const float SettleTimeoutSeconds = 3f;
        private const float ArrivalSlackSeconds = 0.5f;
        private const float DepartureTimeoutSeconds = 12f;
        private const float ArtPixelsPerUnit = 16f;
        private const float PixelTolerance = 1f / ArtPixelsPerUnit + 1e-3f;
        private const float SettleSeconds = 0.6f;
        private const float StillSeconds = 0.3f;
        private const int MaxApproachTurns = 6;
        private const int CrowdSeed = 402;
        private const int CaptureHeight = 896;
        private const int CaptureWidth = 1600;
        private const float CaptureOrthographicSize = 7f;
        private const float CaptureDepth = -10f;

        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreData()
        {
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Battle_KeepsTheZoomAndCentresTheCameraOnTheDuelThenBackOnKleia()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            var camera = Camera.main;
            var explorationCamera = director.ExplorationCamera;
            var confiner = explorationCamera.GetComponent(MapBoundsComponent) as Behaviour;
            Assert.IsNotNull(confiner, "the exploration camera must be confined to the map");
            confiner.enabled = false;
            var zoom = camera.orthographicSize;
            var lens = explorationCamera.LensOrthographicSize;
            var probe = new GameObject("camera probe").AddComponent<CameraProbe>();
            probe.Watch(camera.transform);

            yield return EngageAtRest(director, sandbox, CentralOpponent(director, sandbox));
            probe.Clear();
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.PlayerChoosing, () =>
            {
                Assert.AreEqual(zoom, camera.orthographicSize, "the battle must not zoom");
                Assert.AreEqual(lens, explorationCamera.LensOrthographicSize, "the battle must not zoom");
            });
            yield return WaitUntilStill(probe, explorationCamera);

            Assert.AreSame(explorationCamera.DuelFocus, explorationCamera.FollowTarget, "the camera follows the duel during the battle");
            AssertOnPixel(Snap(director.Arena.WorldCenter), camera.transform.position, "the camera must centre on the duel");
            AssertMovesOneWay(probe.Positions, "moving in to the duel");

            probe.Clear();
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.Exploring, () => Assert.AreEqual(zoom, camera.orthographicSize), _ => new BattleMenuChoice(ActionId.Withdraw));
            yield return WaitUntilStill(probe, explorationCamera);

            Assert.AreSame(sandbox.Player.transform, explorationCamera.FollowTarget, "the camera follows Kleia again after the battle");
            AssertOnPixel(Snap(sandbox.Player.transform.position), camera.transform.position, "the camera must come back to Kleia");
            AssertMovesOneWay(probe.Positions, "moving back to Kleia");
            Assert.AreEqual(zoom, camera.orthographicSize);
            Object.Destroy(probe.gameObject);
        }

        [UnityTest]
        public IEnumerator Crowd_WalksInToFreeRingCellsAndFacesTheDuelBeforeTheFirstTurn()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            var crowd = director.Crowd;
            var target = CentralOpponent(director, sandbox);

            yield return EngageAtRest(director, sandbox, target);
            var arena = director.Arena;
            Assert.IsNotNull(arena);
            Assert.IsTrue(crowd.IsGathering, "the crowd walks in at the start of the battle");
            Assert.GreaterOrEqual(crowd.Gathered.Count, crowd.Rules.MinimumCrowd, "a duel in the middle of the agora draws a big crowd");
            Assert.LessOrEqual(crowd.Gathered.Count, crowd.Rules.MaximumCrowd);
            foreach (var spectator in crowd.Gathered)
            {
                Assert.IsFalse(arena.IsLit(spectator.Seat.Spawn), $"spectator {spectator.Id} must arrive from outside the arena");
                Assert.IsFalse(spectator.IsSeated, $"spectator {spectator.Id} must walk in");
            }

            var capturedArrival = false;
            yield return PlayUntil(director, () => director.Phase is BattleFlowPhase.PlayerChoosing or BattleFlowPhase.OpponentThinking, () =>
            {
                if (!capturedArrival && crowd.IsGathering && crowd.GatherSeconds >= crowd.ExpectedArrivalSeconds * ArrivalCaptureProgress)
                {
                    capturedArrival = true;
                    Capture(director, ArrivalCapturePath);
                }

                if (director.Phase is BattleFlowPhase.PlayerChoosing or BattleFlowPhase.OpponentThinking or BattleFlowPhase.Animating)
                {
                    Assert.IsTrue(crowd.HasArrived, "a turn started before the crowd arrived");
                }
            });

            Assert.IsTrue(crowd.HasArrived);
            Assert.LessOrEqual(crowd.GatherSeconds, crowd.ArrivalCapSeconds + ArrivalSlackSeconds, "the crowd must arrive within the cap");
            var battleGrid = director.Session.State.Grid;
            var seats = new HashSet<Coord>();
            foreach (var spectator in crowd.Gathered)
            {
                var seat = spectator.Seat.Seat;
                Assert.IsTrue(seats.Add(seat), $"two spectators share {seat}");
                Assert.AreEqual(seat, spectator.Walker.Cell, $"spectator {spectator.Id} did not reach its ring cell");
                Assert.IsFalse(spectator.Walker.IsMoving);
                Assert.AreEqual(arena.FacingFrom(seat), spectator.Walker.Facing, $"spectator {spectator.Id} must face the duel");
                Assert.GreaterOrEqual(arena.DistanceFromCenter(seat), crowd.Rules.InnerRadius);
                Assert.IsTrue(sandbox.Grid[seat].IsPassable);
                Assert.IsFalse(sandbox.Grid[seat].IsOccupied, "spectators never take a cell of the exploration grid");
                Assert.AreEqual(spectator.Id, battleGrid[seat].OccupantId, "the battle grid must block the spectator's cell");
                Assert.AreNotEqual(seat, director.Session.State.Player.Position);
                Assert.AreNotEqual(seat, director.Session.State.Opponent.Position);
            }

            var spectatorViews = crowd.Gathered.Select(spectator => spectator.View).ToList();
            CollectionAssert.IsEmpty(sandbox.Actors.Intersect(spectatorViews), "spectators are not map actors");
            CollectionAssert.IsEmpty(director.Opponents.Select(entry => entry.Actor).Intersect(spectatorViews), "spectators never provoke");
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.Exploring, null, _ => new BattleMenuChoice(ActionId.Withdraw));
        }

        [UnityTest]
        public IEnumerator Circulate_CannotMoveKleiaOntoASpectator()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            yield return EngageAtRest(director, sandbox, CentralOpponent(director, sandbox));
            var seats = director.Crowd.Gathered.Select(spectator => spectator.Seat.Seat).ToList();
            var blockedWithinReach = 0;

            for (var turn = 0; turn < MaxApproachTurns && blockedWithinReach == 0; turn++)
            {
                yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.PlayerChoosing);
                director.SubmitChoice(new BattleMenuChoice(ActionId.Circulate));
                Assert.AreEqual(BattleFlowPhase.PlayerTargeting, director.Phase);
                var state = director.Session.State;
                foreach (var seat in seats)
                {
                    Assert.AreEqual(director.Crowd.Gathered.Single(spectator => spectator.Seat.Seat == seat).Id, state.Grid[seat].OccupantId, $"{seat} must stay blocked for the whole battle");
                    Assert.IsFalse(director.TryChooseDestination(seat), $"CIRCULAR moved Kleia onto the spectator at {seat}");
                    Assert.AreEqual(BattleFlowPhase.PlayerTargeting, director.Phase);
                    blockedWithinReach += IsReachableWithout(state, seat) ? 1 : 0;
                }

                if (blockedWithinReach == 0)
                {
                    Assert.IsTrue(director.TryChooseDestination(StepTowardTheCrowd(state, seats)), "Kleia walks towards the crowd");
                }
            }

            Assert.Greater(blockedWithinReach, 0, "Kleia never came within reach of a spectator's cell");
            director.CancelTargeting();
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.Exploring, null, _ => new BattleMenuChoice(ActionId.Withdraw));
        }

        [UnityTest]
        public IEnumerator Veil_DarkensEverythingOutsideTheArenaDuringTheBattleAndLiftsAfter()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            var veil = director.Veil;
            var fog = RevealedCells(sandbox.Grid);
            var materials = sandbox.Actors.ToDictionary(actor => actor, actor => actor.SpriteRenderer.sharedMaterial);
            Assert.IsFalse(veil.IsShown);
            Assert.AreEqual(0, CountTiles(veil.FloorVeil));

            var target = CentralOpponent(director, sandbox);
            yield return EngageAtRest(director, sandbox, target);
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.PlayerChoosing);
            yield return WaitFor(() => Mathf.Approximately(veil.Density, ArenaVeilView.MaximumDensity), SettleTimeoutSeconds);

            var arena = director.Arena;
            Assert.IsTrue(veil.IsShown);
            Assert.AreEqual(ArenaVeilView.MaximumDensity, veil.Density, "the veil is a 50% checker at full strength");
            var outside = 0;
            for (var y = 0; y < sandbox.Grid.Height; y++)
            {
                for (var x = 0; x < sandbox.Grid.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var lit = arena.IsLit(cell);
                    outside += lit ? 0 : 1;
                    Assert.AreEqual(!lit, veil.FloorVeil.HasTile(new Vector3Int(x, y, 0)), $"{cell} lit {lit}");
                }
            }

            Assert.Greater(outside, 0);
            Assert.AreEqual(outside, veil.VeiledCellCount);
            Assert.Greater(veil.VeiledBlockCount, 0, "columns and walls outside the arena are darkened too");
            foreach (var actor in sandbox.Actors.Where(actor => actor.Walker != null))
            {
                var veiled = actor != sandbox.Player && actor != target.Actor && !arena.IsLit(actor.Walker.Cell);
                Assert.AreEqual(veiled, actor.SpriteRenderer.sharedMaterial == veil.ActorMaterial, $"{actor.name} veiled {veiled}");
            }

            foreach (var spectator in director.Crowd.Gathered)
            {
                Assert.AreNotSame(veil.ActorMaterial, spectator.View.SpriteRenderer.sharedMaterial, "the crowd stands in the light");
            }

            Capture(director, CapturePath);
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.Exploring, null, _ => new BattleMenuChoice(ActionId.Withdraw));
            yield return WaitFor(() => !veil.IsShown, SettleTimeoutSeconds);

            Assert.IsFalse(veil.IsShown, "the veil lifts after the battle");
            Assert.AreEqual(0, CountTiles(veil.FloorVeil));
            Assert.AreEqual(0, CountTiles(veil.BlocksVeil));
            foreach (var (actor, material) in materials)
            {
                Assert.AreSame(material, actor.SpriteRenderer.sharedMaterial, $"{actor.name} kept the veil");
            }

            CollectionAssert.AreEqual(fog, RevealedCells(sandbox.Grid), "the veil is render only and never touches the fog of war");
        }

        [UnityTest]
        public IEnumerator Crowd_WalksOffTheMapAfterTheBattleWithoutEverBlockingKleia()
        {
            var (director, sandbox) = default((SandboxBattleDirector, GridSandboxController));
            yield return LoadSandbox(loaded => (director, sandbox) = loaded);
            var crowd = director.Crowd;
            yield return EngageAtRest(director, sandbox, CentralOpponent(director, sandbox));
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.PlayerChoosing);
            var spectators = crowd.Gathered.ToList();
            var arena = director.Arena;
            Assert.IsNotEmpty(spectators);

            director.SubmitChoice(new BattleMenuChoice(ActionId.Withdraw));
            yield return PlayUntil(director, () => director.Phase == BattleFlowPhase.Exploring);

            Assert.IsTrue(sandbox.ExplorationInputEnabled, "exploration input comes back right away");
            Assert.IsEmpty(crowd.Gathered);
            Assert.AreEqual(spectators.Count, crowd.Leaving.Count);
            foreach (var spectator in spectators)
            {
                Assert.IsTrue(DuelCrowdPlanner.IsOnSide(sandbox.Grid, spectator.Exit), $"spectator {spectator.Id} must leave through the map edge, not {spectator.Exit}");
                var seat = spectator.Seat.Seat;
                var outwardX = seat.X >= arena.Center.x ? spectator.Exit.X == sandbox.Grid.Width - 1 : spectator.Exit.X == 0;
                var outwardY = seat.Y >= arena.Center.y ? spectator.Exit.Y == sandbox.Grid.Height - 1 : spectator.Exit.Y == 0;
                Assert.IsTrue(outwardX || outwardY || !HasOutwardExit(sandbox.Grid, seat, arena), $"spectator {spectator.Id} walked across the arena to {spectator.Exit}");
            }

            var destination = QuietSeat(director, sandbox, spectators);
            Assert.IsTrue(sandbox.TryMovePlayerTo(destination), "Kleia can walk onto a cell a spectator just left");
            Assert.IsTrue(sandbox.Player.Walker.IsMoving, "Kleia starts walking at once");

            var deadline = Time.realtimeSinceStartup + DepartureTimeoutSeconds;
            while (crowd.Leaving.Count > 0 && Time.realtimeSinceStartup < deadline)
            {
                AssertNoSpectatorOccupies(sandbox.Grid, spectators);
                yield return null;
            }

            Assert.AreEqual(0, crowd.Leaving.Count, "the crowd must walk off the map");
            yield return null;
            Assert.IsTrue(spectators.All(spectator => !spectator.View), "every spectator is removed once it leaves");
            Assert.AreEqual(1, crowd.transform.childCount, "only the template stays under the crowd");
            yield return WaitFor(() => !sandbox.Player.Walker.IsMoving, StartTimeoutSeconds);
            Assert.AreEqual(destination, sandbox.Player.Walker.Cell, "no spectator stood in Kleia's way");
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase);
        }

        private static IEnumerator LoadSandbox(System.Action<(SandboxBattleDirector, GridSandboxController)> loaded)
        {
            yield return SceneManager.LoadSceneAsync(SandboxScene, LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.IsNotNull(director, "GridSandbox scene must contain the battle director");
            Assert.IsNotNull(director.Crowd, "GridSandbox scene must gather a crowd for battles");
            Assert.IsNotNull(director.Veil, "GridSandbox scene must darken the world outside the arena");
            director.ThinkSeconds = 0f;
            director.AutosaveEnabled = false;
            director.CrowdSeed = CrowdSeed;
            yield return WaitFor(() => sandbox.ProgressLoaded, StartTimeoutSeconds);
            Assert.IsTrue(sandbox.ProgressLoaded, "the map never loaded the saved progress");
            loaded((director, sandbox));
        }

        private static IEnumerator EngageAtRest(SandboxBattleDirector director, GridSandboxController sandbox, MapOpponent target)
        {
            director.enabled = false;
            sandbox.Player.Walker.Teleport(ApproachCell(sandbox.Grid, director, target), Facing.NorthEast);
            var probe = new GameObject("settle probe").AddComponent<CameraProbe>();
            probe.Watch(Camera.main.transform);
            var settleAfter = Time.realtimeSinceStartup + SettleSeconds;
            var deadline = settleAfter + SettleTimeoutSeconds;
            while ((Time.realtimeSinceStartup < settleAfter || !probe.IsStill(StillSeconds)) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Object.Destroy(probe.gameObject);
            director.enabled = true;
            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, StartTimeoutSeconds);
            Assert.AreSame(target, director.Engaged, $"{target.Opponent.Id} did not notice Kleia");
        }

        private static IEnumerator PlayUntil(SandboxBattleDirector director, System.Func<bool> done, System.Action everyFrame = null, System.Func<BattleSession, BattleMenuChoice> choose = null)
        {
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (!done() && Time.realtimeSinceStartup < deadline)
            {
                everyFrame?.Invoke();
                switch (director.Phase)
                {
                    case BattleFlowPhase.Dialogue:
                        director.AdvanceDialogue();
                        break;
                    case BattleFlowPhase.PlayerChoosing when choose != null:
                        director.SubmitChoice(choose(director.Session));
                        break;
                    case BattleFlowPhase.PlayerTargeting when choose != null:
                        director.CancelTargeting();
                        director.SubmitChoice(new BattleMenuChoice(ActionId.Withdraw));
                        break;
                }

                yield return null;
            }

            Assert.IsTrue(done(), $"the battle got stuck in {director.Phase}");
        }

        private static IEnumerator WaitUntilStill(CameraProbe probe, ExplorationCamera explorationCamera)
        {
            var deadline = Time.realtimeSinceStartup + SettleTimeoutSeconds;
            while ((explorationCamera.IsGliding || !probe.IsStill(StillSeconds)) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(explorationCamera.IsGliding, "the camera never finished gliding");
            Assert.IsTrue(probe.IsStill(StillSeconds), "the camera never settled");
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static MapOpponent CentralOpponent(SandboxBattleDirector director, GridSandboxController sandbox)
        {
            var middle = new Vector2((sandbox.Grid.Width - 1) * 0.5f, (sandbox.Grid.Height - 1) * 0.5f);
            return director.Opponents
                .Where(entry => entry.IsPlaced && entry.Opponent.Tutorial?.IsDefined != true)
                .OrderBy(entry => Vector2.Distance(middle, new Vector2(entry.Actor.Walker.Cell.X, entry.Actor.Walker.Cell.Y)))
                .First();
        }

        private static Coord ApproachCell(GridModel grid, SandboxBattleDirector director, MapOpponent entry)
        {
            var center = entry.Actor.Walker.Cell;
            for (var distance = 1; distance <= entry.Opponent.AggroRadius; distance++)
            {
                for (var dx = -distance; dx <= distance; dx++)
                {
                    var dy = distance - System.Math.Abs(dx);
                    foreach (var candidate in new[] { new Coord(center.X + dx, center.Y + dy), new Coord(center.X + dx, center.Y - dy) })
                    {
                        if (grid.TryGet(candidate, out var tile) && tile.IsPassable && !tile.IsOccupied && NoticedFirstBy(director, entry, candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }

            Assert.Fail($"no free cell where {entry.Opponent.Id} notices Kleia first");
            return center;
        }

        private static bool NoticedFirstBy(SandboxBattleDirector director, MapOpponent entry, Coord cell)
        {
            var distance = PositionalModifiers.Distance(cell, entry.Actor.Walker.Cell);
            return director.Opponents
                .Where(other => other != entry && other.IsPlaced)
                .All(other => !AggroRule.IsTriggered(cell, other.Actor.Walker.Cell, other.Opponent.AggroRadius) || PositionalModifiers.Distance(cell, other.Actor.Walker.Cell) > distance);
        }

        private static Coord QuietSeat(SandboxBattleDirector director, GridSandboxController sandbox, IEnumerable<Spectator> spectators)
        {
            var finder = new PathFinder();
            var path = new List<Coord>();
            foreach (var seat in spectators.Select(spectator => spectator.Seat.Seat))
            {
                if (finder.FindPath(sandbox.Grid, sandbox.Player.Walker.Cell, seat, path) && path.All(cell => IsQuiet(director, cell)))
                {
                    return seat;
                }
            }

            Assert.Fail("every spectator seat lies past a duelist, so walking there would start another duel");
            return default;
        }

        private static bool IsQuiet(SandboxBattleDirector director, Coord cell) =>
            director.Opponents.All(entry => !entry.IsPlaced || !AggroRule.IsTriggered(cell, entry.Actor.Walker.Cell, entry.Opponent.AggroRadius));

        private static Coord StepTowardTheCrowd(BattleState state, IReadOnlyCollection<Coord> seats)
        {
            var reachable = new List<Coord>();
            new PathFinder().Reachable(state.Grid, state.Player.Position, state.Rules.MoveRange, reachable);
            return reachable
                .Where(cell => cell != state.Player.Position)
                .OrderBy(cell => seats.Min(seat => PositionalModifiers.Distance(cell, seat)))
                .ThenBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .First();
        }

        private static bool IsReachableWithout(BattleState state, Coord seat)
        {
            var grid = DuelCrowdPlanner.CopyTerrain(state.Grid, null, true);
            var tile = grid[seat];
            tile.OccupantId = TileData.NoOccupant;
            grid.TrySet(seat, tile);
            var reachable = new List<Coord>();
            new PathFinder().Reachable(grid, state.Player.Position, state.Rules.MoveRange, reachable);
            return reachable.Contains(seat);
        }

        private static bool HasOutwardExit(GridModel grid, Coord seat, DuelArena arena)
        {
            var steps = DuelCrowdPlanner.StepsFrom(grid, seat, cell => grid[cell].IsPassable);
            for (var along = 0; along < System.Math.Max(grid.Width, grid.Height); along++)
            {
                var outward = new[]
                {
                    seat.X >= arena.Center.x ? new Coord(grid.Width - 1, along) : new Coord(0, along),
                    seat.Y >= arena.Center.y ? new Coord(along, grid.Height - 1) : new Coord(along, 0)
                };
                if (outward.Any(cell => grid.Contains(cell) && steps[cell.Y * grid.Width + cell.X] > 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertNoSpectatorOccupies(GridModel grid, IReadOnlyCollection<Spectator> spectators)
        {
            var ids = new HashSet<int>(spectators.Select(spectator => spectator.Id));
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    Assert.IsFalse(ids.Contains(grid[new Coord(x, y)].OccupantId), $"a spectator took ({x}, {y}) on the exploration grid");
                }
            }
        }

        private static List<Coord> RevealedCells(GridModel grid)
        {
            var cells = new List<Coord>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid[new Coord(x, y)].Revealed)
                    {
                        cells.Add(new Coord(x, y));
                    }
                }
            }

            return cells;
        }

        private static int CountTiles(UnityEngine.Tilemaps.Tilemap tilemap)
        {
            var count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                count += tilemap.HasTile(position) ? 1 : 0;
            }

            return count;
        }

        private static Vector2 Snap(Vector2 position) =>
            new(Mathf.Round(position.x * ArtPixelsPerUnit) / ArtPixelsPerUnit, Mathf.Round(position.y * ArtPixelsPerUnit) / ArtPixelsPerUnit);

        private static void AssertOnPixel(Vector2 expected, Vector3 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, PixelTolerance, $"{message} (x)");
            Assert.AreEqual(expected.y, actual.y, PixelTolerance, $"{message} (y)");
        }

        private static void AssertMovesOneWay(IReadOnlyList<Vector2Int> positions, string what)
        {
            foreach (var axis in new[] { 0, 1 })
            {
                var direction = 0;
                for (var index = 1; index < positions.Count; index++)
                {
                    var step = System.Math.Sign(positions[index][axis] - positions[index - 1][axis]);
                    if (step == 0)
                    {
                        continue;
                    }

                    Assert.IsTrue(direction == 0 || step == direction, $"the camera jittered on {(axis == 0 ? "x" : "y")} while {what} at frame {index}: {string.Join(", ", positions.Select(position => position[axis]))}");
                    direction = step;
                }
            }
        }

        private static void RenderIgnoringEditorImportNoise(Camera camera)
        {
            var ignoring = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                camera.Render();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoring;
            }
        }

        private static void Capture(SandboxBattleDirector director, string capturePath)
        {
            var camera = Camera.main;
            var pixelPerfect = camera.GetComponent(PixelPerfectComponent) as Behaviour;
            var brain = camera.GetComponent(BrainComponent) as Behaviour;
            var position = camera.transform.position;
            var size = camera.orthographicSize;
            var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                pixelPerfect.enabled = false;
                brain.enabled = false;
                var center = Snap(director.Arena.WorldCenter);
                camera.transform.position = new Vector3(center.x, center.y, CaptureDepth);
                camera.orthographicSize = CaptureOrthographicSize;
                camera.targetTexture = renderTexture;
                RenderIgnoringEditorImportNoise(camera);
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                texture.Apply();
                var path = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, capturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                TestContext.WriteLine($"captured {path}");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                camera.orthographicSize = size;
                camera.transform.position = position;
                pixelPerfect.enabled = true;
                brain.enabled = true;
                Object.Destroy(texture);
                Object.Destroy(renderTexture);
            }
        }

        [DefaultExecutionOrder(int.MaxValue)]
        private sealed class CameraProbe : MonoBehaviour
        {
            private readonly List<Vector2Int> positions = new();
            private Transform view;
            private float changedAt;

            public IReadOnlyList<Vector2Int> Positions => positions;

            public void Watch(Transform camera)
            {
                view = camera;
                changedAt = Time.realtimeSinceStartup;
            }

            public void Clear() => positions.Clear();

            public bool IsStill(float seconds) => positions.Count > 0 && Time.realtimeSinceStartup - changedAt >= seconds;

            private void LateUpdate()
            {
                if (!view)
                {
                    return;
                }

                var position = new Vector2Int(Mathf.RoundToInt(view.position.x * ArtPixelsPerUnit), Mathf.RoundToInt(view.position.y * ArtPixelsPerUnit));
                if (positions.Count == 0 || positions[^1] != position)
                {
                    changedAt = Time.realtimeSinceStartup;
                }

                positions.Add(position);
            }
        }
    }
}
