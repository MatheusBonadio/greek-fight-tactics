using System.Linq;
using System.Threading;
using Game.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class FirestoreSaveClientTests
    {
        private const string EmulatorDocuments = "http://127.0.0.1:8080/v1/projects/demo-gft/databases/(default)/documents";

        private FakeHttpTransport transport;
        private FakeIdTokenProvider tokens;

        [SetUp]
        public void SetUp()
        {
            transport = new FakeHttpTransport();
            tokens = new FakeIdTokenProvider("token-1");
        }

        [Test]
        public void Push_CommitsTheSaveAndDuelDocumentsWithTheIdToken()
        {
            transport.Respond(200, "{\"writeResults\":[]}");

            var result = Run(Client().PushAsync(SampleSave(), CancellationToken.None));

            Assert.AreEqual(RemoteSaveStatus.Ok, result.Status);
            var request = transport.Requests.Single();
            Assert.AreEqual("POST", request.Method);
            Assert.AreEqual(EmulatorDocuments + ":commit", request.Url);
            Assert.AreEqual("Bearer token-1", request.Headers["Authorization"]);

            var writes = (JArray)JObject.Parse(request.Body)["writes"];
            Assert.AreEqual(DocumentsRoot + "/saves/uid-1", (string)writes[0]["update"]["name"]);
            Assert.AreEqual(DocumentsRoot + "/progress/uid-1/duels/batilo", (string)writes[1]["update"]["name"]);
        }

        [Test]
        public void Pull_ReadsTheOwnSaveDocument()
        {
            transport.Respond(200, FirestoreSaveDocument(SampleSave(reputation: 33)));

            var result = Run(Client().PullAsync(CancellationToken.None));

            Assert.AreEqual(RemoteSaveStatus.Ok, result.Status);
            Assert.AreEqual(33, result.Save.Reputation);
            Assert.AreEqual("GET", transport.Requests.Single().Method);
            Assert.AreEqual(EmulatorDocuments + "/saves/uid-1", transport.Requests.Single().Url);
        }

        [TestCase(404, RemoteSaveStatus.NotFound)]
        [TestCase(401, RemoteSaveStatus.Unauthorized)]
        [TestCase(403, RemoteSaveStatus.Unauthorized)]
        [TestCase(429, RemoteSaveStatus.Unavailable)]
        [TestCase(503, RemoteSaveStatus.Unavailable)]
        [TestCase(400, RemoteSaveStatus.Invalid)]
        public void Pull_MapsHttpStatuses(int status, RemoteSaveStatus expected)
        {
            transport.Respond(status, "{}");

            Assert.AreEqual(expected, Run(Client().PullAsync(CancellationToken.None)).Status);
        }

        [Test]
        public void Offline_IsUnavailable()
        {
            transport.Respond(HttpResponseData.Failed("Cannot connect to destination host"));

            Assert.AreEqual(RemoteSaveStatus.Unavailable, Run(Client().PushAsync(SampleSave(), CancellationToken.None)).Status);
        }

        [Test]
        public void NotSignedIn_SkipsTheNetwork()
        {
            tokens = new FakeIdTokenProvider();

            Assert.AreEqual(RemoteSaveStatus.Unavailable, Run(Client().PullAsync(CancellationToken.None)).Status);
            Assert.IsEmpty(transport.Requests);
        }

        [Test]
        public void ForceRefresh_IsForwardedToTheTokenProvider()
        {
            transport.Respond(200, "{}");

            Run(Client().PushAsync(SampleSave(), CancellationToken.None, forceRefresh: true));

            CollectionAssert.AreEqual(new[] { true }, tokens.ForceRefreshCalls);
        }

        private FirestoreSaveClient Client() => new(transport, tokens, FirestoreEndpoints.Emulator("demo-gft"));
    }
}
