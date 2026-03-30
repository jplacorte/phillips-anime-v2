using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeStreamer.Services
{
    public class LocalProxyServer : IDisposable
    {
        private TcpListener? _listener;
        private readonly HttpClient _httpClient;
        private CancellationTokenSource? _cts;
        private int _port;

        // Use a 1 Megabyte buffer for video streaming (Default is 80KB)
        private const int BufferSize = 1024 * 1024;

        public int Port => _port;

        public LocalProxyServer()
        {
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        }

        public void Start(int port = 8080)
        {
            _port = port;

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, _port);
                _listener.Start();
            }
            catch
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            }

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        }

        private async Task AcceptConnectionsAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await _listener!.AcceptTcpClientAsync(token);

                    // OPTIMIZATION: Tweak Socket settings for high-bandwidth media streaming
                    client.NoDelay = true; // Disable Nagle's algorithm to reduce latency
                    client.SendBufferSize = BufferSize;
                    client.ReceiveBufferSize = BufferSize;

                    _ = Task.Run(() => HandleClientAsync(client, token));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Proxy] Listener error: {ex.Message}"); }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                using var networkStream = client.GetStream();
                using var reader = new StreamReader(networkStream, Encoding.ASCII, leaveOpen: true);

                var requestLine = await reader.ReadLineAsync(token);
                if (string.IsNullOrEmpty(requestLine)) return;

                var match = Regex.Match(requestLine, @"^(GET|HEAD)\s+([^\s]+)\s+HTTP");
                if (!match.Success) return;

                var httpMethod = match.Groups[1].Value.ToUpperInvariant();
                var pathAndQuery = match.Groups[2].Value;

                var uri = new Uri($"http://localhost{pathAndQuery}");
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                string? fileId = queryParams["id"];
                string? accessToken = queryParams["token"];

                if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(accessToken))
                {
                    await WriteResponseAsync(networkStream, "HTTP/1.1 400 Bad Request\r\n\r\n");
                    return;
                }

                string? rangeHeader = null;
                while (true)
                {
                    var headerLine = await reader.ReadLineAsync(token);
                    if (string.IsNullOrEmpty(headerLine)) break;
                    if (headerLine.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                        rangeHeader = headerLine.Substring(6).Trim();
                }

                var targetUrl = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media&acknowledgeAbuse=true";
                var targetHttpMethod = httpMethod == "HEAD" ? HttpMethod.Head : HttpMethod.Get;

                using var httpRequest = new HttpRequestMessage(targetHttpMethod, targetUrl);
                httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");

                if (rangeHeader != null)
                    httpRequest.Headers.TryAddWithoutValidation("Range", rangeHeader);

                // HttpCompletionOption.ResponseHeadersRead is crucial here (which you already had!)
                using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, token);

                await WriteResponseAsync(networkStream, $"HTTP/1.1 {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}\r\n");

                foreach (var header in httpResponse.Headers)
                {
                    if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
                    if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)) continue;
                    if (header.Key.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase)) continue;
                    await WriteResponseAsync(networkStream, $"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                }

                foreach (var header in httpResponse.Content.Headers)
                {
                    if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)) continue;
                    if (header.Key.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase)) continue;
                    await WriteResponseAsync(networkStream, $"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                }

                await WriteResponseAsync(networkStream, "Accept-Ranges: bytes\r\n");
                await WriteResponseAsync(networkStream, "Connection: close\r\n\r\n");

                if (httpMethod == "GET" && httpResponse.IsSuccessStatusCode)
                {
                    using var inStream = await httpResponse.Content.ReadAsStreamAsync(token);

                    // OPTIMIZATION: Use a much larger buffer for the stream copy
                    await inStream.CopyToAsync(networkStream, BufferSize, token);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Proxy] Error streaming: {ex.Message}"); }
            finally { client.Close(); }
        }

        private async Task WriteResponseAsync(NetworkStream stream, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        public void Dispose()
        {
            Stop();
            _httpClient?.Dispose();
        }
    }
}