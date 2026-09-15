using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.Grid;
using Game.Net;
using NUnit.Framework;
using UnityEngine;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class PrewarmedArgumentSourceTests
    {
        private const int OpponentDebaterId = 2;
        private const int Seed = 7;
        private const string FallbackJson =
            "[{\"speech\":\"Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.\",\"fallacy_id\":\"ad_hominem\",\"premises\":[\"Kleia é do Cerâmico\",\"Oleiros não entendem de justiça\"],\"weak_premise\":1,\"tone\":\"mocking\"}]";

        private string directory;
        private FakeHttpTransport transport;
        private ArgumentDiskCache cache;
        private FallbackLibrarySO library;
        private TextAsset fallbackText;
        private Debater opponent;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-prewarm-{Guid.NewGuid():N}");
            transport = new FakeHttpTransport();
            cache = new ArgumentDiskCache(directory);
            fallbackText = new TextAsset(FallbackJson);
            library = ScriptableObject.CreateInstance<FallbackLibrarySO>();
            library.SetEntries(new Dictionary<string, TextAsset> { ["false_dilemma"] = fallbackText });
            opponent = new Debater(OpponentDebaterId, Side.Opponent, Profile.Sophist, 50, 5, new Coord(4, 2), Facing.SouthWest);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(library);
            UnityEngine.Object.DestroyImmediate(fallbackText);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Prewarm_QueuesDeliveredArgumentsAndServesThemBeforeTheFallback()
        {
            transport.Respond(200, OpponentArgumentJson).Respond(200, OpponentArgumentJson);
            var source = CreateSource();

            Assert.AreEqual(2, Run(source.PrewarmAsync(OpponentDebaterId, 2, CancellationToken.None)));
            Assert.AreEqual(2, source.ReadyCount(OpponentDebaterId));

            Assert.IsTrue(source.TryNext(opponent, out var first));
            Assert.IsTrue(source.TryNext(opponent, out var second));
            Assert.AreEqual("false_dilemma", first.FallacyId);
            Assert.AreEqual("false_dilemma", second.FallacyId);

            Assert.IsTrue(source.TryNext(opponent, out var fallback));
            Assert.AreEqual("ad_hominem", fallback.FallacyId);
        }

        [Test]
        public void Prewarm_StoresDeliveredArgumentsInTheDiskCacheForTheNextSession()
        {
            transport.Respond(200, OpponentArgumentJson);
            Run(CreateSource().PrewarmAsync(OpponentDebaterId, 1, CancellationToken.None));
            Assert.AreEqual(1, cache.Count);

            var nextSession = CreateSource();
            Assert.AreEqual(1, Run(nextSession.PrewarmAsync(OpponentDebaterId, 1, CancellationToken.None)));
            Assert.AreEqual(1, transport.Requests.Count);
        }

        [Test]
        public void Offline_StopsPrewarmingAndFallsBackSilently()
        {
            var source = CreateSource();

            Assert.AreEqual(0, Run(source.PrewarmAsync(OpponentDebaterId, 5, CancellationToken.None)));
            Assert.AreEqual(ArgumentServiceClient.MaxAttempts, transport.Requests.Count);

            Assert.IsTrue(source.TryNext(opponent, out var argument));
            Assert.AreEqual("ad_hominem", argument.FallacyId);
        }

        [Test]
        public void UnregisteredDebater_HasNoArguments()
        {
            var stranger = new Debater(99, Side.Opponent, Profile.Sophist, 50, 5, new Coord(1, 1), Facing.SouthWest);

            Assert.IsFalse(CreateSource().TryNext(stranger, out _));
        }

        [Test]
        public void RequestCacheKey_MatchesTheServerCacheKey()
        {
            var request = new ArgumentRequest("thrasybios", "Thrasybios", Profile.Sophist, Act.One, "ad_hominem", 7);

            Assert.AreEqual("5ec6c70384b1a269847f092224354426", request.CacheKey);
        }

        private PrewarmedArgumentSource CreateSource()
        {
            var validator = Validator();
            var client = new ArgumentServiceClient(transport, new FakeIdTokenProvider("token"), "http://127.0.0.1:5001/demo-gft/southamerica-east1/generateArgument", validator, new RecordingDelay().Wait);
            var source = new PrewarmedArgumentSource(client, cache, validator, new FallbackArgumentLibrary(library, validator, Seed), Seed);
            source.Register(OpponentDebaterId, new OpponentContext("thrasybios", "Thrasybios", Profile.Sophist, Act.One, new[] { "false_dilemma" }));
            return source;
        }
    }
}
