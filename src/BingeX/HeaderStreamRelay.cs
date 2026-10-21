using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BingeX;

/// <summary>Private loopback routes for player requests carrying provider headers.</summary>
public sealed class HeaderStreamRelay : IAsyncDisposable
{
    private record Route(Uri Url, Uri Origin, IReadOnlyDictionary<string, string> Headers);
    private readonly ConcurrentDictionary<string, Route> routes = new();
    private readonly ConcurrentDictionary<long, Task> pending = new();
    private readonly ConcurrentDictionary<long, HttpListenerContext> active = new();
    private readonly HttpListener listener = new();
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly CancellationTokenSource lifetime = new();
    private readonly string prefix;
    private readonly Task accepting;
    private long sequence;
    private int disposed;
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        { "Host", "Connection", "Content-Length", "Transfer-Encoding", "Accept-Encoding", "Range", "If-Range", "Proxy-Authorization", "Proxy-Connection", "Upgrade", "Trailer", "TE" };
    public HeaderStreamRelay()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var address = $"http://127.0.0.1:{port}/";
        prefix = address + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "/";
        listener.Prefixes.Add(address); listener.Start();
        accepting = Accept();
    }
    public static bool ValidHeaders(IReadOnlyDictionary<string, string> headers) => headers.Count <= 64 && headers.All(x =>
        Regex.IsMatch(x.Key, "^[!#$%&'*+.^_`|~0-9A-Za-z-]{1,128}$") && x.Value.Length <= 16384 && !x.Value.Any(c => c is '\r' or '\n' or '\0'));
    public string Register(string url, IReadOnlyDictionary<string, string> headers)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0 || !ValidHeaders(headers))
            throw new ArgumentException("Invalid stream address or headers.");
        return Add(new(uri, uri, new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase)));
    }
    private string Add(Route route)
    {
        if (route.Url.Scheme is not ("http" or "https") || route.Url.UserInfo.Length > 0) throw new InvalidOperationException("Unsupported playlist address.");
        // Stable names bound route growth when a live playlist is refreshed.
        var extension = Path.GetExtension(route.Url.AbsolutePath).ToLowerInvariant();
        if (extension is not (".m3u8" or ".ts" or ".mp4" or ".m4s" or ".aac" or ".m4a" or ".key" or ".vtt")) extension = "";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(route.Origin + "\n" + route.Url + "\n" + string.Join('\n', route.Headers.OrderBy(x => x.Key).Select(x => x.Key + ":" + x.Value))))) + extension;
        if (routes.Count > 100000 && !routes.ContainsKey(key)) throw new IOException("Playlist route limit reached.");
        routes[key] = route; return prefix + key;
    }
    private async Task Accept()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(lifetime.Token);
                var id = Interlocked.Increment(ref sequence);
                active[id] = context;
                var task = Serve(context); pending[id] = task;
                _ = task.ContinueWith(_ => { active.TryRemove(id, out var ignoredContext); pending.TryRemove(id, out var ignoredTask); }, TaskScheduler.Default);
            }
        }
        catch (Exception) when (lifetime.IsCancellationRequested) { }
    }
    private static bool SameOrigin(Uri a, Uri b) => a.Scheme == b.Scheme && a.Host.Equals(b.Host, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port;
    private async Task Serve(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var target = request.Url!.AbsoluteUri;
            if (!target.StartsWith(prefix, StringComparison.Ordinal) || !routes.TryGetValue(target[prefix.Length..], out var route)) { context.Response.StatusCode = 404; return; }
            if (request.HttpMethod is not ("GET" or "HEAD")) { context.Response.StatusCode = 405; return; }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var token = timeout.Token;
            var url = route.Url;
            HttpResponseMessage? upstream = null;
            try
            {
                for (var redirect = 0; redirect <= 5; redirect++)
                {
                    using var message = new HttpRequestMessage(new HttpMethod(request.HttpMethod), url);
                    foreach (var header in route.Headers)
                    {
                        if (Reserved.Contains(header.Key)) continue;
                        // Credentials stay on the origin that explicitly supplied them.
                        if ((header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)) && !SameOrigin(url, route.Origin)) continue;
                        message.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    message.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");
                    foreach (var name in new[] { "Range", "If-Range" })
                        if (request.Headers[name] is { } value) message.Headers.TryAddWithoutValidation(name, value);
                    upstream = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
                    if ((int)upstream.StatusCode is 301 or 302 or 303 or 307 or 308 && upstream.Headers.Location is { } location)
                    {
                        var next = new Uri(url, location);
                        if (next.Scheme is not ("http" or "https") || next.UserInfo.Length > 0) throw new IOException("Unsupported redirect.");
                        upstream.Dispose(); upstream = null; url = next; continue;
                    }
                    break;
                }
                if (upstream == null) throw new IOException("Too many redirects.");
                var response = context.Response;
                response.StatusCode = (int)upstream.StatusCode;
                var type = upstream.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var playlist = url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || type.Contains("mpegurl", StringComparison.OrdinalIgnoreCase);
                response.ContentType = upstream.Content.Headers.ContentType?.ToString() ?? type;
                foreach (var name in new[] { "Accept-Ranges", "ETag", "Last-Modified" })
                    if (upstream.Headers.TryGetValues(name, out var values) || upstream.Content.Headers.TryGetValues(name, out values)) response.Headers[name] = string.Join(", ", values);
                if (upstream.Content.Headers.ContentRange is { } range) response.Headers["Content-Range"] = range.ToString();
                if (playlist && upstream.IsSuccessStatusCode && request.HttpMethod == "GET")
                {
                    await upstream.Content.LoadIntoBufferAsync(8 * 1024 * 1024, token);
                    var text = await upstream.Content.ReadAsStringAsync(token);
                    string Rewrite(string relative) => relative.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? relative : Add(route with { Url = new Uri(url, relative) });
                    var rewritten = string.Join('\n', text.Replace("\r\n", "\n").Split('\n').Select(line =>
                        line.StartsWith('#') ? Regex.Replace(line, "URI=\"([^\"]+)\"", m => "URI=\"" + Rewrite(m.Groups[1].Value) + "\"") : string.IsNullOrWhiteSpace(line) ? line : Rewrite(line.Trim())));
                    var bytes = Encoding.UTF8.GetBytes(rewritten);
                    response.ContentLength64 = bytes.Length;
                    await response.OutputStream.WriteAsync(bytes, token);
                }
                else
                {
                    if (upstream.Content.Headers.ContentLength is { } length) response.ContentLength64 = length;
                    if (request.HttpMethod == "GET") await upstream.Content.CopyToAsync(response.OutputStream, token);
                }
            }
            finally { upstream?.Dispose(); }
        }
        catch (Exception)
        {
            try { context.Response.StatusCode = 502; } catch { }
        }
        finally { try { context.Response.Close(); } catch { } }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel(); listener.Close();
        foreach (var context in active.Values) { try { context.Response.Abort(); } catch { } }
        http.CancelPendingRequests();
        await accepting;
        try { await Task.WhenAll(pending.Values).WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { /* Closing the client also releases outstanding upstream sockets. */ }
        http.Dispose(); lifetime.Dispose();
    }
}
