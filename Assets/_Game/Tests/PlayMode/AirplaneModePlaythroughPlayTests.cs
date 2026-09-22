using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.Grid;
using Game.Net;
using Game.Tests.Support;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class AirplaneModePlaythroughPlayTests
    {
        private const float BootSlackSeconds = 1f;
        private const float SceneTimeoutSeconds = 10f;
        private const float WalkTimeoutSeconds = 10f;
        private const float BattleTimeoutSeconds = 120f;
        private const float SyncTimeoutSeconds = 5f;
        private const float OfflineThinkSeconds = 0.1f;
        private const float ThinkSlackSeconds = 1f;
        private const int MaxDialogueConfirms = 128;
        private const int MaxApproaches = 4;
        private const int MaxRestoreShift = 2;
        private const int QuietFrames = 30;

        private static readonly NetworkOutage[] Outages = { NetworkOutage.Refused, NetworkOutage.Hanging, NetworkOutage.Unresolvable };

        private static readonly string[] BattleLinesToShow =
        {
            BattleText.QuoteKey,
            BattleText.RefuteHitKey,
            BattleText.RefuteMissKey,
            BattleText.MaieuticsHitKey,
            BattleText.MaieuticsMissKey,
            BattleText.ConcedeKey,
            BattleText.VictoryKey,
            BattleText.WithdrewKey
        };

        private readonly HashSet<string> battleLinesShown = new();
        private readonly HashSet<string> dialoguesPlayed = new();
        private readonly Dictionary<string, BattleStatus> duelResults = new();
        private IsolatedDataDirectory data;
        private FakeFirebase firebase;
        private FlakyNetwork network;
        private IReadOnlyList<string> refutableIds = Array.Empty<string>();
        private float longestOpponentTurnSeconds;
        private int opponentLines;

        private enum DuelPlan
        {
            Win,
            Explore,
            Withdraw
        }

        [UnitySetUp]
        public IEnumerator ConnectTheFakeBackend()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            firebase = new FakeFirebase();
            network = new FlakyNetwork(firebase.Handle, new UnityWebRequestTransport());
            OnlineBackendOverride.Set(new OnlineBackend(network, firebase.Endpoints, firebase.Firestore, (_, _) => Task.CompletedTask));
            battleLinesShown.Clear();
            dialoguesPlayed.Clear();
            duelResults.Clear();
            longestOpponentTurnSeconds = 0f;
            opponentLines = 0;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DisconnectTheFakeBackend()
        {
            OnlineBackendOverride.Clear();
            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EverythingThatExists_IsPlayableWithTheNetworkDownFromTheStart([ValueSource(nameof(Outages))] NetworkOutage outage)
        {
            network.GoDown(outage);
            yield return SceneManager.LoadSceneAsync(PauseMenuController.DefaultMainMenuScene, LoadSceneMode.Single);

            var menu = default(MainMenuController);
            yield return OpenMainMenu(loaded => menu = loaded);
            Assert.IsFalse(menu.Services.HasContinue, "a fresh install has nothing to continue");
            Choose(menu.MainMenu, SystemMenus.NewGame);
            PickSlot(menu.Slots, SaveSlots.DefaultIndex);
            yield return WaitForScene(LocationScenes.StartScene);

            var workshop = default(MapScene);
            yield return EnterMap(loaded => workshop = loaded);
            Assert.AreEqual(LocationScenes.Kerameikos, workshop.Sandbox.LocationId);
            yield return WalkIntoEveryTrigger(workshop);
            yield return TalkToEveryone(workshop);
            var workshopFlags = workshop.Dialogue.Triggers.Select(trigger => trigger.Flag).Distinct().ToList();
            var road = workshop.Sandbox.Exits.First(link => link.Location == LocationScenes.Agora);
            yield return WalkThroughExit(workshop, road);

            var agora = default(MapScene);
            yield return EnterMap(loaded => agora = loaded);
            Assert.AreEqual(road.Arrival, agora.Player.Cell, "the agora puts Kleia at the road's arrival");
            Assert.IsTrue(agora.Services.TryLoadActiveSave(out var arrival), "arriving autosaves while offline");
            Assert.AreEqual(LocationScenes.Agora, arrival.Location);
            CollectionAssert.IsSubsetOf(workshopFlags, arrival.Flags.Keys, "the workshop's story survives the trip");
            refutableIds = agora.Services.Catalog.RefutableIds.ToList();
            yield return TalkToEveryone(agora);
            yield return DuelEveryone(agora);

            AssertEveryBattleLineWasShown();
            Assert.Greater(opponentLines, 0);
            Assert.Less(longestOpponentTurnSeconds, OfflineThinkSeconds + ThinkSlackSeconds, "an opponent turn took longer than its think time");
            Assert.IsEmpty(firebase.Commits, "nothing reaches the cloud while the network is down");

            yield return SaveFromThePauseMenuAndQuit(agora);
            yield return WaitForScene(PauseMenuController.DefaultMainMenuScene);

            yield return OpenMainMenu(loaded => menu = loaded);
            Assert.IsTrue(menu.Services.HasContinue, "the offline save is there to continue");
            Assert.IsTrue(menu.Services.TryLoadActiveSave(out var quitSave));
            Choose(menu.MainMenu, SystemMenus.Continue);
            PickSlot(menu.Slots, SaveSlots.DefaultIndex);
            yield return WaitForScene(LocationScenes.SceneFor(quitSave.Location));

            yield return EnterMap(loaded => agora = loaded);
            AssertRestored(agora, quitSave);

            network.Reconnect();
            var home = agora.Sandbox.Exits.First(link => link.Location == LocationScenes.Kerameikos);
            yield return WalkThroughExit(agora, home);
            yield return EnterMap(loaded => workshop = loaded);
            Assert.AreEqual(home.Arrival, workshop.Player.Cell);
            yield return WaitFor(() => !workshop.Services.Saves.HasPendingPush && !workshop.Services.IsPushing, SyncTimeoutSeconds);

            Assert.IsFalse(workshop.Services.Saves.HasPendingPush, "the offline progress never reached the cloud after reconnecting");
            Assert.IsTrue(workshop.Services.TryLoadActiveSave(out var newest));
            Assert.AreEqual(newest.Timestamp, firebase.RemoteSave.Timestamp, "the newest local save is the one in the cloud");
            CollectionAssert.IsOrdered(firebase.Commits.Select(save => save.Timestamp), "an older save overwrote a newer one in the cloud");
            CollectionAssert.IsSupersetOf(firebase.RemoteSave.Duels.Keys, duelResults.Where(result => result.Value == BattleStatus.PlayerWon).Select(result => result.Key));
            TestContext.WriteLine($"{outage}: {dialoguesPlayed.Count} conversations, {duelResults.Count} duels, {opponentLines} opponent lines, longest opponent turn {longestOpponentTurnSeconds:0.000}s");
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator OpenMainMenu(Action<MainMenuController> loaded)
        {
            var started = Time.realtimeSinceStartup;
            var controller = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(controller, "the main menu scene must contain its controller");
            yield return WaitFor(() => controller.IsReady, GameServices.OnlineBootBudgetSeconds + SceneTimeoutSeconds);

            Assert.IsTrue(controller.IsReady, "the main menu never opened");
            Assert.LessOrEqual(Time.realtimeSinceStartup - started, GameServices.OnlineBootBudgetSeconds + BootSlackSeconds, "the main menu waited on the network past the boot budget");
            loaded(controller);
        }

        private IEnumerator EnterMap(Action<MapScene> loaded)
        {
            var started = Time.realtimeSinceStartup;
            yield return null;
            var map = new MapScene(
                Object.FindAnyObjectByType<GridSandboxController>(),
                Object.FindAnyObjectByType<GameServices>(),
                Object.FindAnyObjectByType<ExplorationDialogueDirector>(),
                Object.FindAnyObjectByType<SandboxBattleDirector>(),
                Object.FindAnyObjectByType<PauseMenuController>());
            Assert.IsNotNull(map.Sandbox);
            Assert.IsNotNull(map.Services, $"{map.Sandbox.LocationId} must contain GameServices");
            Assert.IsNotNull(map.Dialogue, $"{map.Sandbox.LocationId} must contain the dialogue director");
            Assert.IsNotNull(map.Pause, $"{map.Sandbox.LocationId} must contain the pause menu");
            if (map.Battle)
            {
                map.Battle.ThinkSeconds = OfflineThinkSeconds;
            }

            yield return WaitFor(() => map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Dialogue.IsStoryLoaded, GameServices.OnlineBootBudgetSeconds + SceneTimeoutSeconds);
            Assert.IsTrue(map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Dialogue.IsStoryLoaded, $"{map.Sandbox.LocationId} never finished loading");
            Assert.LessOrEqual(Time.realtimeSinceStartup - started, GameServices.OnlineBootBudgetSeconds + BootSlackSeconds, $"{map.Sandbox.LocationId} waited on the network past the boot budget");
            loaded(map);
        }

        private IEnumerator WalkIntoEveryTrigger(MapScene map)
        {
            foreach (var flag in map.Dialogue.Triggers.Select(trigger => trigger.Flag).Distinct().ToList())
            {
                if (StoryCondition.Has(flag).IsMet(map.Dialogue.Story))
                {
                    continue;
                }

                var trigger = default(DialogueTriggerLink);
                foreach (var candidate in map.Dialogue.Triggers.Where(candidate => candidate.Flag == flag))
                {
                    if (map.Sandbox.TryMovePlayerTo(candidate.Cell))
                    {
                        trigger = candidate;
                        break;
                    }
                }

                Assert.IsNotNull(trigger, $"no trigger for {flag} can be reached on foot");
                yield return WaitFor(() => map.Dialogue.IsTalking, WalkTimeoutSeconds);
                Assert.IsTrue(map.Dialogue.IsTalking, $"walking toward {trigger.Cell} never played {trigger.Dialogue}");
                Assert.AreEqual(trigger.Dialogue, map.Dialogue.ActiveDialogueId);
                yield return PlayConversation(map);
            }
        }

        private IEnumerator TalkToEveryone(MapScene map)
        {
            Assert.IsNotEmpty(map.Dialogue.Talkers, $"{map.Sandbox.LocationId} has nobody to talk to");
            foreach (var talker in map.Dialogue.Talkers)
            {
                var npc = talker.Actor.Walker.Cell;
                map.Sandbox.Player.Walker.Teleport(QuietFreeNeighbour(map, npc), Facing.NorthEast);
                yield return null;

                map.Sandbox.Cursor.TryHover(IsoMath.GridToWorld(npc), Vector2.zero);
                map.Sandbox.ConfirmCursor();
                yield return WaitFor(() => map.Dialogue.IsTalking, WalkTimeoutSeconds);
                Assert.IsTrue(map.Dialogue.IsTalking, $"{talker.Dialogue} never started while offline");
                Assert.AreEqual(talker.Dialogue, map.Dialogue.ActiveDialogueId);
                yield return PlayConversation(map);
            }
        }

        private IEnumerator PlayConversation(MapScene map)
        {
            dialoguesPlayed.Add(map.Dialogue.ActiveDialogueId);
            for (var confirms = 0; map.Dialogue.IsTalking && confirms < MaxDialogueConfirms; confirms++)
            {
                var line = map.Dialogue.CurrentLine;
                AssertShowsText(line.Text, map.Dialogue.DialogueBox.Body.text);
                if (!line.IsNarration)
                {
                    AssertShowsText(map.Dialogue.DialogueBox.Line.SpeakerName, map.Dialogue.DialogueBox.SpeakerLabel.text);
                }

                map.Dialogue.Advance();
                yield return null;
            }

            Assert.IsFalse(map.Dialogue.IsTalking, "a conversation never closed");
            yield return null;
        }

        private static IEnumerator WalkThroughExit(MapScene map, MapExitLink exit)
        {
            var player = map.Sandbox.Player.Walker;
            var beside = Neighbours(exit.Cell)
                .First(cell => map.Sandbox.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == player.ActorId)
                    && map.Sandbox.Exits.All(link => link.Cell != cell) && IsQuiet(map, cell));
            player.Teleport(beside, Facing.SouthWest);
            yield return null;

            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(exit.Cell), $"no path onto the exit to {exit.Location}");
            yield return WaitForScene(LocationScenes.SceneFor(exit.Location));
        }

        private IEnumerator DuelEveryone(MapScene map)
        {
            var battle = map.Battle;
            Assert.IsNotNull(battle, "the agora hosts the duels");
            var winner = battle.Opponents
                .Where(entry => entry.Opponent.Profile == Profile.Sophist)
                .OrderBy(entry => entry.Opponent.MaxCoherence)
                .First();
            var quitter = battle.Opponents.Last(entry => entry != winner);
            var explorers = battle.Opponents.Where(entry => entry != winner && entry != quitter).ToList();

            yield return ProvokeByWalkingIn(map, winner);
            yield return PlayDuel(map, DuelPlan.Win);
            Assert.AreEqual(BattleStatus.PlayerWon, duelResults[winner.Opponent.Id], $"naming the right fallacy must beat {winner.Opponent.Id} offline");
            map.Player.Teleport(InsideTheRadiusOf(map, winner), Facing.NorthEast);
            yield return Frames(QuietFrames);
            Assert.AreEqual(BattleFlowPhase.Exploring, battle.Phase, $"{winner.Opponent.Id} picked a fight after losing");

            yield return ProvokeByWalkingIn(map, quitter);
            yield return PlayDuel(map, DuelPlan.Withdraw);
            Assert.AreEqual(BattleStatus.PlayerWithdrew, duelResults[quitter.Opponent.Id]);
            yield return ProvokeByWalkingIn(map, quitter);
            yield return PlayDuel(map, DuelPlan.Withdraw);

            foreach (var entry in explorers)
            {
                yield return Provoke(map, entry);
                yield return PlayDuel(map, DuelPlan.Explore);
            }

            Assert.AreEqual(battle.Opponents.Count, duelResults.Count, "every opponent on the map must be fought");
        }

        private static IEnumerator ProvokeByWalkingIn(MapScene map, MapOpponent entry)
        {
            Assert.IsTrue(TryFindCellNoticedFirstBy(map, entry, out var cell), $"no free cell where {entry.Opponent.Id} is the nearest to notice Kleia");
            map.Player.Teleport(cell, Facing.NorthEast);
            yield return WaitFor(() => map.Battle.Phase != BattleFlowPhase.Exploring, SceneTimeoutSeconds);
            Assert.AreSame(entry, map.Battle.Engaged, $"walking up to {entry.Opponent.Id} did not start the duel");
        }

        private static IEnumerator Provoke(MapScene map, MapOpponent entry)
        {
            if (TryFindCellNoticedFirstBy(map, entry, out _))
            {
                yield return ProvokeByWalkingIn(map, entry);
                yield break;
            }

            TestContext.WriteLine($"the duels moved everyone around {entry.Opponent.Id}, so the duel starts from the nearest free cell");
            map.Player.Teleport(NearestFreeCell(map, entry.Actor.Walker.Cell), Facing.NorthEast);
            map.Battle.StartBattle(entry);
            yield return WaitFor(() => map.Battle.Phase != BattleFlowPhase.Exploring, SceneTimeoutSeconds);
            Assert.AreSame(entry, map.Battle.Engaged, $"the duel against {entry.Opponent.Id} never started");
        }

        private IEnumerator PlayDuel(MapScene map, DuelPlan plan)
        {
            var director = map.Battle;
            var opponent = director.Engaged;
            var argumentRequests = network.RequestsTo(firebase.Endpoints.ArgumentUrl);
            var approaches = 0;
            var playerEntry = -1;
            var thinking = false;
            var thinkingSince = 0f;
            var awaitingOpponentLine = false;
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                Assert.AreEqual(argumentRequests, network.RequestsTo(firebase.Endpoints.ArgumentUrl), "a battle turn asked the server for an argument");
                if (director.Phase == BattleFlowPhase.OpponentThinking && !thinking)
                {
                    thinking = true;
                    thinkingSince = Time.realtimeSinceStartup;
                }
                else if (director.Phase != BattleFlowPhase.OpponentThinking && thinking)
                {
                    thinking = false;
                    longestOpponentTurnSeconds = Math.Max(longestOpponentTurnSeconds, Time.realtimeSinceStartup - thinkingSince);
                    awaitingOpponentLine = true;
                }

                switch (director.Phase)
                {
                    case BattleFlowPhase.Dialogue:
                        ReadBattleLine(director, opponent, awaitingOpponentLine, playerEntry);
                        awaitingOpponentLine = false;
                        playerEntry = -1;
                        director.AdvanceDialogue();
                        break;
                    case BattleFlowPhase.PlayerChoosing:
                        playerEntry = director.Session.State.Log.Count;
                        director.SubmitChoice(ChooseOffline(director.Session, plan, ref approaches));
                        break;
                    case BattleFlowPhase.PlayerTargeting:
                        if (!director.TryChooseDestination(director.Session.CirculateDestination()))
                        {
                            director.CancelTargeting();
                            playerEntry = director.Session.State.Log.Count;
                            director.SubmitChoice(new BattleMenuChoice(ActionId.Concede));
                        }

                        break;
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, $"the duel against {opponent.Opponent.Id} did not finish in time");
            duelResults[opponent.Opponent.Id] = director.LastResult;
            battleLinesShown.Add(BattleText.StatusKey(director.LastResult));
            map.Player.Teleport(QuietCell(map), Facing.NorthEast);
            yield return Frames(QuietFrames);
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "stepping away after a duel must not start another one");
        }

        private void ReadBattleLine(SandboxBattleDirector director, MapOpponent opponent, bool opponentJustPlayed, int playerEntry)
        {
            var box = director.Hud.Dialogue;
            var line = box.Line;
            AssertShowsText(line.Text, box.Body.text);
            if (opponentJustPlayed && line.SpeakerName == opponent.Opponent.DisplayName)
            {
                opponentLines++;
                Assert.IsNotNull(line.Portrait, $"{opponent.Opponent.Id} argued without a portrait");
                Assert.IsFalse(line.Text.StartsWith(FakeFirebase.ServedSpeechPrefix, StringComparison.Ordinal), "the network was down from the start, so every argument comes from the fallback");
            }

            var state = director.Session.State;
            if (playerEntry < 0 || playerEntry >= state.Log.Count)
            {
                return;
            }

            var entry = state.Log[playerEntry];
            var key = entry.Rejection == RejectionReason.None ? OutcomeLineKey(entry) : null;
            if (key != null)
            {
                Assert.AreEqual(string.Empty, line.SpeakerName, $"{key} is narrated");
                battleLinesShown.Add(key);
            }
        }

        private BattleMenuChoice ChooseOffline(BattleSession session, DuelPlan plan, ref int approaches)
        {
            bool Available(ActionId action) => session.Menu.Entries.Any(entry => entry.Action == action && entry.IsAvailable);
            bool Missing(string key) => !battleLinesShown.Contains(key);

            if (plan == DuelPlan.Withdraw)
            {
                return new BattleMenuChoice(ActionId.Withdraw);
            }

            if (Missing(BattleText.QuoteKey) && Available(ActionId.Quote))
            {
                return new BattleMenuChoice(ActionId.Quote);
            }

            var argument = session.State.CurrentArgument;
            if (plan == DuelPlan.Explore && argument.HasValue)
            {
                var presented = argument.Value;
                if (Missing(BattleText.RefuteMissKey) && presented.IsRefutable && Available(ActionId.Refute))
                {
                    return new BattleMenuChoice(ActionId.Refute, refutableIds.First(id => id != presented.FallacyId));
                }

                if (Missing(BattleText.MaieuticsMissKey) && presented.Premises.Count > 1 && Available(ActionId.Maieutics))
                {
                    return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: (presented.WeakPremise + 1) % presented.Premises.Count);
                }

                if (Missing(BattleText.MaieuticsHitKey) && Available(ActionId.Maieutics))
                {
                    return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: presented.WeakPremise);
                }

                if ((Missing(BattleText.MaieuticsHitKey) || Missing(BattleText.MaieuticsMissKey)) && approaches < MaxApproaches && Available(ActionId.Circulate))
                {
                    approaches++;
                    return new BattleMenuChoice(ActionId.Circulate);
                }

                if (Missing(BattleText.ConcedeKey) && Available(ActionId.Concede))
                {
                    return new BattleMenuChoice(ActionId.Concede);
                }
            }

            if (argument.HasValue && argument.Value.IsRefutable && Available(ActionId.Refute))
            {
                return new BattleMenuChoice(ActionId.Refute, argument.Value.FallacyId);
            }

            if (argument.HasValue && Available(ActionId.Maieutics))
            {
                return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: argument.Value.WeakPremise);
            }

            return Available(ActionId.Circulate) ? new BattleMenuChoice(ActionId.Circulate) : new BattleMenuChoice(ActionId.Concede);
        }

        private IEnumerator SaveFromThePauseMenuAndQuit(MapScene map)
        {
            var pause = map.Pause;
            var services = map.Services;
            Assert.AreSame(pause, pause.Input.Panel, "exploration hands the input back to the pause menu");
            pause.Input.Panel.HandleCancel();
            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            Choose(pause.PauseMenu, SystemMenus.Compendium);
            Assert.AreSame(pause.Compendium, pause.Input.Panel);
            Assert.IsNotNull(pause.Compendium.SelectedId, "the fallacies refuted offline show up in the compendium");
            pause.Compendium.HandleCancel();

            Choose(pause.PauseMenu, SystemMenus.Options);
            var volume = services.Settings.VolumeStep;
            var louder = volume < GameSettings.VolumeSteps;
            pause.Options.HandleNavigate(louder ? Vector2.right : Vector2.left);
            Assert.AreNotEqual(volume, services.Settings.VolumeStep, "the options still apply offline");
            pause.Options.HandleNavigate(louder ? Vector2.left : Vector2.right);
            pause.Options.HandleCancel();

            Assert.IsTrue(services.TryLoadActiveSave(out var beforeSaving));
            Choose(pause.PauseMenu, SystemMenus.Save);
            PickSlot(pause.Slots, SaveSlots.DefaultIndex);
            Assert.IsTrue(services.TryLoadActiveSave(out var saved));
            Assert.Greater(saved.Timestamp, beforeSaving.Timestamp, "SALVAR must write the slot while offline");
            Assert.AreEqual(map.Player.Cell, saved.Position);
            Assert.AreSame(pause.PauseMenu, pause.Input.Panel);
            Assert.IsEmpty(firebase.Commits);

            Choose(pause.PauseMenu, SystemMenus.MainMenu);
            yield return null;
        }

        private void AssertRestored(MapScene map, SaveData save)
        {
            var standing = map.Player.Cell;
            var savedCellHolder = map.Sandbox.Grid[save.Position].OccupantId;
            if (savedCellHolder == map.Player.ActorId)
            {
                Assert.AreEqual(save.Position, standing, "CONTINUAR must put Kleia back where she was");
            }
            else
            {
                Assert.LessOrEqual(PositionalModifiers.Distance(save.Position, standing), MaxRestoreShift, "CONTINUAR must put Kleia next to the saved cell another actor holds");
            }

            foreach (var won in duelResults.Where(result => result.Value == BattleStatus.PlayerWon).Select(result => result.Key))
            {
                var entry = map.Battle.Opponents.Single(candidate => candidate.Opponent.Id == won);
                Assert.IsTrue(map.Battle.IsDefeated(entry), $"CONTINUAR forgot the duel won against {won}");
            }
        }

        private void AssertEveryBattleLineWasShown()
        {
            var missing = BattleLinesToShow.Where(key => !battleLinesShown.Contains(key)).ToList();
            Assert.IsEmpty(missing, $"these battle lines never showed up offline: {string.Join(", ", missing)}");
        }

        private static string OutcomeLineKey(BattleLogEntry entry) => entry.Action switch
        {
            ActionId.Refute => entry.Hit ? BattleText.RefuteHitKey : BattleText.RefuteMissKey,
            ActionId.Maieutics => entry.Hit ? BattleText.MaieuticsHitKey : BattleText.MaieuticsMissKey,
            ActionId.Concede => BattleText.ConcedeKey,
            ActionId.Quote => BattleText.QuoteKey,
            _ => null
        };

        private static void Choose(MenuListView menu, string id)
        {
            var row = -1;
            for (var index = 0; index < menu.Model.Entries.Count; index++)
            {
                row = menu.Model.Entries[index].Id == id ? index : row;
            }

            Assert.GreaterOrEqual(row, 0, $"{id} is not on the menu");
            Assert.IsTrue(menu.Model.Entries[row].Enabled, $"{id} is disabled");
            menu.HandlePoint(row);
            menu.HandleConfirm();
        }

        private static void PickSlot(SaveSlotsView slots, int index)
        {
            Assert.IsTrue(slots.gameObject.activeSelf, "the slot list never opened");
            slots.HandlePoint(index);
            slots.HandleConfirm();
            if (slots.Model.Step != SaveSlotsStep.ConfirmingOverwrite)
            {
                return;
            }

            slots.HandlePoint(SaveSlotsModel.ConfirmYes);
            slots.HandleConfirm();
        }

        private static void AssertShowsText(string line, string shown)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(line), "a line is empty");
            Assert.IsFalse(line.StartsWith(UiTextSO.MissingPrefix, StringComparison.Ordinal), $"missing text key {line}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(shown), "the box shows nothing");
        }

        private static Coord QuietFreeNeighbour(MapScene map, Coord npc)
        {
            foreach (var cell in Neighbours(npc))
            {
                if (map.Sandbox.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == map.Player.ActorId)
                    && map.Sandbox.Exits.All(link => link.Cell != cell) && map.Dialogue.Triggers.All(trigger => trigger.Cell != cell) && IsQuiet(map, cell))
                {
                    return cell;
                }
            }

            Assert.Fail($"nobody can stand beside {npc} without being noticed");
            return npc;
        }

        private static bool IsQuiet(MapScene map, Coord cell) =>
            !map.Battle || map.Battle.Opponents.All(entry => map.Battle.IsDefeated(entry) || !AggroRule.IsTriggered(cell, entry.Actor.Walker.Cell, entry.Opponent.AggroRadius));

        private static bool TryFindCellNoticedFirstBy(MapScene map, MapOpponent entry, out Coord found)
        {
            foreach (var cell in CellsAround(map, entry.Actor.Walker.Cell, entry.Opponent.AggroRadius))
            {
                var distance = PositionalModifiers.Distance(cell, entry.Actor.Walker.Cell);
                var nearestToNotice = map.Battle.Opponents
                    .Where(other => other != entry && !map.Battle.IsDefeated(other))
                    .All(other => !AggroRule.IsTriggered(cell, other.Actor.Walker.Cell, other.Opponent.AggroRadius) || PositionalModifiers.Distance(cell, other.Actor.Walker.Cell) > distance);
                if (nearestToNotice)
                {
                    found = cell;
                    return true;
                }
            }

            found = default;
            return false;
        }

        private static Coord InsideTheRadiusOf(MapScene map, MapOpponent entry) =>
            FirstCellAround(map, entry.Actor.Walker.Cell, entry.Opponent.AggroRadius, cell => IsQuiet(map, cell), $"no free cell inside the radius of {entry.Opponent.Id} is out of the others' reach");

        private static Coord NearestFreeCell(MapScene map, Coord center) =>
            FirstCellAround(map, center, map.Sandbox.Grid.Width + map.Sandbox.Grid.Height, _ => true, $"no free cell anywhere near {center}");

        private static Coord QuietCell(MapScene map) =>
            FirstCellAround(map, map.Player.Cell, map.Sandbox.Grid.Width + map.Sandbox.Grid.Height, cell => IsQuiet(map, cell), "no free cell is out of every opponent's reach");

        private static Coord FirstCellAround(MapScene map, Coord center, int farthest, Func<Coord, bool> accepts, string failure)
        {
            foreach (var cell in CellsAround(map, center, farthest).Where(accepts))
            {
                return cell;
            }

            Assert.Fail(failure);
            return center;
        }

        private static IEnumerable<Coord> CellsAround(MapScene map, Coord center, int farthest)
        {
            for (var distance = 1; distance <= farthest; distance++)
            {
                for (var y = 0; y < map.Sandbox.Grid.Height; y++)
                {
                    for (var x = 0; x < map.Sandbox.Grid.Width; x++)
                    {
                        var cell = new Coord(x, y);
                        if (PositionalModifiers.Distance(cell, center) == distance && IsFreeFor(map, cell))
                        {
                            yield return cell;
                        }
                    }
                }
            }
        }

        private static bool IsFreeFor(MapScene map, Coord cell) =>
            map.Sandbox.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == map.Player.ActorId)
            && map.Sandbox.Exits.All(link => link.Cell != cell);

        private static IEnumerable<Coord> Neighbours(Coord cell) => new[]
        {
            new Coord(cell.X + 1, cell.Y),
            new Coord(cell.X - 1, cell.Y),
            new Coord(cell.X, cell.Y + 1),
            new Coord(cell.X, cell.Y - 1)
        };

        private static IEnumerator WaitForScene(string scene)
        {
            yield return WaitFor(() => SceneManager.GetActiveScene().name == scene, SceneTimeoutSeconds);
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name, $"{scene} never loaded");
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private sealed class MapScene
        {
            public MapScene(GridSandboxController sandbox, GameServices services, ExplorationDialogueDirector dialogue, SandboxBattleDirector battle, PauseMenuController pause)
            {
                Sandbox = sandbox;
                Services = services;
                Dialogue = dialogue;
                Battle = battle;
                Pause = pause;
            }

            public GridSandboxController Sandbox { get; }

            public GameServices Services { get; }

            public ExplorationDialogueDirector Dialogue { get; }

            public SandboxBattleDirector Battle { get; }

            public PauseMenuController Pause { get; }

            public GridWalker Player => Sandbox.Player.Walker;
        }
    }
}
