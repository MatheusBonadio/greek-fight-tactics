using System;

namespace Game.Net
{
    public sealed class FirestoreEndpoints
    {
        public FirestoreEndpoints(string baseUrl, string projectId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Firestore base URL is required.", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new ArgumentException("Firebase project id is required.", nameof(projectId));
            }

            BaseUrl = baseUrl.TrimEnd('/');
            DocumentsRoot = $"projects/{projectId}/databases/(default)/documents";
        }

        public string BaseUrl { get; }

        public string DocumentsRoot { get; }

        public string CommitUrl => $"{BaseUrl}/{DocumentsRoot}:commit";

        public static FirestoreEndpoints Production(string projectId) => new("https://firestore.googleapis.com/v1", projectId);

        public static FirestoreEndpoints Emulator(string projectId, string host = "127.0.0.1") => new($"http://{host}:8080/v1", projectId);

        public string DocumentUrl(string path) => $"{BaseUrl}/{DocumentsRoot}/{path}";
    }
}
