using System;
using System.IO;
using System.Linq;
using System.Threading;
using Game.Net;
using NUnit.Framework;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class FirebaseAnonymousAuthTests
    {
        private const string SignUpGrant = "{\"idToken\":\"id-1\",\"refreshToken\":\"refresh-1\",\"expiresIn\":\"3600\",\"localId\":\"uid-1\"}";
        private const string RefreshGrant = "{\"id_token\":\"id-2\",\"refresh_token\":\"refresh-2\",\"expires_in\":\"3600\",\"user_id\":\"uid-1\"}";

        private FakeHttpTransport transport;
        private InMemoryAuthSessionStore store;
        private ManualClock clock;
        private FirebaseAnonymousAuth auth;
        private FirebaseEndpoints endpoints;

        [SetUp]
        public void SetUp()
        {
            transport = new FakeHttpTransport();
            store = new InMemoryAuthSessionStore();
            clock = new ManualClock();
            endpoints = FirebaseEndpoints.Emulator("demo-gft");
            auth = new FirebaseAnonymousAuth(transport, endpoints, store, () => clock.Now);
        }

        [Test]
        public void FirstBoot_SignsUpAnonymouslyAndPersistsTheRefreshToken()
        {
            transport.Respond(200, SignUpGrant);

            Assert.AreEqual("id-1", Token());

            var request = transport.Requests.Single();
            Assert.AreEqual("http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=emulator-api-key", request.Url);
            StringAssert.Contains("returnSecureToken", request.Body);
            Assert.AreEqual(new AuthSession("uid-1", "refresh-1"), store.Session);
            Assert.AreEqual("uid-1", auth.Uid);
        }

        [Test]
        public void CachedToken_IsReusedUntilItIsCloseToExpiring()
        {
            transport.Respond(200, SignUpGrant).Respond(200, RefreshGrant);
            Token();

            clock.Advance(TimeSpan.FromSeconds(3600 - FirebaseAnonymousAuth.ExpiryMarginSeconds - 1));
            Assert.AreEqual("id-1", Token());
            Assert.AreEqual(1, transport.Requests.Count);

            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.AreEqual("id-2", Token());

            var refresh = transport.Requests[1];
            Assert.AreEqual("http://127.0.0.1:9099/securetoken.googleapis.com/v1/token?key=emulator-api-key", refresh.Url);
            Assert.AreEqual(HttpRequestData.FormContentType, refresh.ContentType);
            Assert.AreEqual("grant_type=refresh_token&refresh_token=refresh-1", refresh.Body);
            Assert.AreEqual("refresh-2", store.Session.RefreshToken);
        }

        [Test]
        public void StoredSession_RefreshesInsteadOfCreatingANewAccount()
        {
            store.Session = new AuthSession("uid-1", "refresh-1");
            transport.Respond(200, RefreshGrant);

            Assert.AreEqual("id-2", Token());
            StringAssert.Contains("securetoken", transport.Requests.Single().Url);
        }

        [Test]
        public void RejectedRefreshToken_StartsANewAnonymousAccount()
        {
            store.Session = new AuthSession("uid-0", "revoked");
            transport.Respond(400, "{\"error\":{\"message\":\"INVALID_REFRESH_TOKEN\"}}").Respond(200, SignUpGrant);

            Assert.AreEqual("id-1", Token());
            Assert.AreEqual("uid-1", store.Session.Uid);
        }

        [Test]
        public void Offline_ReturnsNoTokenAndKeepsTheStoredSession()
        {
            store.Session = new AuthSession("uid-1", "refresh-1");
            transport.Respond(HttpResponseData.Failed("Cannot resolve destination host"));

            Assert.IsNull(Token());
            Assert.AreEqual("refresh-1", store.Session.RefreshToken);
        }

        [Test]
        public void ForceRefresh_BypassesTheCachedToken()
        {
            transport.Respond(200, SignUpGrant).Respond(200, RefreshGrant);
            Token();

            Assert.AreEqual("id-2", Run(auth.GetIdTokenAsync(true, CancellationToken.None)));
        }

        [Test]
        public void MalformedGrant_IsTreatedAsUnavailable()
        {
            transport.Respond(200, "{\"idToken\":\"id-1\"}");

            Assert.IsNull(Token());
            Assert.IsNull(store.Session);
        }

        [Test]
        public void FileStore_RoundTripsAndClearsTheSession()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"gft-auth-{Guid.NewGuid():N}");
            try
            {
                var fileStore = new FileAuthSessionStore(directory);
                Assert.IsFalse(fileStore.TryLoad(out _));

                fileStore.Save(new AuthSession("uid-1", "refresh-1"));
                Assert.IsTrue(new FileAuthSessionStore(directory).TryLoad(out var loaded));
                Assert.AreEqual(new AuthSession("uid-1", "refresh-1"), loaded);

                fileStore.Clear();
                Assert.IsFalse(fileStore.TryLoad(out _));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void Endpoints_RefuseSecretKeysAndPointProductionAtTheFunctionRegion()
        {
            Assert.Throws<ArgumentException>(() => FirebaseEndpoints.Production("sk-live-123", "gft"));

            var production = FirebaseEndpoints.Production("AIza-web-key", "gft");
            Assert.AreEqual("https://southamerica-east1-gft.cloudfunctions.net/generateArgument", production.ArgumentUrl);
            Assert.AreEqual("http://127.0.0.1:5001/demo-gft/southamerica-east1/generateArgument", endpoints.ArgumentUrl);
        }

        private string Token() => Run(auth.GetIdTokenAsync(false, CancellationToken.None));
    }
}
