using BingeX;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.BEncoding;
using MonoTorrent.PieceWriter;
using ReusableTasks;
using LibVLCSharp.Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

internal static class TorrentChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static async Task Run(string directory, string? video)
    {
        using var json = JsonDocument.Parse("""{"infoHash":"ABCDEF0123456789ABCDEF0123456789ABCDEF01","fileIdx":1,"sources":["tracker:udp://tracker.example:80/announce","peer:127.0.0.1:1234","tracker:file:///wrong","dht:ignored"]}""");
        var choice = MediaLibrary.ParseStream("Fixture", json.RootElement);
        Check(choice.UnsupportedReason == null && choice.Torrent is { FileIndex: 1 } && choice.Torrent.Trackers.Length == 1 && choice.Torrent.Peers.Length == 1, "Torrent add-on fields and supported tracker/peer hints must survive parsing.");
        using var invalid = JsonDocument.Parse("""{"infoHash":"abcdef0123456789abcdef0123456789abcdef01","fileIdx":-1}""");
        Check(MediaLibrary.ParseStream("Fixture", invalid.RootElement).UnsupportedReason != null, "Invalid episode indexes must stay disabled.");
        using var wrongType = JsonDocument.Parse("""{"infoHash":"abcdef0123456789abcdef0123456789abcdef01","fileIdx":"bad"}""");
        Check(MediaLibrary.ParseStream("Fixture", wrongType.RootElement).UnsupportedReason != null, "Malformed index types must produce an explanation, not hide the provider.");
        var bigSize = TorrentPlayback.CacheLimit + 1;
        var pieceLength = 16 * 1024 * 1024;
        var oversized = new BEncodedDictionary { ["info"] = new BEncodedDictionary { ["name"] = new BEncodedString("large.mkv"), ["length"] = new BEncodedNumber(bigSize), ["piece length"] = new BEncodedNumber(pieceLength), ["pieces"] = new BEncodedString(new byte[((bigSize + pieceLength - 1) / pieceLength) * 20]) } };
        var tooLarge = false;
        try { TorrentPlayback.ValidateTorrent(Torrent.Load(oversized)); } catch (IOException) { tooLarge = true; }
        Check(tooLarge, "Oversized torrents must be rejected before downloading their files.");
        Check(TorrentHttpServer.TryRange("bytes=10-19", 100, out var rangeStart, out var rangeEnd) && rangeStart == 10 && rangeEnd == 19, "Explicit byte ranges must preserve bounds.");
        Check(TorrentHttpServer.TryRange("bytes=-20", 100, out rangeStart, out rangeEnd) && rangeStart == 80 && rangeEnd == 99, "Suffix ranges must be supported.");
        Check(!TorrentHttpServer.TryRange("bytes=100-", 100, out _, out _) && !TorrentHttpServer.TryRange("bytes=1-2,4-5", 100, out _, out _), "Invalid and multipart ranges must be rejected.");
        using (var missing = new MissingStartStream())
        await using (var server = new TorrentHttpServer(missing, () => null))
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
        {
            using var oldRequest = new HttpRequestMessage(HttpMethod.Get, server.Url);
            oldRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 99);
            var obsolete = http.SendAsync(oldRequest);
            await missing.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using var nextRequest = new HttpRequestMessage(HttpMethod.Get, server.Url);
            nextRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(1000, 1099);
            using var response = await http.SendAsync(nextRequest);
            Check((await response.Content.ReadAsByteArrayAsync()).Length == 100, "A new seek must cancel a blocked old read and deliver the new range.");
            try { using var ignored = await obsolete; } catch (HttpRequestException) { }
        }
        using (var stopRead = new CancellationTokenSource())
        using (var blocked = new CancellableTorrentStream(new NeverReadyStream(), stopRead.Token, _ => { }, _ => { }))
        {
            var read = Task.Run(() => blocked.Read(new byte[100], 0, 100));
            stopRead.Cancel();
            Check(await read.WaitAsync(TimeSpan.FromSeconds(3)) == 0, "Cancellation must release blocked native decoder reads promptly.");
        }
        var cacheData = Path.Combine(directory, "cache-check");
        var root = Path.Combine(cacheData, "torrent-cache-v1");
        var old = Path.Combine(root, "session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "part.bin"), "cached");
        Directory.CreateDirectory(Path.Combine(root, "keep-this-folder"));
        TorrentPlayback.CleanOldSessions(cacheData);
        Check(!Directory.Exists(old) && Directory.Exists(Path.Combine(root, "keep-this-folder")), "Cleanup must delete only owned torrent sessions.");
        var retained = Path.Combine(root, "torrent-" + new string('c', 40));
        var nextCache = Path.Combine(root, "torrent-" + new string('d', 40));
        Directory.CreateDirectory(retained); Directory.CreateDirectory(nextCache);
        File.WriteAllText(Path.Combine(retained, "reserved.txt"), TorrentPlayback.CacheLimit.ToString());
        TorrentPlayback.CleanOldSessions(cacheData);
        Check(Directory.Exists(retained), "Legacy cleanup must preserve persistent caches.");
        using (var lease = new FileStream(Path.Combine(retained, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var full = false;
            try { TorrentCache.Reserve(root, nextCache, 1); } catch (IOException) { full = true; }
            Check(full && Directory.Exists(retained), "Cache eviction must never delete an active video.");
        }
        TorrentCache.Reserve(root, nextCache, 1);
        Check(!Directory.Exists(retained) && Directory.Exists(nextCache), "A full cache must evict inactive downloads to respect its total budget.");
        Console.WriteLine("PASS: torrent source parsing, episode index validation, tracker/peer filtering and scoped cache cleanup.");
        if (video == null) return;
        var seedPath = Path.Combine(directory, "seed"); Directory.CreateDirectory(seedPath);
        var mediaPath = Path.Combine(seedPath, "video.mkv"); File.Copy(Path.GetFullPath(video), mediaPath);
        File.WriteAllText(Path.Combine(seedPath, "notes.txt"), "ignore this file");
        var seedPort = FreePort();
        await using var tracker = new LocalTracker(seedPort);
        var creator = new TorrentCreator(TorrentType.V1Only) { PieceLength = 64 * 1024, Announce = tracker.Url };
        var torrentData = await creator.CreateAsync(new TorrentFileSource(seedPath));
        var torrent = Torrent.Load(torrentData);
        var exported = Environment.GetEnvironmentVariable("BINGEX_TORRENT_FIXTURE");
        if (!string.IsNullOrEmpty(exported))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exported))!);
            File.WriteAllBytes(exported, torrentData.Encode());
        }
        var settings = new EngineSettingsBuilder
        {
            CacheDirectory = Path.Combine(directory, "seed-meta"), AllowPortForwarding = false,
            AllowLocalPeerDiscovery = false, DhtEndPoint = null, MaximumUploadRate = 2 * 1024 * 1024,
            AutoSaveLoadDhtCache = false, AutoSaveLoadFastResume = false, AutoSaveLoadMagnetLinkMetadata = false
        };
        settings.ListenEndPoints.Clear(); settings.ListenEndPoints.Add("ipv4", new IPEndPoint(IPAddress.Loopback, seedPort));
        using var seeder = new ClientEngine(settings.ToSettings());
        var seed = await seeder.AddAsync(torrent, seedPath, new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings());
        await seed.HashCheckAsync(false); await seed.StartAsync();
        if (!string.IsNullOrEmpty(exported))
        {
            Console.WriteLine("READY: synthetic torrent fixture at " + exported);
            await Task.Delay(TimeSpan.FromMinutes(8));
        }
        using (var release = new CancellationTokenSource())
        {
            var sink = new RecordingWriter();
            using var gate = new TorrentWriteGate(sink, seedPath, release.Token);
            var pendingWrite = gate.WriteAsync(seed.Files.First(), 0, new byte[16]).AsTask();
            await Task.Delay(20);
            Check(!pendingWrite.IsCompleted && sink.Writes == 0, "Torrent payload writes must remain blocked before metadata validation.");
            gate.Approve(); await pendingWrite;
            Check(sink.Writes == 1, "Approved payload writes must reach the cache writer.");
        }
        using (var release = new CancellationTokenSource())
        {
            var sink = new RecordingWriter();
            using var gate = new TorrentWriteGate(sink, seedPath, release.Token);
            var pendingWrite = gate.WriteAsync(seed.Files.First(), 0, new byte[16]).AsTask();
            release.Cancel();
            var observed = false;
            try { await pendingWrite; } catch (OperationCanceledException) { observed = true; }
            Check(observed && sink.Writes == 0, "Stopping before validation must cancel queued writes without touching payload files.");
        }
        Check(seed.Complete, "Synthetic video fixture must be seeded completely.");
        var fileIndex = seed.Files.Select((f, index) => (f, index)).Single(x => x.f.Path.EndsWith(".mkv")).index;
        Check(TorrentPlayback.SelectFile(seed.Files, null).Path.EndsWith(".mkv"), "Default selection must choose a video rather than a sidecar.");
        var rejected = false;
        try { TorrentPlayback.SelectFile(seed.Files, seed.Files.Count + 1); } catch (IOException) { rejected = true; }
        Check(rejected, "Out-of-range episode selection must not fall back to another file.");
        var data = Path.Combine(directory, "torrent-download");
        var hash = torrent.InfoHashes.V1!.ToHex();
        var session = new TorrentPlayback(data, localOnly: true, infoHash: hash);
        var sessionPath = session.SessionDirectory;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var source = new TorrentSource(torrent.InfoHashes.V1!.ToHex(), fileIndex, [], [$"ipv4://127.0.0.1:{seedPort}"]);
            var bufferStatus = new ConcurrentQueue<TorrentStatus>();
            var stream = await session.OpenAsync(source, new ImmediateProgress(bufferStatus.Enqueue), deadline.Token);
            Check(bufferStatus.Any(x => x.StartupTarget == TorrentBufferPolicy.StartupBytes && x.StartupBuffered == x.StartupTarget), "Playback must wait for its full bounded startup buffer and report its progress.");
            Check(session.FileName.EndsWith(".mkv"), "Peer hints alone must retrieve metadata before selecting the exact indexed video.");
            var actual = new byte[4096]; var expected = new byte[4096];
            using var original = File.OpenRead(mediaPath);
            await stream.ReadExactlyAsync(actual); await original.ReadExactlyAsync(expected);
            Check(actual.SequenceEqual(expected), "Downloaded video bytes must match the seeded fixture.");
            stream.Seek(-4096, SeekOrigin.End); original.Seek(-4096, SeekOrigin.End);
            await stream.ReadExactlyAsync(actual); await original.ReadExactlyAsync(expected);
            Check(actual.SequenceEqual(expected), "Seeking must retrieve verified bytes at the end of the file.");
            stream.Seek(0, SeekOrigin.Begin);
            if (OperatingSystem.IsWindows()) System.Runtime.InteropServices.NativeLibrary.Load(Path.Combine(Environment.SystemDirectory, "d3d11.dll"));
            Core.Initialize();
            using var vlc = new LibVLC("--vout=dummy", "--aout=dummy", "--avcodec-hw=none");
            using var player = new MediaPlayer(vlc);
            var url = session.StartHttpServer();
            using var http = new HttpClient();
            using (var rangeRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                rangeRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(100, 199);
                using var response = await http.SendAsync(rangeRequest);
                Check(response.StatusCode == HttpStatusCode.PartialContent && (await response.Content.ReadAsByteArrayAsync()).SequenceEqual(File.ReadAllBytes(mediaPath).Skip(100).Take(100)), "HTTP ranges must return exact requested bytes.");
            }
            using var media = new Media(vlc, new Uri(url));
            try
            {
                Check(player.Play(media), "Existing VLC player must accept the torrent stream.");
                for (var attempt = 0; attempt < 80 && player.Time < 500; attempt++) await Task.Delay(250);
                Check(player.IsPlaying && player.Time > 0 && player.Length > 0, "The native player must decode and advance through torrent-fed video.");
                player.Time = 60000;
                for (var attempt = 0; attempt < 60 && player.Time < 59000; attempt++) await Task.Delay(250);
                Check(player.Time >= 59000, "Native seeking must work through the torrent stream.");
                await Task.Delay(750);
                Check(player.IsPlaying && player.Time > 60000, "Video playback must continue advancing after a forward seek.");
                player.Time = 10000;
                for (var attempt = 0; attempt < 60 && (player.Time < 9500 || player.Time > 20000); attempt++) await Task.Delay(250);
                Check(player.Time >= 9500 && player.Time < 20000, "Backward HTTP seeking must preserve the active torrent session.");
                await Task.Delay(750);
                Check(player.IsPlaying && player.Time > 10000 && player.Time < 20000, "Video playback must continue advancing after a backward seek.");
            }
            finally { player.Stop(); }
            var download = await http.GetByteArrayAsync(url, deadline.Token);
            Check(download.SequenceEqual(File.ReadAllBytes(mediaPath)), "The complete cached video must match the seed.");
        }
        finally { await session.DisposeAsync(); await seed.StopAsync(); }
        Check(Directory.Exists(sessionPath), "Stopping must preserve a persistent torrent cache.");
        await using (var reopened = new TorrentPlayback(data, localOnly: true, infoHash: hash))
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var restored = await reopened.OpenAsync(new(hash, fileIndex, [], []), new Progress<TorrentStatus>(), deadline.Token);
            Check(reopened.SessionDirectory == sessionPath, "Reopening must use the existing cache.");
            using var http = new HttpClient();
            var url = reopened.StartHttpServer();
            var expectedFile = File.ReadAllBytes(mediaPath);
            foreach (var start in new long[] { expectedFile.Length - 4096, 0, expectedFile.Length / 2, 4096 })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, start + 1023);
                using var response = await http.SendAsync(request, deadline.Token);
                Check((await response.Content.ReadAsByteArrayAsync(deadline.Token)).SequenceEqual(expectedFile.Skip((int)start).Take(1024)), "Completed cache must support random forward/backward reads with the seeder stopped.");
            }
            Core.Initialize();
            using var vlc = new LibVLC("--vout=dummy", "--aout=dummy", "--avcodec-hw=none");
            using var player = new MediaPlayer(vlc);
            using var media = new Media(vlc, new Uri(url));
            Check(player.Play(media), "Reopened cached video must play without live peers.");
            try
            {
                for (var attempt = 0; attempt < 60 && player.Time < 500; attempt++) await Task.Delay(100);
                Check(player.Time > 0, "Reopened cached video must decode with the seeder stopped.");
                foreach (var target in new long[] { 90000, 5000, 60000, 10000 })
                {
                    player.Time = target;
                    for (var attempt = 0; attempt < 60 && Math.Abs(player.Time - target) > 2000; attempt++) await Task.Delay(100);
                    Check(Math.Abs(player.Time - target) <= 2000, "Completed cached video must seek both directions without peers.");
                    await Task.Delay(750);
                    Check(player.IsPlaying && player.Time > target && player.Time < target + 4000, "Cached playback must advance after each offline seek, not just report a new time.");
                }
            }
            finally { player.Stop(); }
        }
        await using (var canceled = new TorrentPlayback(data, localOnly: true))
        {
            using var token = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var observed = false;
            var statuses = new ConcurrentQueue<TorrentStatus>();
            try { await canceled.OpenAsync(new(new string('a', 40), null, [], []), new ImmediateProgress(status => { statuses.Enqueue(status); if (!status.HasMetadata && status.ElapsedSeconds >= 1) token.Cancel(); }), token.Token); }
            catch (OperationCanceledException) { observed = true; }
            Check(observed, "Unavailable torrent metadata lookup must be cancellable.");
            Check(statuses.Any(x => !x.HasMetadata && x.ElapsedSeconds >= 1), "Metadata lookup must publish elapsed discovery status instead of frozen download percentages.");
        }
        var unavailableData = BEncodedValue.Decode<BEncodedDictionary>(torrentData.Encode());
        unavailableData.Remove("announce"); unavailableData.Remove("announce-list");
        var unavailable = Torrent.Load(unavailableData);
        await using (var stalled = new TorrentPlayback(data, localOnly: true))
        {
            using var token = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
            var observed = false;
            try { await stalled.OpenAsync(new(unavailable.InfoHashes.V1!.ToHex(), fileIndex, [], []), new Progress<TorrentStatus>(), token.Token, unavailable); }
            catch (OperationCanceledException) { observed = true; }
            Check(observed, "Initial buffering without peers must be cancellable.");
        }
        var duringLookup = new TorrentPlayback(data, localOnly: true);
        var lookup = duringLookup.OpenAsync(new(new string('b', 40), null, [], []), new Progress<TorrentStatus>(), CancellationToken.None);
        await duringLookup.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Check(lookup.IsCompleted && !Directory.Exists(duringLookup.SessionDirectory), "Disposal during metadata lookup must cancel and await startup before clearing its cache.");
        Console.WriteLine("PASS: local peer seeding, metadata retrieval, HTTP range cancellation, forward/backward native seeking, persistent cache reopen and random seeks with the seeder stopped, cancellation and scoped cleanup.");
    }
    private sealed class RecordingWriter : IPieceWriter
    {
        public int Writes;
        public int OpenFiles => 0;
        public int MaximumOpenFiles => 1;
        public ReusableTask WriteAsync(ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> data) { Writes++; return ReusableTask.CompletedTask; }
        public async ReusableTask<int> ReadAsync(ITorrentManagerFile file, long offset, Memory<byte> data) { await Task.CompletedTask; return 0; }
        public async ReusableTask<bool> ExistsAsync(ITorrentManagerFile file) { await Task.CompletedTask; return false; }
        public ReusableTask CloseAsync(ITorrentManagerFile file) => ReusableTask.CompletedTask;
        public ReusableTask FlushAsync(ITorrentManagerFile file) => ReusableTask.CompletedTask;
        public ReusableTask MoveAsync(ITorrentManagerFile file, string path, bool overwrite) => ReusableTask.CompletedTask;
        public ReusableTask SetMaximumOpenFilesAsync(int count) => ReusableTask.CompletedTask;
        public void Dispose() { }
    }
    private sealed class ImmediateProgress(Action<TorrentStatus> report) : IProgress<TorrentStatus> { public void Report(TorrentStatus value) => report(value); }
    private sealed class NeverReadyStream : MemoryStream
    {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        { await Task.Delay(Timeout.InfiniteTimeSpan, token); return 0; }
    }
    private sealed class MissingStartStream : MemoryStream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public MissingStartStream() : base(new byte[8192]) { }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (Position == 0) { Started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            return await base.ReadAsync(buffer, token);
        }
    }
    private static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    private sealed class LocalTracker : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task run;
        private readonly int peerPort;
        public string Url { get; }
        public LocalTracker(int peerPort)
        {
            this.peerPort = peerPort;
            listener.Start(); Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/announce";
            run = Run();
        }
        private async Task Run()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(stop.Token);
                    using var network = client.GetStream();
                    var request = new byte[8192]; _ = await network.ReadAsync(request, stop.Token);
                    var body = Encoding.ASCII.GetBytes("d8:intervali5e5:peers6:").Concat(new byte[] { 127, 0, 0, 1, (byte)(peerPort >> 8), (byte)peerPort }).Concat("e"u8.ToArray()).ToArray();
                    var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await network.WriteAsync(headers, stop.Token); await network.WriteAsync(body, stop.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await run; stop.Dispose(); }
    }
}
