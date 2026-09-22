using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Net;

namespace Game.Tests.Support
{
    public enum NetworkOutage
    {
        None,
        TimedOut,
        Refused,
        ServerError,
        Hanging,
        Unresolvable
    }

    public sealed class FlakyNetwork : IHttpTransport
    {
        public const string RefusedError = "Cannot connect to destination host";
        public const long ServerErrorStatus = 503;
        public const string UnresolvableHost = "gft-airplane-mode.invalid";

        private readonly Func<HttpRequestData, HttpResponseData> online;
        private readonly IHttpTransport realNetwork;
        private readonly List<TaskCompletionSource<HttpResponseData>> hanging = new();

        public FlakyNetwork(Func<HttpRequestData, HttpResponseData> online, IHttpTransport realNetwork = null)
        {
            this.online = online ?? throw new ArgumentNullException(nameof(online));
            this.realNetwork = realNetwork;
        }

        public NetworkOutage Outage { get; private set; }

        public bool IsUp => Outage == NetworkOutage.None;

        public List<HttpRequestData> Requests { get; } = new();

        public List<TimeSpan> Timeouts { get; } = new();

        public int HangingCount => hanging.Count(source => !source.Task.IsCompleted);

        public void GoDown(NetworkOutage outage)
        {
            Outage = outage;
        }

        public void Reconnect()
        {
            Outage = NetworkOutage.None;
            var stuck = hanging.ToArray();
            hanging.Clear();
            foreach (var source in stuck)
            {
                source.TrySetResult(HttpResponseData.Timeout());
            }
        }

        public int RequestsTo(string url) => Requests.Count(request => request.Url == url);

        public Task<HttpResponseData> SendAsync(HttpRequestData request, TimeSpan timeout, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Requests.Add(request);
            Timeouts.Add(timeout);
            return Outage switch
            {
                NetworkOutage.None => Task.FromResult(online(request)),
                NetworkOutage.TimedOut => Task.FromResult(HttpResponseData.Timeout()),
                NetworkOutage.Refused => Task.FromResult(HttpResponseData.Failed(RefusedError)),
                NetworkOutage.ServerError => Task.FromResult(HttpResponseData.Completed(ServerErrorStatus, "{\"error\":\"unavailable\"}")),
                NetworkOutage.Unresolvable => SendToUnresolvableHost(request, timeout, cancellation),
                _ => Hang(cancellation)
            };
        }

        public static HttpRequestData AtUnresolvableHost(HttpRequestData request) =>
            new(request.Method, new UriBuilder(request.Url) { Scheme = Uri.UriSchemeHttps, Host = UnresolvableHost, Port = -1 }.Uri.AbsoluteUri, request.Body, request.ContentType, request.Headers);

        private Task<HttpResponseData> SendToUnresolvableHost(HttpRequestData request, TimeSpan timeout, CancellationToken cancellation)
        {
            if (realNetwork == null)
            {
                throw new InvalidOperationException("Looking up an unresolvable host needs a real transport.");
            }

            return realNetwork.SendAsync(AtUnresolvableHost(request), timeout, cancellation);
        }

        internal Task<HttpResponseData> Hang(CancellationToken cancellation)
        {
            var source = new TaskCompletionSource<HttpResponseData>();
            hanging.Add(source);
            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => source.TrySetCanceled(cancellation));
            }

            return source.Task;
        }
    }

    public sealed class FlakyIdTokenProvider : IIdTokenProvider
    {
        private readonly FlakyNetwork network;

        public FlakyIdTokenProvider(FlakyNetwork network)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Uid => FakeFirebase.Uid;

        public bool HasCachedToken { get; set; } = true;

        public List<bool> ForceRefreshCalls { get; } = new();

        public Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            ForceRefreshCalls.Add(forceRefresh);
            if ((HasCachedToken && !forceRefresh) || network.IsUp)
            {
                HasCachedToken = true;
                return Task.FromResult(FakeFirebase.IdToken);
            }

            return network.Outage == NetworkOutage.Hanging ? AwaitRenewalAsync(cancellation) : Task.FromResult<string>(null);
        }

        private async Task<string> AwaitRenewalAsync(CancellationToken cancellation)
        {
            var response = await network.Hang(cancellation);
            return response.IsSuccess ? FakeFirebase.IdToken : null;
        }
    }
}
