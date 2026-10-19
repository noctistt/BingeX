using BingeX;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.BEncoding;
using LibVLCSharp.Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

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
        Check(seed.Complete, "Synthetic video fixture must be seeded completely.");
        var fileIndex = seed.Files.Select((f, index) => (f, index)).Single(x => x.f.Path.EndsWith(".mkv")).index;
        Check(TorrentPlayback.SelectFile(seed.Files, null).Path.EndsWith(".mkv"), "Default selection must choose a video rather than a sidecar.");
        var rejected = false;
        try { TorrentPlayback.SelectFile(seed.Files, seed.Files.Count + 1); } catch (IOException) { rejected = true; }
        Check(rejected, "Out-of-range episode selection must not fall back to another file.");
        var data = Path.Combine(directory, "torrent-download");
        var session = new TorrentPlayback(data, localOnly: true);
        var sessionPath = session.SessionDirectory;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var source = new TorrentSource(torrent.InfoHashes.V1!.ToHex(), fileIndex, [tracker.Url], [$"ipv4://127.0.0.1:{seedPort}"]);
            var stream = await session.OpenAsync(source, new Progress<TorrentStatus>(), deadline.Token);
            Check(session.FileName.EndsWith(".mkv"), "The exact indexed video must be selected after magnet metadata lookup.");
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
            using var input = new StreamMediaInput(stream);
            using var media = new Media(vlc, input);
            try
            {
                Check(player.Play(media), "Existing VLC player must accept the torrent stream.");
                for (var attempt = 0; attempt < 80 && player.Time < 500; attempt++) await Task.Delay(250);
                Check(player.IsPlaying && player.Time > 0 && player.Length > 0, "The native player must decode and advance through torrent-fed video.");
                player.Time = 60000;
                for (var attempt = 0; attempt < 60 && player.Time < 59000; attempt++) await Task.Delay(250);
                Check(player.Time >= 59000, "Native seeking must work through the torrent stream.");
            }
            finally { session.Cancel(); player.Stop(); }
        }
        finally { await session.DisposeAsync(); await seed.StopAsync(); }
        Check(!Directory.Exists(sessionPath), "Stopping must remove temporary torrent files after releasing the decoder.");
        await using (var canceled = new TorrentPlayback(data, localOnly: true))
        {
            using var token = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
            var observed = false;
            try { await canceled.OpenAsync(new(new string('a', 40), null, [], []), new Progress<TorrentStatus>(), token.Token); }
            catch (OperationCanceledException) { observed = true; }
            Check(observed, "Unavailable torrent metadata lookup must be cancellable.");
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
        Console.WriteLine("PASS: local peer seeding, tracker/magnet metadata retrieval, exact file selection, verified reads, native torrent playback/seek, cancellation and cache disposal.");
    }
    private sealed class NeverReadyStream : MemoryStream
    {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        { await Task.Delay(Timeout.InfiniteTimeSpan, token); return 0; }
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
