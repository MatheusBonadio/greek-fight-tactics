using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Game.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.EditMode.Net
{
    internal sealed class FakeHttpTransport : IHttpTransport
    {
        private readonly Queue<Func<HttpRequestData, HttpResponseData>> responses = new();

        public List<HttpRequestData> Requests { get; } = new();

        public List<TimeSpan> Timeouts { get; } = new();

        public FakeHttpTransport Respond(HttpResponseData response)
        {
            responses.Enqueue(_ => response);
            return this;
        }

        public FakeHttpTransport Respond(long status, string body = null) => Respond(HttpResponseData.Completed(status, body));

        public FakeHttpTransport Throw(Exception exception)
        {
            responses.Enqueue(_ => throw exception);
            return this;
        }

        public Task<HttpResponseData> SendAsync(HttpRequestData request, TimeSpan timeout, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Requests.Add(request);
            Timeouts.Add(timeout);
            return Task.FromResult(responses.Count == 0 ? HttpResponseData.Failed("no response queued") : responses.Dequeue()(request));
        }
    }

    internal sealed class FakeIdTokenProvider : IIdTokenProvider
    {
        private readonly Queue<string> tokens;

        public FakeIdTokenProvider(params string[] tokens)
        {
            this.tokens = new Queue<string>(tokens);
        }

        public string Uid { get; set; } = "uid-1";

        public List<bool> ForceRefreshCalls { get; } = new();

        public Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellation)
        {
            ForceRefreshCalls.Add(forceRefresh);
            var token = tokens.Count switch
            {
                0 => null,
                1 => tokens.Peek(),
                _ => tokens.Dequeue()
            };

            return Task.FromResult(token);
        }
    }

    internal sealed class RecordingDelay
    {
        public List<TimeSpan> Delays { get; } = new();

        public Task Wait(TimeSpan duration, CancellationToken cancellation)
        {
            Delays.Add(duration);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryAuthSessionStore : IAuthSessionStore
    {
        public AuthSession Session { get; set; }

        public bool TryLoad(out AuthSession session)
        {
            session = Session;
            return session != null;
        }

        public void Save(AuthSession session) => Session = session;

        public void Clear() => Session = null;
    }

    internal sealed class ManualClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan duration) => Now += duration;
    }

    internal static class NetSamples
    {
        public const string DocumentsRoot = "projects/demo-gft/databases/(default)/documents";

        public const string OpponentArgumentJson =
            "{\"speech\":\"Ou você respeita os deuses, ou você é como Sócrates. Não existe meio-termo, menina.\",\"fallacy_id\":\"false_dilemma\",\"premises\":[\"Só existem duas posições\",\"Quem duvida é como Sócrates\"],\"weak_premise\":0,\"tone\":\"theatrical\"}";

        public static ArgumentValidator Validator() => new(new[] { "false_dilemma", "ad_hominem" });

        public static SaveData SampleSave(long timestamp = 1789480000000, int reputation = 12)
        {
            var save = new SaveData
            {
                ActFlag = Act.Two,
                FogBitmask = new byte[] { 1, 2, 255 },
                DamarisBranch = DamarisChoice.Accepted,
                Reputation = reputation,
                Position = new Coord(7, 3),
                Timestamp = timestamp
            };
            save.Inventory["heraclitus"] = 2;
            save.Duels["batilo"] = new DuelRecord(true, DuelVictory.Maieutics);
            save.Flags["met_menexeno"] = "yes";
            return save;
        }

        public static string FirestoreSaveDocument(SaveData save, string uid = "uid-1")
        {
            var commit = FirestoreDocuments.SaveCommit(DocumentsRoot, uid, save);
            var update = commit["writes"][0]["update"];
            return new JObject { ["name"] = update["name"], ["fields"] = update["fields"] }.ToString(Formatting.None);
        }

        public static T Run<T>(Task<T> task)
        {
            Assert.IsTrue(task.IsCompleted, "fakes complete synchronously, so the task must already be finished");
            return task.GetAwaiter().GetResult();
        }
    }
}
