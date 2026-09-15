using System;

namespace Game.Net
{
    public sealed class FirebaseEndpoints
    {
        public const string DefaultRegion = "southamerica-east1";
        public const string FunctionName = "generateArgument";
        public const string EmulatorApiKey = "emulator-api-key";

        private const string SecretKeyPrefix = "sk-";

        public FirebaseEndpoints(string apiKey, string signUpUrl, string refreshUrl, string argumentUrl)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("Firebase web API key is required.", nameof(apiKey));
            }

            if (apiKey.StartsWith(SecretKeyPrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException("Secret keys never belong in the client.", nameof(apiKey));
            }

            if (string.IsNullOrWhiteSpace(signUpUrl) || string.IsNullOrWhiteSpace(refreshUrl) || string.IsNullOrWhiteSpace(argumentUrl))
            {
                throw new ArgumentException("Every Firebase endpoint URL is required.");
            }

            var key = Uri.EscapeDataString(apiKey);
            SignUpUrl = $"{signUpUrl}?key={key}";
            RefreshUrl = $"{refreshUrl}?key={key}";
            ArgumentUrl = argumentUrl;
        }

        public string SignUpUrl { get; }

        public string RefreshUrl { get; }

        public string ArgumentUrl { get; }

        public static FirebaseEndpoints Production(string apiKey, string projectId, string region = DefaultRegion) =>
            new(apiKey,
                "https://identitytoolkit.googleapis.com/v1/accounts:signUp",
                "https://securetoken.googleapis.com/v1/token",
                $"https://{region}-{projectId}.cloudfunctions.net/{FunctionName}");

        public static FirebaseEndpoints Emulator(string projectId, string host = "127.0.0.1", string region = DefaultRegion) =>
            new(EmulatorApiKey,
                $"http://{host}:9099/identitytoolkit.googleapis.com/v1/accounts:signUp",
                $"http://{host}:9099/securetoken.googleapis.com/v1/token",
                $"http://{host}:5001/{projectId}/{region}/{FunctionName}");
    }
}
