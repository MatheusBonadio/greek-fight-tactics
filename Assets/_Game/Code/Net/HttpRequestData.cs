using System;
using System.Collections.Generic;

namespace Game.Net
{
    public sealed class HttpRequestData
    {
        public const string JsonContentType = "application/json";
        public const string FormContentType = "application/x-www-form-urlencoded";

        public HttpRequestData(string method, string url, string body, string contentType, IReadOnlyDictionary<string, string> headers = null)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                throw new ArgumentException("HTTP method is required.", nameof(method));
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL is required.", nameof(url));
            }

            Method = method;
            Url = url;
            Body = body;
            ContentType = contentType;
            Headers = headers ?? new Dictionary<string, string>();
        }

        public string Method { get; }

        public string Url { get; }

        public string Body { get; }

        public string ContentType { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
