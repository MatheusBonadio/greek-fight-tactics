using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
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
    public sealed class AirplaneModePlayTests
    {
        private const float StartTimeoutSeconds = 5f;
        private const float SyncTimeoutSeconds = 5f;
        private const float BattleTimeoutSeconds = 90f;
        private const float BootSlackSeconds = 1f;
        private const float FastPushRetrySeconds = 0.1f;
        private const float RetryWindowSeconds = 0.6f;
        private const float OfflineThinkSeconds = 0.1f;
        private const float ThinkSlackSeconds = 1f;
        private const int MaxPlayerTurns = 40;
        private const int MaxDialogueConfirms = 64;
        private const float RealSyncTimeoutSeconds = 30f;
        private const float RealPrewarmTimeoutSeconds = 15f;
        private const float RefusedWindowSeconds = 3f;

        private IsolatedDataDirectory data;
        private FakeFirebase firebase;
        private FlakyNetwork network;
        private LoopbackHttpServer server;
        private GameObject host;

        [UnitySetUp]
        public IEnumerator ConnectTheFakeBackend()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            firebase = new FakeFirebase();
            network = new FlakyNetwork(firebase.Handle, new UnityWebRequestTransport());
            OnlineBackendOverride.Set(new OnlineBackend(network, firebase.Endpoints, firebase.Firestore, (_, _) => Task.CompletedTask));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DisconnectTheFakeBackend()
        {
            if (host)
            {
                Object.DestroyImmediate(host);
            }

            OnlineBackendOverride.Clear();
            server?.Dispose();
            server = null;
            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HangingNetwork_BootsWithinTheBudgetAndPushesTheAutosaveOnReconnect()
        {
            SeedLocalSave(new SaveData { Location = LocationScenes.Agora, Reputation = 3, Timestamp = 100 });
            network.GoDown(NetworkOutage.Hanging);

            var started = Time.realtimeSinceStartup;
            var services = CreateServices();
            yield return WaitFor(() => services.IsBooted, GameServices.OnlineBootBudgetSeconds + StartTimeoutSeconds);

            Assert.IsTrue(services.IsBooted, services.BootTask?.Exception?.ToString() ?? "boot never finished");
            Assert.LessOrEqual(Time.realtimeSinceStartup - started, GameServices.OnlineBootBudgetSeconds + BootSlackSeconds, "the boot waited on the network past its budget");
            Assert.IsNotNull(services.Saves, "the backend is configured, only unreachable");
            Assert.AreEqual(0, network.HangingCount, "the boot gives up the requests it left hanging");
            Assert.IsTrue(services.TryLoadActiveSave(out var loaded));
            Assert.AreEqual(100, loaded.Timestamp);

            services.Autosave(new SaveData { Location = LocationScenes.Agora, Reputation = 4, Timestamp = 200 });

            AssertLocalTimestamp(services, 200);
            Assert.IsTrue(services.Saves.HasPendingPush);
            Assert.IsTrue(services.IsPushing, "the push waits on the network in the background");
            Assert.IsEmpty(firebase.Commits);

            network.Reconnect();
            yield return WaitFor(() => !services.Saves.HasPendingPush, SyncTimeoutSeconds);

            Assert.IsFalse(services.Saves.HasPendingPush, "the autosave never reached the cloud after reconnecting");
            CollectionAssert.AreEqual(new[] { 200L }, firebase.Commits.Select(save => save.Timestamp));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AutosaveDuringAnOutage_IsPushedOnceTheNetworkReturns()
        {
            var services = CreateServices();
            yield return WaitFor(() => services.IsBooted, StartTimeoutSeconds);
            Assert.IsTrue(services.IsBooted);
            Assert.AreEqual(1, firebase.SignUps);

            var timestamp = 100L;
            foreach (var outage in new[] { NetworkOutage.TimedOut, NetworkOutage.Refused, NetworkOutage.ServerError, NetworkOutage.Unresolvable })
            {
                network.GoDown(outage);
                timestamp += 100;
                services.Autosave(new SaveData { Location = LocationScenes.Agora, Timestamp = timestamp });
                yield return WaitFor(() => !services.IsPushing, SyncTimeoutSeconds);

                AssertLocalTimestamp(services, timestamp);
                Assert.IsTrue(services.Saves.HasPendingPush, $"{outage} must leave the save pending");
                Assert.AreNotEqual(timestamp, firebase.RemoteSave?.Timestamp ?? 0, $"{outage} must not reach the cloud");

                network.Reconnect();
                yield return WaitFor(() => !services.Saves.HasPendingPush, SyncTimeoutSeconds);

                Assert.IsFalse(services.Saves.HasPendingPush, $"the save written during {outage} was never pushed");
                Assert.AreEqual(timestamp, firebase.RemoteSave.Timestamp);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealTransport_HostThatNeverResolves_FailsAsANetworkErrorWithoutThrowing()
        {
            var request = FlakyNetwork.AtUnresolvableHost(new HttpRequestData("POST", firebase.Endpoints.SignUpUrl, "{}", HttpRequestData.JsonContentType));
            var started = Time.realtimeSinceStartup;
            var lookup = new UnityWebRequestTransport().SendAsync(request, TimeSpan.FromSeconds(ArgumentServiceClient.TimeoutSeconds), CancellationToken.None);
            yield return WaitFor(() => lookup.IsCompleted, (float)ArgumentServiceClient.TimeoutSeconds + StartTimeoutSeconds);

            Assert.IsTrue(lookup.IsCompletedSuccessfully, lookup.Exception?.ToString() ?? "the lookup never finished");
            var response = lookup.Result;
            TestContext.WriteLine($"{request.Url}: {response.Outcome} ({response.Error}) after {Time.realtimeSinceStartup - started:0.000}s");
            Assert.AreNotEqual(HttpOutcome.Completed, response.Outcome, $"{FlakyNetwork.UnresolvableHost} is reserved and must never resolve, but this machine's DNS answered for it");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StaleSaveFromAnOfflineBoot_NeverOverwritesANewerCloudSave()
        {
            firebase.RemoteSave = new SaveData { Location = LocationScenes.Agora, Reputation = 9, Timestamp = 300 };
            SeedLocalSave(new SaveData { Location = LocationScenes.Agora, Reputation = 1, Timestamp = 100 });
            network.GoDown(NetworkOutage.Refused);

            var services = CreateServices();
            yield return WaitFor(() => services.IsBooted, StartTimeoutSeconds);
            Assert.IsTrue(services.Saves.HasPendingPush, "offline the boot keeps the local save and marks it for the next sync");

            network.Reconnect();
            yield return new WaitForSecondsRealtime(RetryWindowSeconds);

            Assert.IsEmpty(firebase.Commits, "a save the session never wrote is reconciled on the next boot, not pushed blindly");
            Assert.AreEqual(300, firebase.RemoteSave.Timestamp);

            services.Autosave(new SaveData { Location = LocationScenes.Agora, Reputation = 2, Timestamp = 400 });
            yield return WaitFor(() => !services.Saves.HasPendingPush, SyncTimeoutSeconds);

            CollectionAssert.AreEqual(new[] { 400L }, firebase.Commits.Select(save => save.Timestamp));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NetworkDropsMidSession_BattleTurnsNeverWaitAndEveryLineHasText()
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            var services = Object.FindAnyObjectByType<GameServices>();
            var dialogue = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            Assert.IsNotNull(services, "the agora must contain GameServices");
            director.ThinkSeconds = OfflineThinkSeconds;
            services.PushRetrySeconds = FastPushRetrySeconds;
            yield return WaitFor(() => sandbox.ProgressLoaded, GameServices.OnlineBootBudgetSeconds + StartTimeoutSeconds);

            var target = director.Opponents[0];
            var opponentId = target.Actor.Walker.ActorId;
            yield return WaitFor(() => director.PreparedArguments != null && director.PreparedArguments.ReadyCount(opponentId) == SandboxBattleDirector.PrewarmCount, StartTimeoutSeconds);
            Assert.IsTrue(director.PreparedArguments.IsOnline, "the map boots online through the fake backend");
            Assert.AreEqual(SandboxBattleDirector.PrewarmCount, director.PreparedArguments.ReadyCount(opponentId), "the opponent's arguments are prewarmed while online");
            yield return FinishTheTutorialOf(dialogue, target);

            network.GoDown(NetworkOutage.Hanging);
            yield return DuelChallenge.Engage(sandbox, director, target, StartTimeoutSeconds);

            var tally = new BattleTally();
            yield return PlayOfflineBattle(director, () => network.Requests.Count, tally);

            Assert.AreEqual(0, tally.LowestReady, "the arguments prewarmed online are drawn with the network down");
            Assert.Greater(tally.Fallback, 0, "after the prewarmed queue the fallback keeps the opponent talking");
            AssertTurnsNeverWaited(tally);

            Assert.IsTrue(services.TryLoadActiveSave(out var afterBattle), "the battle autosaves locally while offline");
            Assert.AreEqual(LocationScenes.Agora, afterBattle.Location);
            Assert.IsTrue(services.Saves.HasPendingPush);
            Assert.IsEmpty(firebase.Commits);

            yield return WaitFor(() => dialogue.IsStoryLoaded, StartTimeoutSeconds);
            Assert.IsTrue(dialogue.TryStartDialogue(dialogue.Talkers[0].Dialogue), "npcs still talk while offline");
            for (var confirms = 0; dialogue.IsTalking && confirms < MaxDialogueConfirms; confirms++)
            {
                AssertShowsText(dialogue.CurrentLine.Text, dialogue.DialogueBox.Body.text);
                dialogue.Advance();
                yield return null;
            }

            Assert.IsFalse(dialogue.IsTalking);

            network.Reconnect();
            yield return WaitFor(() => !services.Saves.HasPendingPush, SyncTimeoutSeconds);

            Assert.IsFalse(services.Saves.HasPendingPush, "the offline autosave never reached the cloud");
            Assert.IsTrue(services.TryLoadActiveSave(out var newest));
            Assert.AreEqual(newest.Timestamp, firebase.RemoteSave.Timestamp, "the newest local save is the one in the cloud");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealSockets_HangingServer_BootsWithinTheBudgetAndPushesTheAutosaveWhenItAnswers()
        {
            UseLoopbackServer();
            server.Hang();
            SeedLocalSave(new SaveData { Location = LocationScenes.Agora, Reputation = 3, Timestamp = 100 });

            var started = Time.realtimeSinceStartup;
            var services = CreateServices();
            yield return WaitFor(() => services.IsBooted, GameServices.OnlineBootBudgetSeconds + StartTimeoutSeconds);

            Assert.IsTrue(services.IsBooted, services.BootTask?.Exception?.ToString() ?? "boot never finished");
            Assert.LessOrEqual(Time.realtimeSinceStartup - started, GameServices.OnlineBootBudgetSeconds + BootSlackSeconds, "the boot waited on a silent server past its budget");
            Assert.Greater(server.Connections, 0, "the boot must really open a socket to the server");
            Assert.AreEqual(0, firebase.SignUps);

            services.Autosave(new SaveData { Location = LocationScenes.Agora, Reputation = 4, Timestamp = 200 });
            AssertLocalTimestamp(services, 200);
            Assert.IsTrue(services.Saves.HasPendingPush);

            server.Reconnect();
            yield return WaitFor(() => !services.Saves.HasPendingPush, RealSyncTimeoutSeconds);

            Assert.IsFalse(services.Saves.HasPendingPush, $"the autosave never reached the server: {string.Join(", ", server.Received)}");
            Assert.AreEqual(200, firebase.RemoteSave.Timestamp);
            Assert.AreEqual(1, firebase.SignUps);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealSockets_ServerDropsMidSession_TurnsNeverWaitAndTheAutosaveSyncsOnReconnect()
        {
            UseLoopbackServer();
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            var services = Object.FindAnyObjectByType<GameServices>();
            var dialogue = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            director.ThinkSeconds = OfflineThinkSeconds;
            services.PushRetrySeconds = FastPushRetrySeconds;

            yield return WaitFor(() => sandbox.ProgressLoaded, RealPrewarmTimeoutSeconds);
            var target = director.Opponents[0];
            bool EveryOpponentIsReady() => director.PreparedArguments != null && director.Opponents.Where(SandboxBattleDirector.Prewarms).All(entry => director.PreparedArguments.ReadyCount(entry.Actor.Walker.ActorId) == SandboxBattleDirector.PrewarmCount);
            yield return WaitFor(EveryOpponentIsReady, RealPrewarmTimeoutSeconds);
            Assert.IsTrue(EveryOpponentIsReady(), $"the server never answered the whole prewarm: {string.Join(", ", server.Received)}");
            Assert.AreEqual(1, firebase.SignUps);
            yield return FinishTheTutorialOf(dialogue, target);

            server.Hang();
            yield return DuelChallenge.Engage(sandbox, director, target, StartTimeoutSeconds);

            var tally = new BattleTally();
            yield return PlayOfflineBattle(director, () => server.Connections, tally);

            Assert.AreEqual(0, tally.LowestReady, "the arguments that came over the socket are drawn while the server hangs");
            Assert.Greater(tally.Fallback, 0, "after the prewarmed queue the fallback keeps the opponent talking");
            AssertTurnsNeverWaited(tally);
            Assert.IsTrue(services.TryLoadActiveSave(out _), "the battle autosaves locally while the server hangs");
            Assert.IsTrue(services.Saves.HasPendingPush);
            Assert.IsNull(firebase.RemoteSave);

            server.GoDown();
            yield return new WaitForSecondsRealtime(RefusedWindowSeconds);
            Assert.IsTrue(services.Saves.HasPendingPush, "a refused connection must leave the save pending");
            Assert.IsNull(firebase.RemoteSave);

            server.Reconnect();
            yield return WaitFor(() => !services.Saves.HasPendingPush, RealSyncTimeoutSeconds);

            Assert.IsFalse(services.Saves.HasPendingPush, $"the autosave never reached the server: {string.Join(", ", server.Received)}");
            Assert.IsTrue(services.TryLoadActiveSave(out var newest));
            Assert.AreEqual(newest.Timestamp, firebase.RemoteSave.Timestamp, "the newest local save is the one in the cloud");
            LogAssert.NoUnexpectedReceived();
        }

        private void UseLoopbackServer()
        {
            server = new LoopbackHttpServer(request => firebase.Handle(request));
            firebase = FakeFirebase.ServedAt(server.BaseUrl);
            OnlineBackendOverride.Set(new OnlineBackend(new UnityWebRequestTransport(), firebase.Endpoints, firebase.Firestore));
        }

        private static IEnumerator FinishTheTutorialOf(ExplorationDialogueDirector story, MapOpponent entry)
        {
            yield return WaitFor(() => story.IsStoryLoaded, StartTimeoutSeconds);
            Assert.IsTrue(story.IsStoryLoaded, "the story never loaded");
            var tutorial = entry.Opponent.Tutorial;
            if (tutorial.IsPending(story.Story))
            {
                Assert.IsTrue(story.Remember(new StoryEffect(tutorial.DoneFlag)), $"the lesson of {entry.Opponent.Id} could not be marked as done");
            }
        }

        private static IEnumerator PlayOfflineBattle(SandboxBattleDirector director, Func<int> requests, BattleTally tally)
        {
            Assert.IsNull(director.Tutorial, "the lesson is scripted and never draws the prewarmed arguments, so these duels come after it");
            var requestsAtStart = requests();
            var opponentName = director.Opponent.DisplayName;
            var opponentId = director.Engaged.Actor.Walker.ActorId;
            var thinking = false;
            var thinkingSince = 0f;
            var awaitingOpponentLine = false;
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                Assert.AreEqual(requestsAtStart, requests(), "a battle turn touched the network");
                tally.LowestReady = Math.Min(tally.LowestReady, director.PreparedArguments.ReadyCount(opponentId));
                if (director.Phase == BattleFlowPhase.OpponentThinking && !thinking)
                {
                    thinking = true;
                    thinkingSince = Time.realtimeSinceStartup;
                }
                else if (director.Phase != BattleFlowPhase.OpponentThinking && thinking)
                {
                    thinking = false;
                    tally.OpponentTurns++;
                    tally.LongestThinkingSeconds = Math.Max(tally.LongestThinkingSeconds, Time.realtimeSinceStartup - thinkingSince);
                    awaitingOpponentLine = true;
                }

                switch (director.Phase)
                {
                    case BattleFlowPhase.Dialogue:
                        var line = director.Hud.Dialogue.Line;
                        AssertShowsText(line.Text, director.Hud.Dialogue.Body.text);
                        if (awaitingOpponentLine && line.SpeakerName == opponentName)
                        {
                            var served = line.Text.StartsWith(FakeFirebase.ServedSpeechPrefix, StringComparison.Ordinal);
                            tally.Served += served ? 1 : 0;
                            tally.Fallback += served ? 0 : 1;
                        }

                        awaitingOpponentLine = false;
                        director.AdvanceDialogue();
                        break;
                    case BattleFlowPhase.PlayerChoosing:
                        tally.PlayerTurns++;
                        director.SubmitChoice(ChooseWhileOffline(director.Session, tally.Fallback > 0 || tally.PlayerTurns > MaxPlayerTurns));
                        break;
                    case BattleFlowPhase.PlayerTargeting:
                        if (!director.TryChooseDestination(director.Session.CirculateDestination()))
                        {
                            director.CancelTargeting();
                            director.SubmitChoice(new BattleMenuChoice(ActionId.Concede));
                        }

                        break;
                }

                yield return null;
            }

            TestContext.WriteLine($"{tally.OpponentTurns} opponent turns offline: {tally.Served} prewarmed lines, {tally.Fallback} fallback lines, longest turn {tally.LongestThinkingSeconds:0.000}s");
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "the battle did not finish in time");
        }

        private static void AssertTurnsNeverWaited(BattleTally tally)
        {
            Assert.Greater(tally.OpponentTurns, 0);
            Assert.Less(tally.LongestThinkingSeconds, OfflineThinkSeconds + ThinkSlackSeconds, "an opponent turn took longer than its think time");
            Assert.Less(OfflineThinkSeconds + ThinkSlackSeconds, (float)ArgumentServiceClient.TimeoutSeconds);
        }

        private GameServices CreateServices()
        {
            host = new GameObject(nameof(GameServices));
            host.SetActive(false);
            var services = host.AddComponent<GameServices>();
            services.Configure(null, null, null, data.Directory);
            services.PushRetrySeconds = FastPushRetrySeconds;
            host.SetActive(true);
            return services;
        }

        private void SeedLocalSave(SaveData save) =>
            new SaveSlotCatalog(Path.Combine(data.Directory, GameServices.SaveFolder)).StoreFor(SaveSlots.DefaultIndex).Save(save);

        private static void AssertLocalTimestamp(GameServices services, long timestamp)
        {
            Assert.IsTrue(services.LocalSaves.TryLoad(out var save, out var error), error);
            Assert.AreEqual(timestamp, save.Timestamp);
        }

        private static void AssertShowsText(string line, string shown)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(line), "a dialogue line is empty");
            Assert.IsFalse(line.StartsWith(UiTextSO.MissingPrefix, StringComparison.Ordinal), $"missing text key {line}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(shown), "the dialogue box shows nothing");
        }

        private static BattleMenuChoice ChooseWhileOffline(BattleSession session, bool leave)
        {
            if (leave)
            {
                return new BattleMenuChoice(ActionId.Withdraw);
            }

            return session.Menu.Entries.Any(entry => entry.Action == ActionId.Circulate && entry.IsAvailable)
                ? new BattleMenuChoice(ActionId.Circulate)
                : new BattleMenuChoice(ActionId.Concede);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private sealed class BattleTally
        {
            public int Served;
            public int Fallback;
            public int PlayerTurns;
            public int OpponentTurns;
            public float LongestThinkingSeconds;
            public int LowestReady = int.MaxValue;
        }
    }
}
