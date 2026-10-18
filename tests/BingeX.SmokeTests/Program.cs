using BingeX;
using LibVLCSharp.Shared;
using System.Text.Json;
using System.Net;

static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
var directory = Path.Combine(Path.GetTempPath(), "BingeX-test-" + Guid.NewGuid());
try
{
    using (var store = new LibraryStore(directory))
    {
        store.Save(new("C:\\media\\O'Brien.mkv", "O'Brien", 12000, 60000));
        store.Save(new("C:\\media\\O'Brien.mkv", "O'Brien", 22000, 60000));
        store.Install(new("test.provider", "Provider", "https://example.com/token/manifest.json"));
        var context = new MediaContext("tt123", "series", "A Show", "https://example.com/poster.jpg", "test.provider", "tt123:2:3", "Episode 3");
        store.SaveContext(context);
        store.Save(new(context.HistoryKey, context.DisplayName, 15000, 90000));
    }
    using (var store = new LibraryStore(directory))
    {
        Assert(store.History().Count(x => x.Path == "C:\\media\\O'Brien.mkv") == 1 && store.History().First(x => x.Path == "C:\\media\\O'Brien.mkv").Position == 22000, "Resume position must persist without duplicate history entries.");
        Assert(store.History().Any(x => x.Title == "O'Brien"), "Titles containing quotes must round-trip.");
        var context = store.Context("stremio:series:tt123:2:3");
        Assert(context?.EpisodeId == "tt123:2:3" && context.AddonId == "test.provider", "Remote resume must use stable episode identity without storing a playback URL.");
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
    var searchUrl = AddonClient.Endpoint("https://example.com/config/manifest.json?key=abc", "catalog", "movie", "top", new Dictionary<string, string> { ["search"] = "a/b & c" });
    Assert(searchUrl.Contains("/search=a%2Fb%20%26%20c.json?key=abc"), "Search text must stay encoded in its own path segment.");
    using var fixture = JsonDocument.Parse("""
        {"id":"fixture","name":"Fixture","types":["movie"],"idPrefixes":["tt"],
         "resources":["catalog","meta",{"name":"stream","types":["series"],"idPrefixes":["episode:"]}],
         "catalogs":[{"type":"movie","id":"top","name":"Popular","extra":[{"name":"search"}]},
           {"type":"series","id":"search","extra":[{"name":"search","isRequired":true}]},
           {"type":"movie","id":"genre","extra":[{"name":"genre","isRequired":true}]}]}
        """);
    Assert(MediaLibrary.Supports(fixture.RootElement, "meta", "movie", "tt12"), "String resources must inherit manifest scopes.");
    Assert(!MediaLibrary.Supports(fixture.RootElement, "meta", "series", "tt12"), "Incompatible types must not be queried.");
    Assert(MediaLibrary.Supports(fixture.RootElement, "stream", "series", "episode:1") && !MediaLibrary.Supports(fixture.RootElement, "stream", "series", "tt12"), "Object resource scopes must override manifest scopes.");
    var parsedCatalogs = MediaLibrary.ParseCatalogs(new("fixture", "Fixture", "https://example.com/manifest.json"), fixture.RootElement).ToArray();
    Assert(parsedCatalogs.Length == 2 && parsedCatalogs[0].Browsable && parsedCatalogs[0].Searchable && !parsedCatalogs[1].Browsable, "Search-only and required-filter catalogs must be distinguished.");
    using var episodeJson = JsonDocument.Parse("""{"id":"tt1","type":"series","name":"Series","videos":[{"id":"e2","season":2,"episode":1},{"id":"e1","season":1,"episode":2}]}""");
    Assert(MediaLibrary.ParseTitle(episodeJson.RootElement, "fixture", "movie").Episodes[0].Id == "e1", "Episodes must be grouped and ordered by season/number.");
    using var torrent = JsonDocument.Parse("""{"infoHash":"abc","name":"Torrent"}""");
    using var direct = JsonDocument.Parse("""{"url":"https://example.com/movie.mp4","name":"1080p"}""");
    Assert(MediaLibrary.ParseStream("Fixture", torrent.RootElement).UnsupportedReason != null && MediaLibrary.ParseStream("Fixture", direct.RootElement).UnsupportedReason == null, "Unsupported sources must be explained rather than offered as playable.");
    using (var fakeClient = new AddonClient(new FixtureHandler()))
    {
        var mediaLibrary = new MediaLibrary(fakeClient);
        var installed = new[] { new InstalledAddon("good", "Good", "https://good.example/manifest.json"), new InstalledAddon("bad", "Bad", "https://bad.example/manifest.json") };
        var discovery = await mediaLibrary.Catalogs(installed, CancellationToken.None);
        Assert(discovery.Items.Count == 1 && discovery.Failures == 1, "One failed provider must not hide healthy catalogs.");
        var results = await mediaLibrary.Search(discovery.Items, "a/b", CancellationToken.None);
        Assert(results.Items.Count == 1, "Search must query supported catalogs and deduplicate titles.");
        var sources = await mediaLibrary.Streams("movie", "tt1", installed, CancellationToken.None);
        Assert(sources.Items.Count == 2 && sources.Items.Count(x => x.UnsupportedReason == null) == 1 && sources.Failures == 1, "Stream lookup must preserve playable and explained unsupported sources when another provider fails.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var cancellationObserved = false;
        try { await mediaLibrary.Catalog(discovery.Items[0], null, canceled.Token); } catch (OperationCanceledException) { cancellationObserved = true; }
        Assert(cancellationObserved, "Canceled navigation must stop pending catalog work.");
    }
    Console.WriteLine("PASS: persistent history, parameterized titles, add-on persistence/removal, configured episode URLs, manifest scheme validation.");
    Console.WriteLine("PASS: remote episode resume, search encoding, provider scope filtering, catalog capabilities, episode ordering, source compatibility, partial failures, cancellation.");
    if (args.Contains("--live-addon"))
    {
        const string manifestUrl = "https://v3-cinemeta.strem.io/manifest.json";
        var manifest = await client.Get(manifestUrl);
        Assert(AddonClient.Text(manifest, "id").Length > 0, "Live manifest must have an ID.");
        var catalog = await client.Get(AddonClient.Endpoint(manifestUrl, "catalog", "movie", "top"));
        Assert(catalog.GetProperty("metas").GetArrayLength() > 0, "Live catalog must return titles.");
        var mediaLibrary = new MediaLibrary(client);
        var installed = new[] { new InstalledAddon(AddonClient.Text(manifest, "id"), "Cinemeta", manifestUrl) };
        var discovery = await mediaLibrary.Catalogs(installed, CancellationToken.None);
        var results = await mediaLibrary.Search(discovery.Items, "Big Buck Bunny", CancellationToken.None);
        Assert(results.Items.Count > 0, "Live searchable catalogs must return results.");
        var detail = await mediaLibrary.Details(results.Items[0], installed, CancellationToken.None);
        Assert(detail.Description.Length > 0 && detail.Poster.StartsWith("https://"), "Live title page must have a synopsis and poster.");
        var series = await mediaLibrary.Details(new("tt0944947", "series", "Series", "", "", "", "", "", "", installed[0].Id, []), installed, CancellationToken.None);
        Assert(series.Episodes.Count > 0, "Live series metadata must provide episodes.");
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

sealed class FixtureHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestUri!.Host == "bad.example") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var json = request.RequestUri.AbsolutePath.EndsWith("manifest.json")
            ? """{"resources":["catalog",{"name":"stream","types":["movie"],"idPrefixes":["tt"]}],"types":["movie"],"catalogs":[{"type":"movie","id":"top","name":"Top","extra":[{"name":"search"}]}]}"""
            : request.RequestUri.AbsolutePath.Contains("/stream/")
                ? """{"streams":[{"url":"https://video.example/test.mp4","name":"1080p"},{"infoHash":"abc","name":"Torrent"}]}"""
                : """{"metas":[{"id":"tt1","name":"One","type":"movie"},{"id":"tt1","name":"One","type":"movie"}]}""";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
