using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BingeX;

// A new HTTP range cancels the obsolete read, without ending the torrent session.
// Only one request uses MonoTorrent's streaming reader at a time.
internal sealed class TorrentHttpServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim reader = new(1);
    private readonly object sync = new();
    private readonly List<Task> requests = [];
    private CancellationTokenSource? active;
    private readonly Stream stream;
    private readonly Func<string?> completedFile;
    private readonly Task accept;
    private readonly string path = "/" + Guid.NewGuid().ToString("N") + "/video";
    public string Url { get; }

    public TorrentHttpServer(Stream stream, Func<string?> completedFile)
    {
        this.stream = stream; this.completedFile = completedFile;
        listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}{path}";
        accept = AcceptAsync();
    }
    private async Task AcceptAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(lifetime.Token);
                lock (sync)
                {
                    requests.RemoveAll(x => x.IsCompleted);
                    requests.Add(ServeAsync(client));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (lifetime.IsCancellationRequested) { }
    }
    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        using (var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
        {
            request.CancelAfter(TimeSpan.FromMinutes(2));
            var token = request.Token;
            try
            {
                using var network = client.GetStream();
                var header = new List<byte>();
                var one = new byte[1];
                while (header.Count < 16384)
                {
                    if (await network.ReadAsync(one, token) == 0) return;
                    header.Add(one[0]);
                    if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
                }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                var first = lines[0].Split(' ');
                if (first.Length != 3 || first[1] != path || first[0] is not ("GET" or "HEAD"))
                { await Headers(network, "404 Not Found", "Content-Length: 0\r\n", token); return; }
                var length = stream.Length;
                long start = 0, end = length - 1;
                var range = lines.FirstOrDefault(x => x.StartsWith("Range:", StringComparison.OrdinalIgnoreCase));
                if (range != null && !TryRange(range[6..].Trim(), length, out start, out end))
                { await Headers(network, "416 Range Not Satisfiable", $"Content-Range: bytes */{length}\r\nContent-Length: 0\r\n", token); return; }
                await Headers(network, range == null ? "200 OK" : "206 Partial Content",
                    $"Accept-Ranges: bytes\r\nContent-Type: application/octet-stream\r\nContent-Length: {end - start + 1}\r\n" +
                    (range == null ? "" : $"Content-Range: bytes {start}-{end}/{length}\r\n"), token);
                if (first[0] == "HEAD") return;
                request.CancelAfter(Timeout.InfiniteTimeSpan);
                lock (sync) { active?.Cancel(); active = request; }
                await reader.WaitAsync(token);
                try
                {
                    // Completed files no longer need the torrent reader or live peers.
                    using var local = completedFile() is { } file
                        ? new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous)
                        : null;
                    var input = (Stream?)local ?? stream;
                    input.Seek(start, SeekOrigin.Begin);
                    var remaining = end - start + 1;
                    var buffer = new byte[65536];
                    while (remaining > 0)
                    {
                        var count = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
                        if (count == 0) break;
                        await network.WriteAsync(buffer.AsMemory(0, count), token);
                        remaining -= count;
                    }
                }
                finally { reader.Release(); }
            }
            catch (Exception error) when (error is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
            finally { lock (sync) { if (ReferenceEquals(active, request)) active = null; } }
        }
    }
    private static Task Headers(NetworkStream network, string status, string headers, CancellationToken token) =>
        network.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n{headers}Connection: close\r\n\r\n"), token).AsTask();
    internal static bool TryRange(string value, long length, out long start, out long end)
    {
        start = 0; end = length - 1;
        if (!value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || length <= 0) return false;
        var parts = value[6..].Split('-');
        if (parts.Length != 2) return false;
        if (parts[0].Length == 0)
        {
            if (!long.TryParse(parts[1], out var suffix) || suffix <= 0) return false;
            start = Math.Max(0, length - suffix); return true;
        }
        if (!long.TryParse(parts[0], out start) || start < 0 || start >= length) return false;
        if (parts[1].Length > 0 && !long.TryParse(parts[1], out end)) return false;
        end = Math.Min(end, length - 1);
        return end >= start;
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel(); listener.Stop();
        await accept;
        Task[] pending;
        lock (sync) { active?.Cancel(); pending = requests.ToArray(); }
        await Task.WhenAll(pending);
        reader.Dispose(); lifetime.Dispose();
    }
}
