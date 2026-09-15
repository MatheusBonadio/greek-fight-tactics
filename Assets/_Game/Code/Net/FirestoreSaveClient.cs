using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;

namespace Game.Net
{
    public enum RemoteSaveStatus
    {
        Ok,
        NotFound,
        Unauthorized,
        Invalid,
        Unavailable,
        Cancelled
    }

    public readonly struct RemoteSaveResult
    {
        public readonly RemoteSaveStatus Status;
        public readonly SaveData Save;
        public readonly string Error;

        public RemoteSaveResult(RemoteSaveStatus status, SaveData save = null, string error = null)
        {
            Status = status;
            Save = save;
            Error = error;
        }
    }

    public sealed class FirestoreSaveClient
    {
        public const double TimeoutSeconds = 10;

        private const string AuthorizationHeader = "Authorization";

        private readonly IHttpTransport transport;
        private readonly IIdTokenProvider tokens;
        private readonly FirestoreEndpoints endpoints;

        public FirestoreSaveClient(IHttpTransport transport, IIdTokenProvider tokens, FirestoreEndpoints endpoints)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            this.endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
        }

        public Task<RemoteSaveResult> PullAsync(CancellationToken cancellation, bool forceRefresh = false) =>
            SendAsync(
                uid => new HttpRequestData("GET", endpoints.DocumentUrl($"{FirestoreDocuments.SavesCollection}/{Uri.EscapeDataString(uid)}"), null, null),
                body => FirestoreDocuments.TryReadSave(body, out var save, out var error)
                    ? new RemoteSaveResult(RemoteSaveStatus.Ok, save)
                    : new RemoteSaveResult(RemoteSaveStatus.Invalid, error: error),
                forceRefresh,
                cancellation);

        public Task<RemoteSaveResult> PushAsync(SaveData save, CancellationToken cancellation, bool forceRefresh = false)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            return SendAsync(
                uid => new HttpRequestData("POST", endpoints.CommitUrl, FirestoreDocuments.SaveCommit(endpoints.DocumentsRoot, uid, save).ToString(Formatting.None), HttpRequestData.JsonContentType),
                _ => new RemoteSaveResult(RemoteSaveStatus.Ok, save),
                forceRefresh,
                cancellation);
        }

        private async Task<RemoteSaveResult> SendAsync(Func<string, HttpRequestData> build, Func<string, RemoteSaveResult> onSuccess, bool forceRefresh, CancellationToken cancellation)
        {
            try
            {
                var token = await tokens.GetIdTokenAsync(forceRefresh, cancellation);
                var uid = tokens.Uid;
                if (token == null || string.IsNullOrEmpty(uid))
                {
                    return new RemoteSaveResult(RemoteSaveStatus.Unavailable, error: "not signed in");
                }

                var request = build(uid);
                var headers = new Dictionary<string, string> { [AuthorizationHeader] = $"Bearer {token}" };
                var response = await transport.SendAsync(
                    new HttpRequestData(request.Method, request.Url, request.Body, request.ContentType, headers),
                    TimeSpan.FromSeconds(TimeoutSeconds),
                    cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (response.Outcome != HttpOutcome.Completed)
                {
                    return new RemoteSaveResult(RemoteSaveStatus.Unavailable, error: response.Error);
                }

                return response.Status switch
                {
                    >= 200 and < 300 => onSuccess(response.Body),
                    404 => new RemoteSaveResult(RemoteSaveStatus.NotFound),
                    401 or 403 => new RemoteSaveResult(RemoteSaveStatus.Unauthorized, error: $"http {response.Status}"),
                    429 or >= 500 => new RemoteSaveResult(RemoteSaveStatus.Unavailable, error: $"http {response.Status}"),
                    _ => new RemoteSaveResult(RemoteSaveStatus.Invalid, error: $"http {response.Status}: {response.Body}")
                };
            }
            catch (OperationCanceledException)
            {
                return new RemoteSaveResult(RemoteSaveStatus.Cancelled);
            }
            catch (Exception exception)
            {
                return new RemoteSaveResult(RemoteSaveStatus.Unavailable, error: exception.Message);
            }
        }
    }
}
