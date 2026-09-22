using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Net;
using Game.Tests.EditMode.Battle;
using Game.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class AirplaneModeTests
    {
        private const int Seed = 402;
        private const int PrewarmCount = 3;
        private const int FallbackDraws = 6;
        private const string OpponentKey = "thrasybios";

        private string directory;
        private SynchronizationContext previousContext;
        private FakeFirebase firebase;
        private FlakyNetwork network;
        private FlakyIdTokenProvider tokens;
        private RecordingDelay delay;
        private FallacyCatalogSO catalog;
        private FallbackLibrarySO fallbackLibrary;
        private OpponentSO opponent;
        private ArgumentValidator validator;

        [SetUp]
        public void SetUp()
        {
            previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            directory = Path.Combine(Path.GetTempPath(), $"gft-airplane-{Guid.NewGuid():N}");
            firebase = new FakeFirebase();
            network = new FlakyNetwork(firebase.Handle);
            tokens = new FlakyIdTokenProvider(network);
            delay = new RecordingDelay();
            catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            fallbackLibrary = AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(DataTools.FallbackLibraryPath);
            opponent = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(OpponentKey));
            Assert.IsNotNull(catalog, "run DataTools.Regenerate first");
            Assert.IsNotNull(fallbackLibrary, "run DataTools.Regenerate first");
            Assert.IsNotNull(opponent, "run DataTools.Regenerate first");
            validator = new ArgumentValidator(catalog.RefutableIds);
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PrewarmedArguments_AreStillServedAfterTheNetworkDrops()
        {
            var source = CreateSource();
            Assert.AreEqual(PrewarmCount, Run(source.PrewarmAsync(BattleFactory.OpponentId, PrewarmCount, CancellationToken.None)));

            network.GoDown(NetworkOutage.Refused);
            var requestsAtDrop = network.Requests.Count;
            var drawn = Draw(source, PrewarmCount + FallbackDraws);

            Assert.IsTrue(drawn.Take(PrewarmCount).All(argument => FakeFirebase.WasServed(argument)), "the queue filled online is served first");
            Assert.IsTrue(drawn.Skip(PrewarmCount).All(argument => !FakeFirebase.WasServed(argument)), "once the queue is empty the fallback takes over");
            Assert.AreEqual(requestsAtDrop, network.Requests.Count, "drawing an argument never touches the network");
        }

        [TestCase(NetworkOutage.TimedOut)]
        [TestCase(NetworkOutage.Refused)]
        [TestCase(NetworkOutage.ServerError)]
        public void PrewarmDuringAnOutage_TriesOnceMoreThenFallsBackAndRefillsOnReconnect(NetworkOutage outage)
        {
            var source = CreateSource();
            network.GoDown(outage);

            Assert.AreEqual(0, Run(source.PrewarmAsync(BattleFactory.OpponentId, PrewarmCount, CancellationToken.None)));
            Assert.AreEqual(ArgumentServiceClient.MaxAttempts, network.RequestsTo(firebase.Endpoints.ArgumentUrl), "one request and one retry, then the prewarm stops");
            CollectionAssert.AreEqual(Enumerable.Repeat(TimeSpan.FromSeconds(ArgumentServiceClient.TimeoutSeconds), ArgumentServiceClient.MaxAttempts), network.Timeouts);
            CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(ArgumentServiceClient.BackoffSeconds) }, delay.Delays);

            var requestsBeforeTheTurns = network.Requests.Count;
            Assert.IsTrue(Draw(source, FallbackDraws).All(argument => !FakeFirebase.WasServed(argument)));
            Assert.AreEqual(requestsBeforeTheTurns, network.Requests.Count);

            network.Reconnect();
            Assert.AreEqual(PrewarmCount, Run(source.PrewarmAsync(BattleFactory.OpponentId, PrewarmCount, CancellationToken.None)));
            Assert.IsTrue(Draw(source, PrewarmCount).All(argument => FakeFirebase.WasServed(argument)), "arguments come from the network again after reconnecting");
        }

        [Test]
        public void ExpiredTokenDuringAnOutage_SkipsTheRequestAndFallsBack()
        {
            tokens.HasCachedToken = false;
            var source = CreateSource();
            network.GoDown(NetworkOutage.Refused);

            Assert.AreEqual(0, Run(source.PrewarmAsync(BattleFactory.OpponentId, PrewarmCount, CancellationToken.None)));
            Assert.AreEqual(0, network.RequestsTo(firebase.Endpoints.ArgumentUrl), "without an id token the argument request is never sent");
            Assert.IsTrue(Draw(source, FallbackDraws).All(argument => !FakeFirebase.WasServed(argument)));

            network.Reconnect();
            Assert.AreEqual(PrewarmCount, Run(source.PrewarmAsync(BattleFactory.OpponentId, PrewarmCount, CancellationToken.None)));
        }

        [Test]
        public void HangingNetwork_NeverHoldsABattleTurn()
        {
            var source = CreateSource();
            network.GoDown(NetworkOutage.Hanging);
            var prewarm = source.PrewarmAsync(BattleFactory.OpponentId, PrewarmCount, CancellationToken.None);
            Assert.IsFalse(prewarm.IsCompleted, "the fake network holds the request until it reconnects");
            Assert.AreEqual(1, network.HangingCount);

            var recorder = new RecordingArgumentSource(source);
            var state = BattleFactory.Create(playerInitiative: 5, opponentInitiative: 10);
            var clock = Stopwatch.StartNew();
            var result = BattleSimulator.Run(state, new StrategicPlayerPolicy(), new OpponentBrain(new Random(Seed)), recorder);
            clock.Stop();

            Assert.AreNotEqual(BattleStatus.Ongoing, result.Status);
            Assert.IsNotEmpty(recorder.Served, "the opponent argued during the outage");
            Assert.IsTrue(recorder.Served.All(argument => validator.IsValid(argument) && !FakeFirebase.WasServed(argument)));
            Assert.Less(clock.Elapsed.TotalSeconds, ArgumentServiceClient.TimeoutSeconds, "a whole battle ran in less time than a single request timeout");
            Assert.AreEqual(1, network.Requests.Count, "the turns never sent a request");
            Assert.IsFalse(prewarm.IsCompleted, "the battle finished while the prewarm was still stuck on the network");

            network.Reconnect();

            Assert.AreEqual(PrewarmCount, Run(prewarm), "after reconnecting the stuck request times out, is retried and the queue fills");
            Assert.AreEqual(PrewarmCount, source.ReadyCount(BattleFactory.OpponentId));
        }

        [Test]
        public void EveryOpponent_ArguesOfflineWithLinesThatAreNeverEmpty()
        {
            foreach (var id in DataTools.OpponentIds())
            {
                var entry = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(id));
                var pool = entry.FallacyPool.Select(fallacy => fallacy.Id).ToList();
                var offline = new PrewarmedArgumentSource(null, new ArgumentDiskCache(directory), validator, new FallbackArgumentLibrary(fallbackLibrary, validator, Seed), Seed);
                offline.Register(BattleFactory.OpponentId, OpponentContext.From(entry));

                foreach (var argument in Draw(offline, pool.Count * FallbackDraws))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(argument.Speech), id);
                    Assert.IsTrue(validator.IsValid(argument), $"{id}: {argument.Speech}");
                    CollectionAssert.Contains(pool, argument.FallacyId, id);
                }
            }
        }

        [TestCase(NetworkOutage.TimedOut)]
        [TestCase(NetworkOutage.Refused)]
        [TestCase(NetworkOutage.ServerError)]
        [TestCase(NetworkOutage.Hanging)]
        public void OfflineAutosaves_StayLocalAndOnlyTheNewestIsPushedOnReconnect(NetworkOutage outage)
        {
            var store = new LocalSaveStore(directory);
            var synchronizer = CreateSynchronizer(store);
            Assert.IsNull(Run(synchronizer.ReconcileAsync(CancellationToken.None)));

            network.GoDown(outage);
            synchronizer.SaveLocal(SampleSave(timestamp: 100));
            AssertLocalTimestamp(store, 100);

            var firstPush = synchronizer.PushPendingAsync(CancellationToken.None);
            if (outage == NetworkOutage.Hanging)
            {
                Assert.IsFalse(firstPush.IsCompleted);
            }
            else
            {
                Assert.IsFalse(Run(firstPush));
                Assert.AreEqual(SaveSynchronizer.MaxPushAttempts, network.RequestsTo(firebase.Firestore.CommitUrl));
                Assert.AreEqual(RemoteSaveStatus.Unavailable, synchronizer.LastRemoteStatus);
            }

            synchronizer.SaveLocal(SampleSave(timestamp: 200));
            AssertLocalTimestamp(store, 200);
            Assert.IsTrue(synchronizer.HasPendingPush);
            Assert.IsEmpty(firebase.Commits, "nothing reaches the cloud while offline");

            network.Reconnect();
            var pushed = outage == NetworkOutage.Hanging ? Run(firstPush) : Run(synchronizer.PushPendingAsync(CancellationToken.None));

            Assert.IsTrue(pushed);
            Assert.IsFalse(synchronizer.HasPendingPush);
            CollectionAssert.AreEqual(new[] { 200L }, firebase.Commits.Select(save => save.Timestamp), "only the newest save is uploaded");
            AssertLocalTimestamp(store, 200);
        }

        [TestCase(300, 200)]
        [TestCase(200, 300)]
        public void ReconnectingAfterAnOfflineBoot_KeepsTheNewestSave(long localTimestamp, long remoteTimestamp)
        {
            var store = new LocalSaveStore(directory);
            store.Save(SampleSave(timestamp: localTimestamp, reputation: 1));
            firebase.RemoteSave = SampleSave(timestamp: remoteTimestamp, reputation: 2);
            var synchronizer = CreateSynchronizer(store);

            network.GoDown(NetworkOutage.Refused);
            Assert.AreEqual(localTimestamp, Run(synchronizer.ReconcileAsync(CancellationToken.None)).Timestamp, "offline the local save is the one in play");
            Assert.IsTrue(synchronizer.HasPendingPush);

            network.Reconnect();
            var newest = Math.Max(localTimestamp, remoteTimestamp);
            Assert.AreEqual(newest, Run(synchronizer.ReconcileAsync(CancellationToken.None)).Timestamp);

            AssertLocalTimestamp(store, newest);
            Assert.AreEqual(newest, firebase.RemoteSave.Timestamp);
            Assert.AreEqual(localTimestamp > remoteTimestamp ? 1 : 0, firebase.Commits.Count);
            Assert.IsFalse(synchronizer.HasPendingPush);
        }

        [TestCase(NetworkOutage.TimedOut)]
        [TestCase(NetworkOutage.Refused)]
        [TestCase(NetworkOutage.ServerError)]
        [TestCase(NetworkOutage.Hanging)]
        public void AnonymousAccount_SurvivesAnOutageAndRefreshesOnReconnect(NetworkOutage outage)
        {
            var clock = new ManualClock();
            var sessions = new InMemoryAuthSessionStore();
            var auth = new FirebaseAnonymousAuth(network, firebase.Endpoints, sessions, () => clock.Now);
            Assert.AreEqual(FakeFirebase.IdToken, Run(auth.GetIdTokenAsync(false, CancellationToken.None)));

            clock.Advance(TimeSpan.FromSeconds(FakeFirebase.TokenLifetimeSeconds));
            network.GoDown(outage);
            var renewal = auth.GetIdTokenAsync(false, CancellationToken.None);
            if (outage == NetworkOutage.Hanging)
            {
                Assert.IsFalse(renewal.IsCompleted);
                network.Reconnect();
            }

            Assert.IsNull(Run(renewal), "an unreachable network yields no token instead of an error");
            Assert.AreEqual(new AuthSession(FakeFirebase.Uid, FakeFirebase.RefreshToken), sessions.Session, "an outage must not throw the anonymous account away");

            network.Reconnect();
            Assert.AreEqual(FakeFirebase.IdToken, Run(auth.GetIdTokenAsync(false, CancellationToken.None)));
            Assert.AreEqual(1, firebase.SignUps, "reconnecting refreshes the same account instead of creating a new one");
            Assert.AreEqual(1, firebase.Refreshes);
            Assert.AreEqual(FakeFirebase.Uid, auth.Uid);
        }

        private PrewarmedArgumentSource CreateSource()
        {
            var client = new ArgumentServiceClient(network, tokens, firebase.Endpoints.ArgumentUrl, validator, delay.Wait);
            var source = new PrewarmedArgumentSource(client, new ArgumentDiskCache(directory), validator, new FallbackArgumentLibrary(fallbackLibrary, validator, Seed), Seed);
            source.Register(BattleFactory.OpponentId, OpponentContext.From(opponent));
            return source;
        }

        private SaveSynchronizer CreateSynchronizer(LocalSaveStore store) =>
            new(store, new FirestoreSaveClient(network, tokens, firebase.Firestore), delay.Wait);

        private List<Argument> Draw(PrewarmedArgumentSource source, int count)
        {
            var debater = new Debater(BattleFactory.OpponentId, Side.Opponent, Profile.Sophist, 50, 5, BattleFactory.OpponentStart, Game.Grid.Facing.SouthWest);
            var drawn = new List<Argument>();
            for (var index = 0; index < count; index++)
            {
                Assert.IsTrue(source.TryNext(debater, out var argument), $"no argument on draw {index}");
                Assert.IsTrue(validator.IsValid(argument), argument.Speech);
                drawn.Add(argument);
            }

            return drawn;
        }

        private static void AssertLocalTimestamp(LocalSaveStore store, long timestamp)
        {
            Assert.IsTrue(store.TryLoad(out var save, out var error), error);
            Assert.AreEqual(timestamp, save.Timestamp);
        }

        private sealed class RecordingArgumentSource : IArgumentSource
        {
            private readonly IArgumentSource inner;

            public RecordingArgumentSource(IArgumentSource inner)
            {
                this.inner = inner;
            }

            public List<Argument> Served { get; } = new();

            public bool TryNext(Debater opponent, out Argument argument)
            {
                if (!inner.TryNext(opponent, out argument))
                {
                    return false;
                }

                Served.Add(argument);
                return true;
            }
        }
    }
}
