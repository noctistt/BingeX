using MonoTorrent;
using MonoTorrent.Client;
using System.Net;
using System.Text.RegularExpressions;

namespace BingeX;

public record TorrentStatus(string Stage, double DownloadedPercent, long DownloadRate, int Peers, bool Waiting, bool HasMetadata = true, int ElapsedSeconds = 0, string Discovery = "", long BufferedBytes = 0, double? BufferedSeconds = null, long MeasuredRate = 0, long? RequiredRate = null, long StartupBuffered = 0, long StartupTarget = 0);

public sealed class TorrentPlayback : IAsyncDisposable
{
    public const long CacheLimit = 32L * 1024 * 1024 * 1024;
    private readonly string root;
    private readonly string? cacheHash;
    private readonly FileStream cacheLease;
    private TorrentHttpServer? server;
    public string SessionDirectory { get; }
    private readonly CancellationTokenSource lifetime = new();
    private readonly ClientEngine engine;
    private readonly TorrentWriteGate writeGate;
    private readonly bool localOnly;
    private readonly System.Diagnostics.Stopwatch startup = new();
    private string stage = "Finding peers and torrent information…";
    private bool prepared;
    private int trackerReplies;
    private ITorrentManagerFile? selectedFile;
    private long durationMs;
    private readonly DeliveryRate delivery = new();
    private long measuredRate;
    private long startupBuffered;
    private long startupTarget;
    public void UpdateDuration(long milliseconds) => Interlocked.Exchange(ref durationMs, Math.Max(0, milliseconds));
    private TorrentManager? manager;
    private Stream? stream;
    private Task? monitor;
    private Task<Stream>? opening;
    private DateTime lastRead = DateTime.UtcNow;
    private int reading;
    private bool disposed;
    public string FileName { get; private set; } = "";
    public string? Failure { get; private set; }
    public TorrentPlayback(string dataDirectory, bool localOnly = false, string? infoHash = null)
    {
        this.localOnly = localOnly;
        root = Path.GetFullPath(Path.Combine(dataDirectory, "torrent-cache-v1"));
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("The torrent cache must be a regular folder.");
        if (infoHash != null && !Regex.IsMatch(infoHash, "^[a-fA-F0-9]{40}$")) throw new ArgumentException("Invalid torrent hash.", nameof(infoHash));
        cacheHash = infoHash?.ToLowerInvariant();
        SessionDirectory = Path.Combine(root, cacheHash == null ? "session-" + Guid.NewGuid().ToString("N") : "torrent-" + cacheHash);
        Directory.CreateDirectory(SessionDirectory);
        if ((File.GetAttributes(SessionDirectory) & FileAttributes.ReparsePoint) != 0) throw new IOException("The torrent cache must be a regular folder.");
        cacheLease = new FileStream(Path.Combine(SessionDirectory, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var settings = new EngineSettingsBuilder
        {
            CacheDirectory = Path.Combine(SessionDirectory, "engine"),
            DiskCacheBytes = 8 * 1024 * 1024,
            MaximumConnections = 60,
            MaximumHalfOpenConnections = 8,
            MaximumUploadRate = 256 * 1024,
            AllowPortForwarding = false,
            AllowLocalPeerDiscovery = false,
            AutoSaveLoadDhtCache = !localOnly,
            AutoSaveLoadFastResume = cacheHash != null,
            AutoSaveLoadMagnetLinkMetadata = cacheHash != null,
            DhtEndPoint = localOnly ? null : new IPEndPoint(IPAddress.Any, 0)
        };
        settings.ListenEndPoints.Clear();
        settings.ListenEndPoints.Add("ipv4", new IPEndPoint(localOnly ? IPAddress.Loopback : IPAddress.Any, 0));
        TorrentWriteGate? gate = null;
        var factories = Factories.Default.WithPieceWriterCreator(count => gate = new TorrentWriteGate(Factories.Default.CreatePieceWriter(count), Path.Combine(SessionDirectory, "video"), lifetime.Token));
        try
        {
            engine = new ClientEngine(settings.ToSettings(), factories);
            writeGate = gate ?? throw new InvalidOperationException("The torrent cache writer could not start.");
        }
        catch { cacheLease.Dispose(); throw; }
    }
    public Task<Stream> OpenAsync(TorrentSource source, IProgress<TorrentStatus> progress, CancellationToken token, Torrent? fixture = null, int? durationSeconds = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (cacheHash != null && !cacheHash.Equals(source.InfoHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("The cached torrent does not match this source.");
        if (opening != null) throw new InvalidOperationException("This torrent session has already been started.");
        return opening = OpenCoreAsync(source, progress, token, fixture, durationSeconds);
    }
    private async Task<Stream> OpenCoreAsync(TorrentSource source, IProgress<TorrentStatus> progress, CancellationToken token, Torrent? fixture, int? durationSeconds)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var work = timeout.Token;
        if (durationSeconds is > 0) UpdateDuration(durationSeconds.Value * 1000L);
        progress.Report(new("Finding peers and torrent information…", 0, 0, 0, true, false, 0, "Starting peer discovery"));
        try
        {
            startup.Start();
            var trackers = source.Trackers;
            if (!localOnly && source.TorrentFile == null && fixture == null && trackers.Length == 0)
                trackers = ["udp://tracker.opentrackr.org:1337/announce", "udp://tracker.torrent.eu.org:451/announce", "http://tracker2.dler.org:80/announce"];
            var magnet = new MagnetLink(InfoHash.FromHex(source.InfoHash), "", trackers);
            var torrent = fixture ?? (source.TorrentFile is { } torrentPath ? Torrent.Load(torrentPath) : null);
            manager = torrent == null
                ? await engine.AddStreamingAsync(magnet, Path.Combine(SessionDirectory, "video"), new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings())
                : await engine.AddStreamingAsync(torrent, Path.Combine(SessionDirectory, "video"), new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings());
            manager.TrackerManager.AnnounceComplete += (_, reply) => { if (reply.Successful) Interlocked.Increment(ref trackerReplies); };
            monitor = MonitorAsync(progress);
            await manager.StartAsync();
            if (source.Peers.Length > 0) await manager.AddPeersAsync(source.Peers.Select(x => new PeerInfo(new Uri(x))));
            await manager.WaitForMetadataAsync(work);
            torrent = manager.Torrent!;
            if (torrent.InfoHashes.V1?.ToHex().Equals(source.InfoHash, StringComparison.OrdinalIgnoreCase) != true) throw new IOException("Torrent metadata does not match the selected source.");
            ValidateTorrent(torrent);
            TorrentCache.Reserve(root, SessionDirectory, torrent.Size);
            var drive = new DriveInfo(Path.GetPathRoot(SessionDirectory)!);
            // Sparse file lengths include holes that still need disk space.
            long alreadyStored = 0;
            for (var index = 0; index < manager.Bitfield.Length; index++)
                if (manager.Bitfield[index]) alreadyStored += Math.Min(torrent.PieceLength, torrent.Size - (long)index * torrent.PieceLength);
            if (drive.AvailableFreeSpace < Math.Max(0, torrent.Size - alreadyStored) + 64L * 1024 * 1024) throw new IOException("There is not enough free space for this torrent. Choose a smaller source or free some disk space.");
            var file = SelectFile(manager.Files, source.FileIndex);
            foreach (var item in manager.Files)
                await manager.SetFilePriorityAsync(item, ReferenceEquals(item, file) ? Priority.Normal : Priority.DoNotDownload);
            selectedFile = file;
            FileName = file.Path;
            work.ThrowIfCancellationRequested();
            stage = "Buffering the start of the video…";
            writeGate.Approve();
            // Use the cancellable reader below rather than prebuffering in MonoTorrent (also supports tiny clips).
            var raw = await manager.StreamProvider!.CreateStreamAsync(file, false, work);
            stream = new CancellableTorrentStream(raw, lifetime.Token, waiting =>
            {
                Interlocked.Exchange(ref reading, waiting ? 1 : 0);
                if (!waiting) lastRead = DateTime.UtcNow;
            }, error => Failure = error);
            progress.Report(new("Buffering the start of the video…", manager.PartialProgress, manager.Monitor.DownloadRate, manager.OpenConnections, true));
            // Seeking explicitly activates priority for the selected file, including episodes deep in a pack.
            raw.Seek(Math.Max(0, raw.Length - 1), SeekOrigin.Begin);
            await raw.ReadExactlyAsync(new byte[1], 0, 1, work);
            raw.Seek(0, SeekOrigin.Begin);
            startupTarget = TorrentBufferPolicy.StartupTarget(stream.Length, durationSeconds);
            var buffer = new byte[256 * 1024];
            while (startupBuffered < startupTarget)
            {
                var count = (int)Math.Min(buffer.Length, startupTarget - startupBuffered);
                await raw.ReadExactlyAsync(buffer, 0, count, work);
                Interlocked.Add(ref startupBuffered, count);
                progress.Report(new("Building a playback buffer…", manager.PartialProgress, manager.Monitor.DownloadRate, manager.OpenConnections, true,
                    StartupBuffered: startupBuffered, StartupTarget: startupTarget, MeasuredRate: measuredRate));
            }
            raw.Seek(0, SeekOrigin.Begin);
            work.ThrowIfCancellationRequested();
            prepared = true;
            progress.Report(new("Ready to play", manager.PartialProgress, manager.Monitor.DownloadRate, manager.OpenConnections, false));
            return stream;
        }
        catch (OperationCanceledException) when (Failure != null) { throw new IOException(Failure); }
        catch (OperationCanceledException) when (!linked.IsCancellationRequested)
        { throw new IOException("Torrent startup timed out. No playable data arrived. Try another source; if every source fails, peer connections may be blocked on this network."); }
    }
    public static ITorrentManagerFile SelectFile(IList<ITorrentManagerFile> files, int? fileIndex)
    {
        // Preserve the provider's original file ordering; an index must never silently select another episode.
        if (fileIndex is { } index)
        {
            if (index >= files.Count || !IsVideo(files[index].Path)) throw new IOException("The add-on selected a missing or unsupported video file. Try another source.");
            return files[index];
        }
        return files.Where(x => IsVideo(x.Path)).OrderByDescending(x => x.Length).FirstOrDefault()
            ?? throw new IOException("This torrent does not contain a supported video file.");
    }
    private static bool IsVideo(string path) => new[] { ".mkv", ".mp4", ".avi", ".mov", ".webm", ".m4v", ".ts", ".m2ts", ".mpg", ".mpeg", ".wmv", ".flv", ".ogv" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static void ValidateTorrent(Torrent torrent)
    {
        if (torrent.Size <= 0 || torrent.Size > CacheLimit) throw new IOException("This torrent exceeds the 32 GB cache limit. Choose a smaller source or an individual-episode torrent.");
        foreach (var file in torrent.Files)
        {
            var parts = file.Path.Replace('\\', '/').Split('/');
            if (Path.IsPathRooted(file.Path) || parts.Any(x => x is ".." or "." || x.Contains(':'))) throw new IOException("This torrent contains an unsafe file path.");
        }
    }
    private async Task MonitorAsync(IProgress<TorrentStatus> progress)
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                await Task.Delay(1000, lifetime.Token);
                var current = manager;
                if (current == null) continue;
                if (current.State == TorrentState.Error) { Failure = "Torrent download failed. Check free disk space or try another source."; lifetime.Cancel(); break; }
                if (!current.HasMetadata)
                {
                    var elapsed = (int)startup.Elapsed.TotalSeconds;
                    var discovery = $"{Volatile.Read(ref trackerReplies)} tracker replies" + (localOnly ? "" : $" · DHT {engine.Dht.State} · {engine.Dht.NodeCount} discovery nodes");
                    progress.Report(new(current.OpenConnections > 0 ? "Connected to peers; requesting torrent information…" : elapsed >= 15 ? "Still searching for reachable peers…" : "Finding peers and torrent information…", 0, current.Monitor.DownloadRate, current.OpenConnections, true, false, elapsed, discovery));
                    continue;
                }
                measuredRate = delivery.Add(startup.Elapsed.TotalSeconds, current.Monitor.DataBytesReceived);
                var file = selectedFile;
                var position = stream?.Position ?? 0;
                var contiguous = file == null ? 0 : TorrentBufferPolicy.ContiguousBytes(file.OffsetInTorrent, file.Length, position, current.Torrent!.PieceLength, index => current.Bitfield[index]);
                var duration = Interlocked.Read(ref durationMs);
                var secondsAhead = file == null ? null : TorrentBufferPolicy.EstimateSeconds(contiguous, file.Length, duration);
                long? required = file != null && duration > 0 ? (long)(file.Length * 1000d / duration) : null;
                var waiting = !prepared || Volatile.Read(ref reading) != 0;
                if (waiting && DateTime.UtcNow - lastRead > TimeSpan.FromMinutes(2)) { Failure = "The torrent stopped delivering data. Try another source with more peers."; lifetime.Cancel(); break; }
                progress.Report(new(!prepared ? stage : waiting ? "Buffering video…" : "Torrent connected", current.PartialProgress, current.Monitor.DownloadRate, current.OpenConnections, waiting, BufferedBytes: contiguous, BufferedSeconds: secondsAhead, MeasuredRate: measuredRate, RequiredRate: required, StartupBuffered: startupBuffered, StartupTarget: startupTarget));
            }
        }
        catch (OperationCanceledException) { }
    }
    public void Cancel() => lifetime.Cancel();
    public string StartHttpServer()
    {
        if (!prepared || stream == null) throw new InvalidOperationException("The video is not ready.");
        return (server ??= new TorrentHttpServer(stream, () =>
        {
            var file = selectedFile;
            if (file == null || manager == null) return null;
            var first = (int)(file.OffsetInTorrent / manager.Torrent!.PieceLength);
            var last = (int)((file.OffsetInTorrent + file.Length - 1) / manager.Torrent.PieceLength);
            for (var i = first; i <= last; i++) if (!manager.Bitfield[i]) return null;
            return file.FullPath;
        })).Url;
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        Cancel();
        if (opening != null) { try { await opening; } catch { /* The caller reports startup errors; disposal still releases resources. */ } }
        if (monitor != null) await monitor;
        if (server != null) await server.DisposeAsync();
        stream?.Dispose();
        try { if (manager != null) await manager.StopAsync(TimeSpan.FromSeconds(2)); }
        finally
        {
            engine.Dispose(); cacheLease.Dispose();
            if (cacheHash == null) TorrentCache.Delete(root, SessionDirectory);
            else Directory.SetLastWriteTimeUtc(SessionDirectory, DateTime.UtcNow);
            lifetime.Dispose();
        }
    }
    public static void CleanOldSessions(string dataDirectory)
    {
        var root = Path.GetFullPath(Path.Combine(dataDirectory, "torrent-cache-v1"));
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return;
        foreach (var path in Directory.EnumerateDirectories(root).Where(x => Regex.IsMatch(Path.GetFileName(x), "^session-[a-f0-9]{32}$"))) TorrentCache.Delete(root, path);
    }
}

internal sealed class CancellableTorrentStream(Stream inner, CancellationToken token, Action<bool> waiting, Action<string> failure) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override int Read(byte[] buffer, int offset, int count)
    {
        waiting(true);
        try { return inner.ReadAsync(buffer, offset, count, token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { return 0; }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException) { failure("Torrent data could not be read. Try another source or check disk space."); return 0; }
        finally { waiting(false); }
    }
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken);
        waiting(true);
        try { return await inner.ReadAsync(buffer, linked.Token); }
        finally { waiting(false); }
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}
