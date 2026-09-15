using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Net
{
    public sealed class FirebaseAnonymousAuth : IIdTokenProvider
    {
        public const int ExpiryMarginSeconds = 300;
        public const double RequestTimeoutSeconds = 5;

        private const string SignUpBody = "{\"returnSecureToken\":true}";

        private readonly IHttpTransport transport;
        private readonly FirebaseEndpoints endpoints;
        private readonly IAuthSessionStore store;
        private readonly Func<DateTimeOffset> clock;
        private AuthSession session;
        private string idToken;
        private DateTimeOffset renewAt;
        private Task<string> pending;

        public FirebaseAnonymousAuth(IHttpTransport transport, FirebaseEndpoints endpoints, IAuthSessionStore store, Func<DateTimeOffset> clock = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        private enum TokenGrant
        {
            Granted,
            Rejected,
            Unavailable
        }

        public string Uid => session?.Uid;

        public bool HasValidToken => idToken != null && clock() < renewAt;

        private static TokenFields SignUpFields => new("idToken", "refreshToken", "expiresIn", "localId");

        private static TokenFields RefreshFields => new("id_token", "refresh_token", "expires_in", "user_id");

        public Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellation)
        {
            if (!forceRefresh && HasValidToken)
            {
                return Task.FromResult(idToken);
            }

            if (pending is { IsCompleted: false })
            {
                return pending;
            }

            pending = AcquireAsync(cancellation);
            return pending;
        }

        private async Task<string> AcquireAsync(CancellationToken cancellation)
        {
            if (session == null && store.TryLoad(out var stored))
            {
                session = stored;
            }

            if (session != null)
            {
                var refreshed = await RequestTokenAsync(RefreshRequest(session.RefreshToken), RefreshFields, cancellation);
                if (refreshed != TokenGrant.Rejected)
                {
                    return refreshed == TokenGrant.Granted ? idToken : null;
                }

                store.Clear();
                session = null;
                idToken = null;
            }

            return await RequestTokenAsync(new HttpRequestData("POST", endpoints.SignUpUrl, SignUpBody, HttpRequestData.JsonContentType), SignUpFields, cancellation) == TokenGrant.Granted
                ? idToken
                : null;
        }

        private HttpRequestData RefreshRequest(string refreshToken) =>
            new("POST", endpoints.RefreshUrl, $"grant_type=refresh_token&refresh_token={Uri.EscapeDataString(refreshToken)}", HttpRequestData.FormContentType);

        private async Task<TokenGrant> RequestTokenAsync(HttpRequestData request, TokenFields fields, CancellationToken cancellation)
        {
            var response = await transport.SendAsync(request, TimeSpan.FromSeconds(RequestTimeoutSeconds), cancellation);
            if (response.Outcome != HttpOutcome.Completed || response.Status == 429 || response.Status >= 500)
            {
                return TokenGrant.Unavailable;
            }

            if (!response.IsSuccess)
            {
                return TokenGrant.Rejected;
            }

            if (!JsonFields.TryParseObject(response.Body, out var json))
            {
                return TokenGrant.Unavailable;
            }

            var token = JsonFields.Text(json, fields.IdToken);
            var refreshToken = JsonFields.Text(json, fields.RefreshToken);
            var uid = JsonFields.Text(json, fields.Uid);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(uid) ||
                !int.TryParse(JsonFields.Text(json, fields.ExpiresIn), NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresInSeconds))
            {
                return TokenGrant.Unavailable;
            }

            idToken = token;
            renewAt = clock().AddSeconds(Math.Max(0, expiresInSeconds - ExpiryMarginSeconds));
            session = new AuthSession(uid, refreshToken);
            store.Save(session);
            return TokenGrant.Granted;
        }

        private readonly struct TokenFields
        {
            public readonly string IdToken;
            public readonly string RefreshToken;
            public readonly string ExpiresIn;
            public readonly string Uid;

            public TokenFields(string idToken, string refreshToken, string expiresIn, string uid)
            {
                IdToken = idToken;
                RefreshToken = refreshToken;
                ExpiresIn = expiresIn;
                Uid = uid;
            }
        }
    }
}
