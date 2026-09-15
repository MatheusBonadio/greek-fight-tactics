using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Net
{
    public interface IHttpTransport
    {
        Task<HttpResponseData> SendAsync(HttpRequestData request, TimeSpan timeout, CancellationToken cancellation);
    }
}
