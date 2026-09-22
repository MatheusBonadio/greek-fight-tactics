using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Net;

namespace Game.Tests.Support
{
    public enum LoopbackState
    {
        Up,
        Hanging,
        Down
    }

    public sealed class LoopbackHttpServer : IDisposable
    {
        private const string HeadTerminator = "\r\n\r\n";
        private const string LineBreak = "\r\n";
        private const string ContinueResponse = "HTTP/1.1 100 Continue\r\n\r\n";
        private const string ExpectHeader = "Expect";
        private const string ExpectContinue = "100-continue";
        private const string ContentLengthHeader = "Content-Length";
        private const string ContentTypeHeader = "Content-Type";
        private const int MaxHeadBytes = 64 * 1024;
        private const int ReadChunkBytes = 4096;

        private readonly Func<HttpRequestData, HttpResponseData> handle;
        private readonly List<TcpClient> held = new();
        private readonly List<string> received = new();
        private TcpListener listener;

        public LoopbackHttpServer(Func<HttpRequestData, HttpResponseData> handle)
        {
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));
            if (SynchronizationContext.Current == null)
            {
                throw new InvalidOperationException("The server answers on the thread that starts it, so it needs a synchronization context.");
            }

            listener = Listen(0);
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public int Port { get; }

        public string BaseUrl => $"http://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}";

        public LoopbackState State { get; private set; } = LoopbackState.Up;

        public int Connections { get; private set; }

        public IReadOnlyList<string> Received => received;

        public int HeldCount => held.Count;

        public void Hang()
        {
            listener ??= Listen(Port);
            State = LoopbackState.Hanging;
        }

        public void GoDown()
        {
            State = LoopbackState.Down;
            listener?.Stop();
            listener = null;
            ReleaseHeld();
        }

        public void Reconnect()
        {
            listener ??= Listen(Port);
            State = LoopbackState.Up;
            ReleaseHeld();
        }

        public void Dispose()
        {
            GoDown();
        }

        private TcpListener Listen(int port)
        {
            var opened = new TcpListener(IPAddress.Loopback, port);
            opened.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            opened.Start();
            _ = AcceptAsync(opened);
            return opened;
        }

        private async Task AcceptAsync(TcpListener owner)
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await owner.AcceptTcpClientAsync();
                }
                catch (Exception exception) when (exception is ObjectDisposedException or SocketException or InvalidOperationException)
                {
                    return;
                }

                if (!ReferenceEquals(owner, listener))
                {
                    client.Close();
                    return;
                }

                Connections++;
                _ = ServeAsync(client);
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                var (head, leftover) = await ReadHeadAsync(stream);
                if (head == null)
                {
                    client.Close();
                    return;
                }

                var lines = head.Split(new[] { LineBreak }, StringSplitOptions.None);
                var requestLine = lines[0].Split(' ');
                var headers = ParseHeaders(lines);
                if (State != LoopbackState.Up)
                {
                    held.Add(client);
                    return;
                }

                if (headers.TryGetValue(ExpectHeader, out var expect) && string.Equals(expect, ExpectContinue, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteAsync(stream, Encoding.ASCII.GetBytes(ContinueResponse));
                }

                var length = headers.TryGetValue(ContentLengthHeader, out var rawLength) ? int.Parse(rawLength, CultureInfo.InvariantCulture) : 0;
                var body = await ReadBodyAsync(stream, leftover, length);
                headers.TryGetValue(ContentTypeHeader, out var contentType);
                var url = BaseUrl + Uri.UnescapeDataString(requestLine[1]);
                var response = handle(new HttpRequestData(requestLine[0], url, length > 0 ? body : null, contentType, headers));
                received.Add($"{requestLine[0]} {requestLine[1]} {response.Status.ToString(CultureInfo.InvariantCulture)}");
                await WriteAsync(stream, Encode(response));
                client.Close();
            }
            catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
            {
                client.Close();
            }
        }

        private void ReleaseHeld()
        {
            foreach (var client in held)
            {
                client.Close();
            }

            held.Clear();
        }

        private static async Task<(string Head, byte[] Leftover)> ReadHeadAsync(Stream stream)
        {
            var buffer = new MemoryStream();
            var chunk = new byte[ReadChunkBytes];
            while (buffer.Length < MaxHeadBytes)
            {
                var read = await stream.ReadAsync(chunk, 0, chunk.Length);
                if (read == 0)
                {
                    return (null, null);
                }

                buffer.Write(chunk, 0, read);
                var text = Encoding.ASCII.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
                var end = text.IndexOf(HeadTerminator, StringComparison.Ordinal);
                if (end < 0)
                {
                    continue;
                }

                var bodyStart = end + HeadTerminator.Length;
                var leftover = new byte[buffer.Length - bodyStart];
                Array.Copy(buffer.GetBuffer(), bodyStart, leftover, 0, leftover.Length);
                return (text.Substring(0, end), leftover);
            }

            return (null, null);
        }

        private static async Task<string> ReadBodyAsync(Stream stream, byte[] leftover, int length)
        {
            var body = new byte[length];
            var filled = Math.Min(leftover.Length, length);
            Array.Copy(leftover, body, filled);
            while (filled < length)
            {
                var read = await stream.ReadAsync(body, filled, length - filled);
                if (read == 0)
                {
                    throw new IOException("The client closed the connection before sending the whole body.");
                }

                filled += read;
            }

            return Encoding.UTF8.GetString(body);
        }

        private static Dictionary<string, string> ParseHeaders(string[] lines)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 1; index < lines.Length; index++)
            {
                var separator = lines[index].IndexOf(':');
                if (separator > 0)
                {
                    headers[lines[index].Substring(0, separator).Trim()] = lines[index].Substring(separator + 1).Trim();
                }
            }

            return headers;
        }

        private static byte[] Encode(HttpResponseData response)
        {
            var status = response.Outcome == HttpOutcome.Completed ? response.Status : (long)HttpStatusCode.ServiceUnavailable;
            var payload = Encoding.UTF8.GetBytes(response.Body ?? string.Empty);
            var head = $"HTTP/1.1 {status.ToString(CultureInfo.InvariantCulture)} {(HttpStatusCode)status}{LineBreak}" +
                       $"{ContentTypeHeader}: {HttpRequestData.JsonContentType}; charset=utf-8{LineBreak}" +
                       $"{ContentLengthHeader}: {payload.Length.ToString(CultureInfo.InvariantCulture)}{LineBreak}" +
                       $"Connection: close{HeadTerminator}";
            var headBytes = Encoding.ASCII.GetBytes(head);
            var message = new byte[headBytes.Length + payload.Length];
            headBytes.CopyTo(message, 0);
            payload.CopyTo(message, headBytes.Length);
            return message;
        }

        private static async Task WriteAsync(Stream stream, byte[] bytes)
        {
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }
    }
}
