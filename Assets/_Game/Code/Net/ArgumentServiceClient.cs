using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;

namespace Game.Net
{
    public sealed class ArgumentServiceClient
    {
        public const double TimeoutSeconds = 2;
        public const double BackoffSeconds = 0.5;
        public const int MaxAttempts = 2;

        private const string AuthorizationHeader = "Authorization";

        private readonly IHttpTransport transport;
        private readonly IIdTokenProvider tokens;
        private readonly string argumentUrl;
        private readonly ArgumentValidator validator;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;

        public ArgumentServiceClient(IHttpTransport transport, IIdTokenProvider tokens, string argumentUrl, ArgumentValidator validator, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            if (string.IsNullOrWhiteSpace(argumentUrl))
            {
                throw new ArgumentException("Argument function URL is required.", nameof(argumentUrl));
            }

            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.argumentUrl = argumentUrl;
            this.delay = delay ?? UnityDelay.WaitAsync;
        }

        public async Task<ArgumentFetch> FetchAsync(ArgumentRequest request, CancellationToken cancellation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                return await FetchWithRetryAsync(request, cancellation);
            }
            catch (OperationCanceledException)
            {
                return ArgumentFetch.Cancelled();
            }
            catch (Exception exception)
            {
                return ArgumentFetch.Unavailable(0, exception.Message);
            }
        }

        private async Task<ArgumentFetch> FetchWithRetryAsync(ArgumentRequest request, CancellationToken cancellation)
        {
            var body = request.ToJson();
            var forceRefresh = false;
            var lastError = "no attempt";

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    await delay(TimeSpan.FromSeconds(BackoffSeconds * (attempt - 1)), cancellation);
                }

                var token = await tokens.GetIdTokenAsync(forceRefresh, cancellation);
                if (token == null)
                {
                    return ArgumentFetch.Unavailable(attempt - 1, "no id token");
                }

                var headers = new Dictionary<string, string> { [AuthorizationHeader] = $"Bearer {token}" };
                var message = new HttpRequestData("POST", argumentUrl, body, HttpRequestData.JsonContentType, headers);
                var response = await transport.SendAsync(message, TimeSpan.FromSeconds(TimeoutSeconds), cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (response.Outcome != HttpOutcome.Completed)
                {
                    lastError = response.Error;
                    continue;
                }

                switch (response.Status)
                {
                    case 200:
                        return ArgumentJson.TryParse(response.Body, validator, out var argument, out var violation)
                            ? ArgumentFetch.Delivered(argument, response.Body, attempt)
                            : ArgumentFetch.Invalid(attempt, violation.ToString());
                    case 204:
                        return ArgumentFetch.Declined(attempt);
                    case 401:
                        forceRefresh = true;
                        lastError = "unauthorized";
                        continue;
                    case 429:
                    case >= 500:
                        lastError = $"http {response.Status}";
                        continue;
                    default:
                        return ArgumentFetch.Unavailable(attempt, $"http {response.Status}");
                }
            }

            return ArgumentFetch.Unavailable(MaxAttempts, lastError);
        }
    }
}
