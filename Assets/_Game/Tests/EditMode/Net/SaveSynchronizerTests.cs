using System;
using System.IO;
using System.Threading;
using Game.Net;
using NUnit.Framework;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class SaveSynchronizerTests
    {
        private string directory;
        private LocalSaveStore store;
        private FakeHttpTransport transport;
        private FakeIdTokenProvider tokens;
        private RecordingDelay delay;
        private SaveSynchronizer synchronizer;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-sync-{Guid.NewGuid():N}");
            store = new LocalSaveStore(directory);
            transport = new FakeHttpTransport();
            tokens = new FakeIdTokenProvider("token-1");
            delay = new RecordingDelay();
            synchronizer = new SaveSynchronizer(store, new FirestoreSaveClient(transport, tokens, FirestoreEndpoints.Emulator("demo-gft")), delay.Wait);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SaveLocal_WritesTheFileBeforeAnyNetworkAndPushClearsThePendingFlag()
        {
            synchronizer.SaveLocal(SampleSave());

            Assert.IsTrue(store.Exists);
            Assert.IsTrue(synchronizer.HasPendingPush);
            Assert.IsEmpty(transport.Requests);

            transport.Respond(200, "{}");
            Assert.IsTrue(Run(synchronizer.PushPendingAsync(CancellationToken.None)));
            Assert.IsFalse(synchronizer.HasPendingPush);
        }

        [Test]
        public void Push_RetriesWithExponentialBackoffAndKeepsTheLocalSaveWhenOffline()
        {
            synchronizer.SaveLocal(SampleSave());

            Assert.IsFalse(Run(synchronizer.PushPendingAsync(CancellationToken.None)));

            Assert.AreEqual(SaveSynchronizer.MaxPushAttempts, transport.Requests.Count);
            CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }, delay.Delays);
            Assert.IsTrue(synchronizer.HasPendingPush);
            Assert.IsTrue(store.Exists);
        }

        [Test]
        public void Push_RefreshesTheTokenAfterUnauthorized()
        {
            synchronizer.SaveLocal(SampleSave());
            transport.Respond(401).Respond(200, "{}");

            Assert.IsTrue(Run(synchronizer.PushPendingAsync(CancellationToken.None)));
            CollectionAssert.AreEqual(new[] { false, true }, tokens.ForceRefreshCalls);
        }

        [Test]
        public void Reconcile_NewerRemoteSaveOverwritesTheLocalFile()
        {
            store.Save(SampleSave(timestamp: 100, reputation: 1));
            transport.Respond(200, FirestoreSaveDocument(SampleSave(timestamp: 200, reputation: 99)));

            var current = Run(synchronizer.ReconcileAsync(CancellationToken.None));

            Assert.AreEqual(99, current.Reputation);
            Assert.IsTrue(store.TryLoad(out var reloaded, out _));
            Assert.AreEqual(99, reloaded.Reputation);
            Assert.IsFalse(synchronizer.HasPendingPush);
            Assert.AreEqual(1, transport.Requests.Count);
        }

        [Test]
        public void Reconcile_NewerLocalSaveIsPushed()
        {
            store.Save(SampleSave(timestamp: 300, reputation: 7));
            transport.Respond(200, FirestoreSaveDocument(SampleSave(timestamp: 200))).Respond(200, "{}");

            var current = Run(synchronizer.ReconcileAsync(CancellationToken.None));

            Assert.AreEqual(7, current.Reputation);
            Assert.AreEqual(2, transport.Requests.Count);
            StringAssert.EndsWith(":commit", transport.Requests[1].Url);
            Assert.IsFalse(synchronizer.HasPendingPush);
        }

        [Test]
        public void Reconcile_Offline_KeepsTheLocalSaveAndMarksItPending()
        {
            store.Save(SampleSave(timestamp: 100));

            var current = Run(synchronizer.ReconcileAsync(CancellationToken.None));

            Assert.AreEqual(100, current.Timestamp);
            Assert.IsTrue(synchronizer.HasPendingPush);
            Assert.AreEqual(RemoteSaveStatus.Unavailable, synchronizer.LastRemoteStatus);
        }

        [Test]
        public void Reconcile_FirstSyncUploadsAnExistingLocalSave()
        {
            store.Save(SampleSave(timestamp: 100));
            transport.Respond(404).Respond(200, "{}");

            Run(synchronizer.ReconcileAsync(CancellationToken.None));

            Assert.AreEqual(2, transport.Requests.Count);
            Assert.IsFalse(synchronizer.HasPendingPush);
        }

        [Test]
        public void Reconcile_NewPlayerWithoutAnySave_StartsEmpty()
        {
            transport.Respond(404);

            Assert.IsNull(Run(synchronizer.ReconcileAsync(CancellationToken.None)));
            Assert.IsFalse(synchronizer.HasPendingPush);
        }
    }
}
