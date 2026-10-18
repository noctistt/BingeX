using BingeX;
using LibVLCSharp.Shared;

static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
var directory = Path.Combine(Path.GetTempPath(), "BingeX-test-" + Guid.NewGuid());
try
{
    using (var store = new LibraryStore(directory))
    {
        store.Save(new("C:\\media\\O'Brien.mkv", "O'Brien", 12000, 60000));
        store.Save(new("C:\\media\\O'Brien.mkv", "O'Brien", 22000, 60000));
        store.Install(new("test.provider", "Provider", "https://example.com/token/manifest.json"));
    }
    using (var store = new LibraryStore(directory))
    {
        Assert(store.History().Count == 1 && store.History()[0].Position == 22000, "Resume position must persist without duplicate history entries.");
        Assert(store.History()[0].Title == "O'Brien", "Titles containing quotes must round-trip.");
        Assert(store.Addons().Count == 1, "Installed add-ons must survive reopening.");
        store.RemoveAddon("test.provider");
        Assert(store.Addons().Count == 0, "Removed add-ons must not remain installed.");
    }
    var endpoint = AddonClient.Endpoint("https://example.com/config/manifest.json?key=abc", "stream", "series", "tt123:1:2");
    Assert(endpoint == "https://example.com/config/stream/series/tt123%3A1%3A2.json?key=abc", "Episode IDs and configuration query must survive URL construction.");
    using var client = new AddonClient();
    var rejected = false;
    try { await client.Get("file:///private.json"); } catch (InvalidOperationException) { rejected = true; }
    Assert(rejected, "Manifest client must reject non-HTTPS URLs.");
    Console.WriteLine("PASS: persistent history, parameterized titles, add-on persistence/removal, configured episode URLs, manifest scheme validation.");
    if (args.Contains("--live-addon"))
    {
        const string manifestUrl = "https://v3-cinemeta.strem.io/manifest.json";
        var manifest = await client.Get(manifestUrl);
        Assert(AddonClient.Text(manifest, "id").Length > 0, "Live manifest must have an ID.");
        var catalog = await client.Get(AddonClient.Endpoint(manifestUrl, "catalog", "movie", "top"));
        Assert(catalog.GetProperty("metas").GetArrayLength() > 0, "Live catalog must return titles.");
        Console.WriteLine("PASS: live Cinemeta manifest and movie catalog requests.");
    }
    var videoPath = args.FirstOrDefault(File.Exists);
    if (videoPath != null)
    {
        Core.Initialize();
        using var engine = new LibVLC("--vout=dummy", "--aout=dummy", "--no-video-title-show");
        using var player = new MediaPlayer(engine);
        using var media = new Media(engine, new Uri(Path.GetFullPath(videoPath)));
        Assert(player.Play(media), "Native playback must start.");
        for (var attempt = 0; attempt < 40 && (player.Length <= 0 || player.Time < 500); attempt++) await Task.Delay(250);
        Assert(player.IsPlaying && player.Length > 0 && player.Time > 0, "Native decoder must advance and report duration.");
        player.SetPause(true);
        await Task.Delay(300);
        Assert(player.State == VLCState.Paused, "Playback must pause.");
        player.Time = 20000;
        player.SetPause(false);
        await Task.Delay(700);
        Assert(player.Time >= 19000, "Seek must move playback to the requested position.");
        player.Stop();
        Console.WriteLine("PASS: native LibVLC decoding, duration, pause, seek, and stop.");
    }
}
finally
{
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}
