using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BingeX;

public record CloudstreamExtension(string Key, string Name, string Repository, string Url, string Hash, int Version);
public sealed class CloudstreamService : IAsyncDisposable
{
    private readonly string data;
    private readonly string root;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(45), MaxResponseContentBufferSize = 32 * 1024 * 1024 };
    private readonly SemaphoreSlim gate = new(1);
    private readonly Dictionary<string, JsonElement> loaded = [];
    private readonly Dictionary<string, JsonElement> subtitleResults = [];
    private CloudstreamBridge? bridge;
    private readonly List<CloudstreamExtension> installed;
    public IReadOnlyList<CloudstreamExtension> Installed => installed.ToArray();
    public CloudstreamService(string dataDirectory)
    {
        data = dataDirectory;
        root = Path.Combine(data, "cloudstream");
        Directory.CreateDirectory(root);
        try { installed = JsonSerializer.Deserialize<List<CloudstreamExtension>>(File.ReadAllText(Path.Combine(root, "installed.json"))) ?? []; }
        catch { installed = []; }
    }
    public static string Key(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
    public static InstalledAddon Addon(CloudstreamExtension extension) => new("cloudstream:" + extension.Key, extension.Name, $"cloudstream://local/{extension.Key}/manifest.json");
    private string Plugin(CloudstreamExtension extension) => Path.Combine(root, extension.Key + ".cs3");
    private static Uri Https(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0) throw new InvalidOperationException("Use an HTTPS repository URL.");
        return uri;
    }
    public async Task<IReadOnlyList<CloudstreamExtension>> Repository(string url, CancellationToken token)
    {
        // Accept the familiar GitHub repository page as well as its actual repo.json URL.
        url = CloudstreamRepositoryUrl.Normalize(url);
        var uri = Https(url);
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (uri.Host == "github.com" && parts.Length == 2)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{parts[0]}/{parts[1]}");
            request.Headers.UserAgent.ParseAdd("BingeX/0.13");
            using var reply = await http.SendAsync(request, token); reply.EnsureSuccessStatusCode();
            using var info = JsonDocument.Parse(await reply.Content.ReadAsStringAsync(token));
            url = $"https://raw.githubusercontent.com/{parts[0]}/{parts[1]}/{info.RootElement.GetProperty("default_branch").GetString()}/repo.json";
        }
        using var document = JsonDocument.Parse(await http.GetStringAsync(Https(url), token));
        var lists = document.RootElement.GetProperty("pluginLists").EnumerateArray().Take(20).ToArray();
        var result = new List<CloudstreamExtension>();
        foreach (var list in lists)
        {
            using var plugins = JsonDocument.Parse(await http.GetStringAsync(Https(list.GetString()!), token));
            foreach (var plugin in plugins.RootElement.EnumerateArray().Take(1000))
            {
                var download = AddonClient.Text(plugin, "url"); Https(download);
                var hash = AddonClient.Text(plugin, "fileHash");
                if (!hash.StartsWith("sha256-", StringComparison.OrdinalIgnoreCase) || hash.Length != 71 || !hash[7..].All(Uri.IsHexDigit)) continue;
                if (plugin.TryGetProperty("status", out var status) && status.TryGetInt32(out var state) && state == 0) continue;
                result.Add(new(Key(download), AddonClient.Text(plugin, "name", "Extension"), url, download, hash[7..],
                    plugin.TryGetProperty("version", out var version) && version.TryGetInt32(out var number) ? number : 0));
            }
        }
        return result.DistinctBy(x => x.Key).ToArray();
    }
    private void Save()
    {
        var file = Path.Combine(root, "installed.json");
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(installed));
        File.Move(file + ".tmp", file, true);
    }
    public async Task Install(CloudstreamExtension extension, CancellationToken token)
    {
        if (!CloudstreamRuntime.Ready(data)) throw new InvalidOperationException("Download the bridge first.");
        var bytes = await http.GetByteArrayAsync(Https(extension.Url), token);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(extension.Hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Extension integrity check failed.");
        await gate.WaitAsync(token);
        try
        {
            await Restart();
            var path = Plugin(extension);
            var temporary = path + ".download";
            await File.WriteAllBytesAsync(temporary, bytes, token);
            File.Move(temporary, path, true);
            var result = await Load(extension, token);
            if (result.GetProperty("providers").GetArrayLength() == 0) throw new NotSupportedException("This extension registers no browse or stream providers.");
            installed.RemoveAll(x => x.Key == extension.Key); installed.Add(extension); Save();
        }
        finally { gate.Release(); }
    }
    public async Task Remove(CloudstreamExtension extension)
    {
        await gate.WaitAsync();
        try { await Restart(); installed.RemoveAll(x => x.Key == extension.Key); Save(); File.Delete(Plugin(extension)); }
        finally { gate.Release(); }
    }
    private async Task Restart()
    {
        if (bridge != null) await bridge.DisposeAsync();
        bridge = null; loaded.Clear();
    }
    private async Task<JsonElement> Load(CloudstreamExtension extension, CancellationToken token)
    {
        if (loaded.TryGetValue(extension.Key, out var value)) return value;
        if (!CloudstreamRuntime.Ready(data)) throw new InvalidOperationException("Download the Cloudstream bridge in Add-ons.");
        bridge ??= new(CloudstreamRuntime.Java(data), Path.Combine(CloudstreamRuntime.Home(data), "engine"), Path.Combine(root, "engine-data"));
        value = await bridge.CallAsync("loadPlugin", new { path = Plugin(extension) }, token);
        loaded[extension.Key] = value;
        return value;
    }
    private record Identity(string Provider, string Value, bool Data = false);
    private static string Encode(Identity value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static Identity Decode(string value)
    {
        value = value[(value.LastIndexOf(':') + 1)..];
        return JsonSerializer.Deserialize<Identity>(Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=')))!;
    }
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
    private static string Text(JsonElement value, string key) => AddonClient.Text(value, key);
    private static string Kind(JsonElement value) => Text(value, "type") is "TvSeries" or "Anime" ? "series" : "movie";
    private object Title(JsonElement item, string provider, string key)
    {
        var imdb = item.TryGetProperty("syncIds", out var sync) ? Text(sync, "imdb") : "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(imdb, @"^tt\d{5,12}$")) imdb = "";
        var episodes = new List<object>();
        if (item.TryGetProperty("episodes", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var episode in rows.EnumerateArray())
            {
                index++;
                int Number(string field, int fallback) => episode.TryGetProperty(field, out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var number) ? number : fallback;
                var known = Number("season", -1) >= 0 && Number("episode", -1) > 0;
                episodes.Add(new { id = $"cs:{key}:" + Encode(new(provider, Text(episode, "data"), true)), title = Text(episode, "name") is { Length: > 0 } name ? name : $"Episode {index}", season = Number("season", 1), episode = Number("episode", index), overview = Text(episode, "description"), bingexNumberingKnown = known,
                    bingexStremioId = imdb.Length > 0 && known ? $"{imdb}:{Number("season", 1)}:{Number("episode", index)}" : "" });
            }
        }
        return new { id = $"cs:{key}:" + Encode(new(provider, Text(item, "url"))), type = Kind(item), name = Text(item, "name"), poster = Text(item, "posterUrl"), background = Text(item, "backgroundPosterUrl"), description = Text(item, "plot"), videos = episodes, releaseInfo = item.TryGetProperty("year", out var year) ? year.ToString() : "", bingexStremioId = imdb };
    }
    public async Task<JsonElement> Request(string url, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var rawParts = new Uri(url).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var parts = rawParts.Select(Uri.UnescapeDataString).ToArray();
            var extension = installed.Single(x => x.Key == parts[0]);
            if (parts.Length < 2) throw new InvalidOperationException("Invalid bridge endpoint.");
            var loadedPlugin = await Load(extension, token);
            if (parts[1] == "manifest.json")
            {
                var providers = loadedPlugin.GetProperty("providers").EnumerateArray();
                return Json(new { id = Addon(extension).Id, name = extension.Name, idPrefixes = new[] { $"cs:{extension.Key}:" }, resources = new[] { "catalog", "meta", "stream", "subtitles" }, catalogs = providers.Select(p => new { id = Encode(new(Text(p, "name"), "")), type = "video", name = Text(p, "name"), extra = new[] { new { name = "search", isRequired = !p.GetProperty("hasMainPage").GetBoolean() } } }).ToArray() });
            }
            var resource = parts[1];
            var id = parts[3]; if (id.EndsWith(".json")) id = id[..^5];
            if (resource != "catalog" && !id.StartsWith($"cs:{extension.Key}:", StringComparison.Ordinal))
                throw new InvalidOperationException("This title belongs to another extension.");
            var identity = Decode(id);
            async Task<JsonElement> Call(string method, object parameters) => await bridge!.CallAsync(method, parameters, token);
            var plugin = Plugin(extension);
            if (resource == "catalog")
            {
                var extra = rawParts.Length > 4 ? rawParts[4][..^5] : "";
                var search = extra.Split('&').FirstOrDefault(x => x.StartsWith("search="));
                var result = search != null
                    ? await Call("search", new { provider = identity.Provider, plugin, query = Uri.UnescapeDataString(search[7..]) })
                    : await Call("mainPage", new { provider = identity.Provider, plugin, page = 1 });
                var items = search != null ? result.GetProperty("items").EnumerateArray().ToArray()
                    : result.GetProperty("sections").EnumerateArray().Where(x => x.TryGetProperty("items", out _)).SelectMany(x => x.GetProperty("items").EnumerateArray()).ToArray();
                return Json(new { metas = items.Take(100).Select(x => Title(x, identity.Provider, extension.Key)).ToArray() });
            }
            if (resource == "subtitles") return subtitleResults.GetValueOrDefault(extension.Key + id, Json(new { subtitles = Array.Empty<object>() }));
            var details = identity.Data ? default : await Call("load", new { provider = identity.Provider, plugin, url = identity.Value });
            if (resource == "meta") return Json(new { meta = Title(details, identity.Provider, extension.Key) });
            if (resource != "stream") return Json(new { subtitles = Array.Empty<object>() });
            var payload = identity.Data ? identity.Value : Text(details, "dataUrl");
            if (payload.Length == 0) return Json(new { streams = Array.Empty<object>() });
            var links = await Call("loadLinks", new { provider = identity.Provider, plugin, data = payload });
            subtitleResults[extension.Key + id] = Json(new { subtitles = links.GetProperty("subtitles").EnumerateArray().Select(x => new { id = Key(Text(x, "url")), lang = Text(x, "lang"), label = Text(x,"lang"), url = Text(x, "url"), headers = Headers(x) }).ToArray() });
            return Json(new { streams = links.GetProperty("links").EnumerateArray().Select(link => new
            {
                name = Text(link, "name"), url = Text(link, "url"), description = "Cloudstream · " + identity.Provider,
                audioUrl = link.TryGetProperty("audioTracks", out var audio) && audio.ValueKind == JsonValueKind.Array ? audio.EnumerateArray().Where(x => !x.TryGetProperty("headers", out var h) || h.ValueKind != JsonValueKind.Object || !h.EnumerateObject().Any()).Select(x => Text(x, "url")).FirstOrDefault() ?? "" : "",
                audioTracks = link.TryGetProperty("audioTracks", out var tracks) ? tracks : Json(Array.Empty<object>()),
                subtitles = subtitleResults[extension.Key + id].GetProperty("subtitles"),
                behaviorHints = new { proxyHeaders = new { request = Headers(link) } },
                drm = link.TryGetProperty("drm", out _)
            }).ToArray() });
        }
        catch { await Restart(); throw; }
        finally { gate.Release(); }
    }
    private static Dictionary<string, string> Headers(JsonElement link)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (link.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object)
            foreach (var header in headers.EnumerateObject()) result[header.Name] = header.Value.GetString() ?? "";
        if (Text(link, "referer").Length > 0) result["Referer"] = Text(link, "referer");
        return result;
    }
    public async ValueTask DisposeAsync() { await gate.WaitAsync(); try { await Restart(); http.Dispose(); } finally { gate.Release(); } }
}
