using BingeX;
using System.Net;
using System.Text;

internal static class AddonHealthChecks
{
    public static async Task Run()
    {
        var addon = new InstalledAddon("fixture", "Fixture", "https://example.com/private-token/manifest.json?key=secret");
        foreach (var (code, body, expected, failed) in new[]
        {
            (HttpStatusCode.OK, "{\"id\":\"fixture\",\"name\":\"Fixture\",\"resources\":[]}", "Playback has not been tested", false),
            (HttpStatusCode.OK, "{\"id\":\"fixture\",\"name\":\"Fixture\",\"resources\":[],\"behaviorHints\":{\"configurationRequired\":true}}", "configuration is required", true),
            (HttpStatusCode.OK, "{}", "Invalid add-on manifest", true),
            (HttpStatusCode.OK, "not json", "invalid response", true),
            (HttpStatusCode.TooManyRequests, "", "Request limit", true),
            (HttpStatusCode.Unauthorized, "", "Access denied", true)
        })
        {
            using var client = new AddonClient(new Handler(code, body));
            var result = await AddonHealth.CheckManifest(client, addon, CancellationToken.None);
            if (result.Failed != failed || !result.Message.Contains(expected) || result.Message.Contains("secret") || result.Message.Contains("private-token"))
                throw new Exception("Add-on checks must distinguish failure types without exposing configured URLs.");
        }
        using (var client = new AddonClient(new Handler(HttpStatusCode.OK, "", timeout: true)))
        {
            var result = await AddonHealth.CheckManifest(client, addon, CancellationToken.None);
            if (!result.Message.Contains("timed out")) throw new Exception("Provider timeouts must be labeled.");
        }
        using (var token = new CancellationTokenSource())
        using (var client = new AddonClient(new Handler(HttpStatusCode.OK, "")))
        {
            token.Cancel(); var canceled = false;
            try { await AddonHealth.CheckManifest(client, addon, token.Token); } catch (OperationCanceledException) { canceled = true; }
            if (!canceled) throw new Exception("Canceled checks must not report a provider outage.");
        }
        var known = AddonHealth.CommunityPage(addon with { ManifestUrl = "https://torrentio.strem.fun/private-token/manifest.json?key=secret" });
        if (known.AbsoluteUri != "https://stremio-addons.net/addons/torrentio" || AddonHealth.CommunityPage(addon).AbsoluteUri != "https://stremio-addons.net/addons")
            throw new Exception("Community links must strip all private configuration and avoid guessing unknown listings.");
        foreach (var (code, body, failed, expected) in new[]
        {
            (HttpStatusCode.OK, "{\"streams\":[]}", 0, "no streams"),
            (HttpStatusCode.TooManyRequests, "", 1, "Request limit"),
            (HttpStatusCode.OK, "{}", 1, "invalid stream response"),
            (HttpStatusCode.OK, "{\"streams\":[{\"url\":\"https://video.example/test.mp4\",\"behaviorHints\":{\"proxyHeaders\":{}}}]}", 0, "1 supported")
        })
        {
            using var client = new AddonClient(new Handler(code, body, streamCheck: true));
            var result = await new MediaLibrary(client).Streams("movie", "ttfixture", [addon], CancellationToken.None);
            if (result.Failures != failed || result.Diagnostics?.Single().Message.Contains(expected) != true)
                throw new Exception("Stream lookup diagnostics must distinguish no results, unsupported results, bad responses and rate limiting.");
        }
        Console.WriteLine("PASS: add-on connection results, rate limits, login/configuration failures, cancellation, and private-token-free community links.");
    }
    private sealed class Handler(HttpStatusCode code, string body, bool timeout = false, bool streamCheck = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (timeout) throw new TaskCanceledException();
            if (streamCheck && request.RequestUri!.AbsolutePath.EndsWith("manifest.json"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"fixture\",\"name\":\"Fixture\",\"resources\":[\"stream\"],\"types\":[\"movie\"]}") });
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
