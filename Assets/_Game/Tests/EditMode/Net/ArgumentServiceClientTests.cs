using System;
using System.Linq;
using System.Threading;
using Game.Core;
using Game.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class ArgumentServiceClientTests
    {
        private const string Url = "http://127.0.0.1:5001/demo-gft/southamerica-east1/generateArgument";

        private FakeHttpTransport transport;
        private FakeIdTokenProvider tokens;
        private RecordingDelay delay;

        [SetUp]
        public void SetUp()
        {
            transport = new FakeHttpTransport();
            tokens = new FakeIdTokenProvider("token-1", "token-2");
            delay = new RecordingDelay();
        }

        [Test]
        public void Delivered_PostsTheRequestWithTheIdTokenAndATwoSecondTimeout()
        {
            transport.Respond(200, OpponentArgumentJson);

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Delivered, fetch.Status);
            Assert.AreEqual("false_dilemma", fetch.Argument.FallacyId);
            Assert.AreEqual(OpponentArgumentJson, fetch.Json);
            Assert.AreEqual(1, fetch.Attempts);

            var request = transport.Requests.Single();
            Assert.AreEqual("POST", request.Method);
            Assert.AreEqual(Url, request.Url);
            Assert.AreEqual("Bearer token-1", request.Headers["Authorization"]);
            Assert.AreEqual(HttpRequestData.JsonContentType, request.ContentType);
            Assert.AreEqual(TimeSpan.FromSeconds(2), transport.Timeouts.Single());

            var body = JObject.Parse(request.Body);
            Assert.AreEqual("thrasybios", (string)body["opponent_id"]);
            Assert.AreEqual("Thrasybios", (string)body["opponent_name"]);
            Assert.AreEqual("sophist", (string)body["profile"]);
            Assert.AreEqual(1, (int)body["act"]);
            Assert.AreEqual("false_dilemma", (string)body["fallacy_id"]);
            Assert.AreEqual(3, (int)body["seed"]);
            Assert.AreEqual("a piedade na ágora", (string)body["topic"]);
        }

        [Test]
        public void NoContent_MeansTheServerDeclinedAndIsNotRetried()
        {
            transport.Respond(204);

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Declined, fetch.Status);
            Assert.AreEqual(1, transport.Requests.Count);
            Assert.IsEmpty(delay.Delays);
        }

        [Test]
        public void Timeout_IsRetriedOnceWithBackoff()
        {
            transport.Respond(HttpResponseData.Timeout()).Respond(200, OpponentArgumentJson);

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Delivered, fetch.Status);
            Assert.AreEqual(2, fetch.Attempts);
            CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(ArgumentServiceClient.BackoffSeconds) }, delay.Delays);
        }

        [Test]
        public void RepeatedServerErrors_GiveUpAfterTheRetry()
        {
            transport.Respond(503).Respond(500).Respond(200, OpponentArgumentJson);

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Unavailable, fetch.Status);
            Assert.AreEqual(ArgumentServiceClient.MaxAttempts, transport.Requests.Count);
        }

        [Test]
        public void Unauthorized_ForcesATokenRefreshBeforeTheRetry()
        {
            transport.Respond(401).Respond(200, OpponentArgumentJson);

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Delivered, fetch.Status);
            CollectionAssert.AreEqual(new[] { false, true }, tokens.ForceRefreshCalls);
            Assert.AreEqual("Bearer token-2", transport.Requests[1].Headers["Authorization"]);
        }

        [Test]
        public void PayloadThatBreaksTheContract_IsInvalidAndNotRetried()
        {
            transport.Respond(200, OpponentArgumentJson.Replace("\"weak_premise\":0", "\"weak_premise\":9"));

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Invalid, fetch.Status);
            Assert.AreEqual(nameof(ArgumentViolation.WeakPremiseOutOfRange), fetch.Error);
            Assert.AreEqual(1, transport.Requests.Count);
        }

        [Test]
        public void MissingIdToken_SkipsTheNetwork()
        {
            tokens = new FakeIdTokenProvider();

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Unavailable, fetch.Status);
            Assert.IsEmpty(transport.Requests);
        }

        [Test]
        public void Cancellation_IsReportedWithoutThrowing()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            transport.Respond(200, OpponentArgumentJson);

            var fetch = Fetch(source.Token);

            Assert.AreEqual(ArgumentFetchStatus.Cancelled, fetch.Status);
            Assert.IsEmpty(transport.Requests);
        }

        [Test]
        public void TransportException_BecomesUnavailableInsteadOfThrowing()
        {
            transport.Throw(new InvalidOperationException("socket closed"));

            var fetch = Fetch();

            Assert.AreEqual(ArgumentFetchStatus.Unavailable, fetch.Status);
            Assert.AreEqual("socket closed", fetch.Error);
        }

        private ArgumentFetch Fetch(CancellationToken cancellation = default)
        {
            var client = new ArgumentServiceClient(transport, tokens, Url, Validator(), delay.Wait);
            var request = new ArgumentRequest("thrasybios", "Thrasybios", Profile.Sophist, Act.One, "false_dilemma", 3, "a piedade na ágora");
            return Run(client.FetchAsync(request, cancellation));
        }
    }
}
