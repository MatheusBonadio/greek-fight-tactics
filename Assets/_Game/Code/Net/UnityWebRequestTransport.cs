using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Net
{
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        private const string TimeoutError = "Request timeout";

        public async Task<HttpResponseData> SendAsync(HttpRequestData request, TimeSpan timeout, CancellationToken cancellation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellation.ThrowIfCancellationRequested();
            using var webRequest = new UnityWebRequest(request.Url, request.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds))
            };

            if (request.Body != null)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body)) { contentType = request.ContentType };
            }

            foreach (var header in request.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            await Awaitable.FromAsyncOperation(webRequest.SendWebRequest(), cancellation);

            return webRequest.result switch
            {
                UnityWebRequest.Result.Success or UnityWebRequest.Result.ProtocolError => HttpResponseData.Completed(webRequest.responseCode, webRequest.downloadHandler.text),
                _ when string.Equals(webRequest.error, TimeoutError, StringComparison.OrdinalIgnoreCase) => HttpResponseData.Timeout(),
                _ => HttpResponseData.Failed(webRequest.error)
            };
        }
    }
}
