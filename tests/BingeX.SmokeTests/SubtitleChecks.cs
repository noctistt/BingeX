using BingeX;
using System.Net;

internal static class SubtitleChecks
{
    private static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
    public static async Task Run(bool live)
    {
        var handler = new SubtitleFixtureHandler();
        using var client = new AddonClient(handler);
        var library = new MediaLibrary(client);
        var providers = new[] { new InstalledAddon("one", "One", "https://good.example/config/manifest.json?key=fixture"), new InstalledAddon("bad", "Bad", "https://bad.example/manifest.json") };
        var found = await library.Subtitles("series", "tt123:2:3", providers, CancellationToken.None);
        Check(found.Items.Count == 1 && found.Failures == 1, "Healthy subtitle providers must survive partial failures; duplicate and insecure URLs must be filtered.");
        Check(handler.Seen.Any(x => x.Contains("/subtitles/series/tt123%3A2%3A3.json?key=fixture")), "Episode IDs and provider configuration must survive subtitle URL construction.");
        var before = handler.Seen.Count;
        await library.Subtitles("other", "local:test", providers.Take(1).ToArray(), CancellationToken.None);
        Check(handler.Seen.Count == before, "Providers must not receive subtitle requests outside declared type/ID scope.");
        using var downloads = new OnlineSubtitles(new SubtitleFixtureHandler());
        var document = await downloads.Download("https://files.example/redirect", CancellationToken.None);
        Check(document.TextAt(1500, 0) == "Online caption", "An HTTPS redirect must load and parse an extensionless subtitle URL.");
        foreach (var path in new[] { "html", "oversize", "downgrade" })
        {
            var rejected = false;
            try { await downloads.Download("https://files.example/" + path, CancellationToken.None); }
            catch (Exception e) when (e is FormatException or InvalidOperationException) { rejected = true; }
            Check(rejected, "Invalid subtitle response must be rejected: " + path);
        }
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var canceledObserved = false;
        try { await downloads.Download("https://files.example/srt", canceled.Token); } catch (OperationCanceledException) { canceledObserved = true; }
        Check(canceledObserved, "Canceled subtitle downloads must stop before applying captions.");
        Check(OnlineSubtitles.GuessTitle("Some.Show.S02E03.1080p.mkv") == "Some Show", "Title hints must discard release/episode suffixes.");
        Check(OnlineSubtitles.LanguageName("eng") == "English" && OnlineSubtitles.LanguageName("ger") == "German", "Language codes must have readable names.");
        Console.WriteLine("PASS: subtitle providers, episode routing, deduplication, partial failure, bounded downloads, redirects, invalid data, and cancellation.");
        if (live)
        {
            using var liveClient = new AddonClient();
            var result = await new MediaLibrary(liveClient).Subtitles("movie", "tt0133093", [OnlineSubtitles.DefaultProvider], CancellationToken.None);
            Check(result.Items.Count > 0 && result.Failures == 0, "Official live subtitle provider must return results.");
            var choice = result.Items.FirstOrDefault(x => x.Language == "eng") ?? result.Items[0];
            using var download = new OnlineSubtitles();
            var actual = await download.Download(choice.Url, CancellationToken.None);
            Check(actual.Cues.Count > 0, "Live subtitle result must download and parse.");
            Console.WriteLine($"PASS: live OpenSubtitles search and download ({result.Items.Count} results, {actual.Cues.Count} cues).");
        }
    }
}

internal sealed class SubtitleFixtureHandler : HttpMessageHandler
{
    public List<string> Seen { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Seen.Add(request.RequestUri!.AbsoluteUri);
        if (request.RequestUri.Host == "bad.example") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        if (request.RequestUri.AbsolutePath.EndsWith("manifest.json")) return Json("""{"resources":[{"name":"subtitles","types":["movie","series"],"idPrefixes":["tt"]}]}""");
        if (request.RequestUri.AbsolutePath.Contains("/subtitles/")) return Json("""{"subtitles":[{"id":"one","lang":"eng","url":"https://files.example/srt"},{"id":"copy","lang":"eng","url":"https://files.example/srt"},{"id":"unsafe","lang":"eng","url":"http://files.example/srt"}]}""");
        var path = request.RequestUri.AbsolutePath;
        if (path is "/redirect" or "/downgrade")
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new Uri(path == "/redirect" ? "https://files.example/srt" : "http://files.example/srt");
            return Task.FromResult(redirect);
        }
        var content = new StringContent(path == "/html" ? "<html>not subtitles</html>" : "1\n00:00:01,000 --> 00:00:03,000\nOnline caption\n");
        if (path == "/oversize") content.Headers.ContentLength = 9 * 1024 * 1024;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
    private static Task<HttpResponseMessage> Json(string text) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) });
}
