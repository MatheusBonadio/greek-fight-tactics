using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using Game.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Tests.Support
{
    public sealed class FakeFirebase
    {
        public const string ProjectId = "demo-gft";
        public const string Uid = "uid-airplane";
        public const string IdToken = "id-airplane";
        public const string RefreshToken = "refresh-airplane";
        public const string ServedSpeechPrefix = "Pela rede,";
        public const int TokenLifetimeSeconds = 3600;

        private const string NotFoundBody = "{\"error\":{\"code\":404,\"status\":\"NOT_FOUND\"}}";

        public FakeFirebase()
        {
            Endpoints = FirebaseEndpoints.Emulator(ProjectId);
            Firestore = FirestoreEndpoints.Emulator(ProjectId);
        }

        public FirebaseEndpoints Endpoints { get; }

        public FirestoreEndpoints Firestore { get; }

        public SaveData RemoteSave { get; set; }

        public List<SaveData> Commits { get; } = new();

        public int SignUps { get; private set; }

        public int Refreshes { get; private set; }

        public int ArgumentsServed { get; private set; }

        public string SaveDocumentUrl => Firestore.DocumentUrl($"{FirestoreDocuments.SavesCollection}/{Uid}");

        public static string ServedSpeech(string fallacyId) =>
            $"{ServedSpeechPrefix} um argumento sobre {fallacyId} chega antes do turno começar.";

        public static bool WasServed(in Argument argument) =>
            argument.Speech != null && argument.Speech.StartsWith(ServedSpeechPrefix, StringComparison.Ordinal);

        public HttpResponseData Handle(HttpRequestData request)
        {
            if (request.Url == Endpoints.SignUpUrl)
            {
                SignUps++;
                return Grant("idToken", "refreshToken", "expiresIn", "localId");
            }

            if (request.Url == Endpoints.RefreshUrl)
            {
                Refreshes++;
                return Grant("id_token", "refresh_token", "expires_in", "user_id");
            }

            if (request.Url == Endpoints.ArgumentUrl)
            {
                return ServeArgument(request.Body);
            }

            if (request.Url == Firestore.CommitUrl)
            {
                return Commit(request.Body);
            }

            if (request.Method == "GET" && request.Url == SaveDocumentUrl)
            {
                return RemoteSave == null
                    ? HttpResponseData.Completed(404, NotFoundBody)
                    : HttpResponseData.Completed(200, SaveDocument(RemoteSave));
            }

            return HttpResponseData.Completed(404, NotFoundBody);
        }

        public string SaveDocument(SaveData save)
        {
            var update = FirestoreDocuments.SaveCommit(Firestore.DocumentsRoot, Uid, save)["writes"][0]["update"];
            return new JObject { ["name"] = update["name"], ["fields"] = update["fields"] }.ToString(Formatting.None);
        }

        private static HttpResponseData Grant(string idTokenField, string refreshTokenField, string expiresInField, string uidField) =>
            HttpResponseData.Completed(200, new JObject
            {
                [idTokenField] = IdToken,
                [refreshTokenField] = RefreshToken,
                [expiresInField] = TokenLifetimeSeconds.ToString(CultureInfo.InvariantCulture),
                [uidField] = Uid
            }.ToString(Formatting.None));

        private HttpResponseData ServeArgument(string body)
        {
            var fallacyId = JObject.Parse(body).Value<string>("fallacy_id");
            ArgumentsServed++;
            return HttpResponseData.Completed(200, new JObject
            {
                ["speech"] = ServedSpeech(fallacyId),
                ["fallacy_id"] = fallacyId,
                ["premises"] = new JArray("Quem fala alto tem razão", "A plateia aplaudiu"),
                ["weak_premise"] = 0,
                ["tone"] = ArgumentTones.Arrogant
            }.ToString(Formatting.None));
        }

        private HttpResponseData Commit(string body)
        {
            var update = JObject.Parse(body)["writes"]?[0]?["update"];
            if (update == null)
            {
                return HttpResponseData.Completed(400, "{\"error\":\"commit without writes\"}");
            }

            if (!FirestoreDocuments.TryReadSave(update.ToString(Formatting.None), out var save, out var error))
            {
                return HttpResponseData.Completed(400, new JObject { ["error"] = error }.ToString(Formatting.None));
            }

            RemoteSave = save;
            Commits.Add(save);
            return HttpResponseData.Completed(200, "{\"writeResults\":[]}");
        }
    }
}
