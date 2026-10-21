using System.Net;
using System.Text.Json;
using BingeX;

internal static class SharedProviderChecks
{
    public static async Task Run(string directory)
    {
        var handler = new Handler();
        using var client = new AddonClient(handler);
        var extensionStreamRequests = new List<string>();
        client.CloudstreamRequest = (url,_) => { if(url.Contains("/stream/")) extensionStreamRequests.Add(url); return Task.FromResult(JsonSerializer.SerializeToElement(url.EndsWith("manifest.json")
            ? (object)new { resources = new[] { "meta","stream","subtitles" }, idPrefixes = new[] { "cs:" } }
            : url.Contains("/meta/") ? new { meta = new { id = "cs:test:show", type = "series", name = "Fixture", bingexStremioId = "tt1234567", videos = new[] { new { id = "cs:test:episode", season = 2, episode = 3, bingexNumberingKnown = true, bingexStremioId = "tt1234567:2:3" } } } }
            : url.Contains("/stream/") ? new { streams = new[] { new { name = "Extension", url = "https://media.example/video", subtitles = new[] { new { id = "s1", lang = "en", label = "English", url = "https://subs.example/file", headers = new Dictionary<string,string>{{"Origin","https://provider.example"},{"Cookie","fixture=1"}} } } } } }
            : new { subtitles = Array.Empty<object>() })); };
        var file = Path.Combine(directory,"shared-links.json");
        var library = new MediaLibrary(client,file);
        InstalledAddon[] providers = [new("cloud","Extension","cloudstream://local/test/manifest.json"),new("other","Other add-on","https://addon.example/manifest.json")];
        await library.Streams("series","cs:test:episode",providers,default);
        if (handler.StreamIds.Count != 0) throw new Exception("An unmatched extension ID was sent to an unrelated add-on.");
        var show = new TitleInfo("cs:test:show","series","Fixture","","","","","","","cloud",[]);
        var full = await library.Details(show,providers,default);
        var streams = await library.Streams("series","cs:test:episode",providers,default);
        if (!handler.StreamIds.Contains("tt1234567:2:3") || streams.Items.Count != 2) throw new Exception("Shared episode ID must merge extension and Stremio sources.");
        if (streams.Items[0].Subtitles?.Single().Headers?.GetValueOrDefault("Cookie") != "fixture=1") throw new Exception("Extension subtitle/header data was lost.");
        await library.Subtitles("series","cs:test:episode",providers,default);
        if (!handler.SubtitleIds.Contains("tt1234567:2:3")) throw new Exception("Subtitle add-ons did not receive the shared episode ID.");
        var manual = full with { Id = "cs:manual:show", Episodes = [new("cs:manual:episode","Episode",1,2,""),new("cs:manual:alternate","Alternate",1,2,""),new("cs:manual:unknown","Unknown",1,3,"",false)] };
        library.LinkTitle(manual,full with { Id = "tt7654321" });
        var reopened = new MediaLibrary(client,file);
        if (!reopened.HasSharedId("series","cs:manual:episode") || reopened.HasSharedId("series","cs:manual:unknown")) throw new Exception("Confirmed matches must persist without guessing unknown episode numbers.");
        var streamCount = handler.StreamIds.Count;
        var extensionCount = extensionStreamRequests.Count;
        await reopened.EpisodeStreams("series",["cs:manual:episode","cs:manual:alternate"],providers,default);
        if (handler.StreamIds.Count != streamCount+1 || handler.StreamIds.Last()!="tt7654321:1:2" || extensionStreamRequests.Count!=extensionCount+2)
            throw new Exception("Grouped episode variants must preserve both extension requests but send one canonical query to shared-ID providers.");
        using var downloader = new OnlineSubtitles(handler);
        var document = await downloader.Download("https://subs.example/file",default,new Dictionary<string,string>{{"Origin","https://provider.example"},{"Cookie","fixture=1"}});
        if (document.Cues.Count != 1 || !handler.SawSubtitleHeaders) throw new Exception("Header-protected extension subtitle did not load.");
        Console.WriteLine("PASS: extension subtitles/headers, shared-ID source merging, episode subtitle routing, explicit-match persistence and unknown-ID isolation.");
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<string> StreamIds { get; } = [];
        public List<string> SubtitleIds { get; } = [];
        public bool SawSubtitleHeaders;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var url = request.RequestUri!;
            if (url.Host == "subs.example")
            {
                SawSubtitleHeaders = request.Headers.TryGetValues("Origin",out var origin) && origin.Single() == "https://provider.example" && request.Headers.TryGetValues("Cookie",out var cookie) && cookie.Single() == "fixture=1";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent("1\n00:00:00,000 --> 00:00:02,000\nFixture caption\n")});
            }
            var id = Uri.UnescapeDataString(url.Segments.Last()).Replace(".json","");
            object body;
            if (url.AbsolutePath.EndsWith("manifest.json")) body = new { resources = new[] { "stream","subtitles" }, idPrefixes = new[] { "tt" } };
            else if (url.AbsolutePath.Contains("/stream/")) { StreamIds.Add(id); body = new { streams = new[] { new { url = "https://media.example/other" } } }; }
            else { SubtitleIds.Add(id); body = new { subtitles = Array.Empty<object>() }; }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent(JsonSerializer.Serialize(body))});
        }
    }
}
