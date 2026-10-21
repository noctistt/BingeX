using System.Collections.Concurrent;
using System.Text.Json;

namespace BingeX;

public record CatalogInfo(InstalledAddon Addon, string Id, string Type, string Name, bool Searchable, bool Browsable)
{
    public override string ToString() => $"{Name} · {Type} · {Addon.Name}";
}
public record EpisodeInfo(string Id, string Name, int Season, int Number, string Overview, bool NumberingKnown = true);
public record TitleInfo(string Id, string Type, string Name, string Poster, string Background, string Description,
    string Release, string Rating, string Genres, string AddonId, IReadOnlyList<EpisodeInfo> Episodes, int? DurationSeconds = null);
public record MediaContext(string Id, string Type, string Name, string Poster, string AddonId, string? EpisodeId = null, string? EpisodeName = null, int? DurationSeconds = null)
{
    public string HistoryKey => $"stremio:{Type}:{EpisodeId ?? Id}";
    public string DisplayName => EpisodeName == null ? Name : $"{Name} · {EpisodeName}";
}
public record StreamChoice(string Provider, string Name, string Description, string Url, string? UnsupportedReason, TorrentSource? Torrent = null, SourceEvidence? Evidence = null, IReadOnlyDictionary<string, string>? RequestHeaders = null, string? AudioUrl = null, IReadOnlyList<StreamAudioOption>? AudioOptions = null, IReadOnlyList<SubtitleChoice>? Subtitles = null);
public record SubtitleChoice(string Provider, string Id, string Language, string Label, string Url, IReadOnlyDictionary<string, string>? Headers = null);
public record ProviderResult<T>(IReadOnlyList<T> Items, int Failures, IReadOnlyList<AddonCheck>? Diagnostics = null);

public sealed partial class MediaLibrary(AddonClient client, string? sharedIdFile = null)
{
    private readonly ConcurrentDictionary<string, JsonElement> manifests = new();
    private readonly SemaphoreSlim requests = new(5);
    private readonly SemaphoreSlim extensionRequests = new(1);

    private readonly ConcurrentDictionary<string,(DateTime At, IReadOnlyList<TitleInfo> Titles)> catalogCache=new();
    public void Invalidate() { manifests.Clear(); catalogCache.Clear(); }
    private async Task<JsonElement> Request(string url, CancellationToken token)
    {
        var queue=url.StartsWith("cloudstream://",StringComparison.OrdinalIgnoreCase) ? extensionRequests : requests;
        await queue.WaitAsync(token);
        try { return await client.Get(url, token); }
        finally { queue.Release(); }
    }
    private async Task<JsonElement> Manifest(InstalledAddon addon, CancellationToken token)
    {
        if (manifests.TryGetValue(addon.ManifestUrl, out var cached)) return cached;
        var manifest = await Request(addon.ManifestUrl, token);
        manifests[addon.ManifestUrl] = manifest;
        return manifest;
    }
    public static bool Supports(JsonElement manifest, string resource, string type, string id)
    {
        if (!manifest.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array) return false;
        foreach (var entry in resources.EnumerateArray())
        {
            var name = entry.ValueKind == JsonValueKind.String ? entry.GetString() : AddonClient.Text(entry, "name");
            if (name != resource) continue;
            var scope = entry.ValueKind == JsonValueKind.Object ? entry : manifest;
            if (scope.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Array && !types.EnumerateArray().Any(x => x.GetString() == type)) continue;
            if (scope.TryGetProperty("idPrefixes", out var prefixes) && prefixes.ValueKind == JsonValueKind.Array && !prefixes.EnumerateArray().Any(x => id.StartsWith(x.GetString() ?? "", StringComparison.Ordinal))) continue;
            return true;
        }
        return false;
    }
    public static IEnumerable<CatalogInfo> ParseCatalogs(InstalledAddon addon, JsonElement manifest)
    {
        if (!manifest.TryGetProperty("catalogs", out var catalogs) || catalogs.ValueKind != JsonValueKind.Array) yield break;
        foreach (var catalog in catalogs.EnumerateArray())
        {
            var id = AddonClient.Text(catalog, "id"); var type = AddonClient.Text(catalog, "type");
            if (id.Length == 0 || type.Length == 0) continue;
            var extras = catalog.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array ? extra.EnumerateArray().ToArray() : [];
            var searchable = extras.Any(x => AddonClient.Text(x, "name") == "search");
            var browsable = !extras.Any(x => x.TryGetProperty("isRequired", out var required) && required.ValueKind == JsonValueKind.True);
            // Required filters other than search need their own controls before we can query them.
            if (extras.Any(x => AddonClient.Text(x, "name") != "search" && x.TryGetProperty("isRequired", out var required) && required.ValueKind == JsonValueKind.True)) continue;
            yield return new(addon, id, type, AddonClient.Text(catalog, "name", id), searchable, browsable);
        }
    }
    public async Task<ProviderResult<CatalogInfo>> Catalogs(IReadOnlyList<InstalledAddon> installed, CancellationToken token)
    {
        var results = await Task.WhenAll(installed.Select(async addon =>
        {
            try { return new ProviderResult<CatalogInfo>(ParseCatalogs(addon, await Manifest(addon, token)).ToArray(), 0); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return new ProviderResult<CatalogInfo>([], 1); }
        }));
        return new(results.SelectMany(x => x.Items).ToArray(), results.Sum(x => x.Failures));
    }
    public async Task<IReadOnlyList<TitleInfo>> Catalog(CatalogInfo catalog, string? search, CancellationToken token)
    {
        var extra = search == null ? null : new Dictionary<string, string> { ["search"] = search };
        var url=AddonClient.Endpoint(catalog.Addon.ManifestUrl,"catalog",catalog.Type,catalog.Id,extra);
        if(search==null && catalogCache.TryGetValue(url,out var cached) && DateTime.UtcNow-cached.At<TimeSpan.FromMinutes(5)) return cached.Titles;
        var response = await Request(url, token);
        if (!response.TryGetProperty("metas", out var metas) || metas.ValueKind != JsonValueKind.Array) return [];
        var titles=metas.EnumerateArray().Where(x => AddonClient.Text(x, "id").Length > 0).Take(100).Select(x => ParseTitle(x, catalog.Addon.Id, catalog.Type)).ToArray();
        token.ThrowIfCancellationRequested();
        if(search==null) { if(catalogCache.Count>=100) catalogCache.Clear(); catalogCache[url]=(DateTime.UtcNow,titles); }
        return titles;
    }
    public async Task<ProviderResult<TitleInfo>> Search(IReadOnlyList<CatalogInfo> catalogs, string query, CancellationToken token)
    {
        var results = await Task.WhenAll(catalogs.Where(x => x.Searchable).Select(async catalog =>
        {
            try { return new ProviderResult<TitleInfo>(await Catalog(catalog, query, token), 0); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return new ProviderResult<TitleInfo>([], 1); }
        }));
        return new(results.SelectMany(x => x.Items).DistinctBy(x => (x.Type, x.Id)).Take(120).ToArray(), results.Sum(x => x.Failures));
    }
    public async Task<TitleInfo> Details(TitleInfo preview, IReadOnlyList<InstalledAddon> installed, CancellationToken token)
    {
        foreach (var addon in installed.OrderByDescending(x => x.Id == preview.AddonId))
        {
            try
            {
                if (!Supports(await Manifest(addon, token), "meta", preview.Type, preview.Id)) continue;
                var response = await Request(AddonClient.Endpoint(addon.ManifestUrl, "meta", preview.Type, preview.Id), token);
                if (response.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
                {
                    RegisterSharedIds(meta, AddonClient.Text(meta,"type",preview.Type));
                    return ParseTitle(meta, addon.Id, preview.Type) with { Poster = AddonClient.Text(meta, "poster", preview.Poster) };
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { /* Try the next compatible metadata provider. */ }
        }
        return preview;
    }
    public async Task<ProviderResult<StreamChoice>> EpisodeStreams(string type, IReadOnlyList<string> ids, IReadOnlyList<InstalledAddon> installed, CancellationToken token)
    {
        // Different extension entries can map to the same shared Stremio ID.
        // Query that provider once, while retaining distinct extension payloads.
        var requests = installed.SelectMany(addon => ids.Select(id => (Id:id, Query:ProviderId(addon,type,id)))
            .Where(x=>x.Query!=null).DistinctBy(x=>x.Query).Select(x=>(Addon:addon,x.Id))).ToArray();
        var results = await Task.WhenAll(requests.Select(x=>Streams(type,x.Id,[x.Addon],token)));
        return new(results.SelectMany(x=>x.Items).ToArray(),results.Sum(x=>x.Failures),results.SelectMany(x=>x.Diagnostics ?? []).ToArray());
    }
    public async Task<ProviderResult<StreamChoice>> Streams(string type, string id, IReadOnlyList<InstalledAddon> installed, CancellationToken token)
    {
        var results = await Task.WhenAll(installed.Select(async addon =>
        {
            ProviderResult<StreamChoice> Result(IReadOnlyList<StreamChoice> items, string message, bool failed = false) =>
                new(items, failed ? 1 : 0, [new(addon.ManifestUrl, message, failed, DateTime.UtcNow)]);
            try
            {
                var queryId = ProviderId(addon, type, id);
                if (queryId == null || !Supports(await Manifest(addon, token), "stream", type, queryId)) return Result([], "Does not offer streams for this title type or shared ID.");
                var response = await Request(AddonClient.Endpoint(addon.ManifestUrl, "stream", type, queryId), token);
                if (!response.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
                    return Result([], "The provider returned an invalid stream response.", true);
                var items = streams.EnumerateArray().Select(x => ParseStream(addon.Name, x, addon.ManifestUrl.StartsWith("cloudstream://"))).ToArray();
                return Result(items, items.Length == 0 ? "Responded, but returned no streams for the last title checked." : $"Returned {items.Length} stream(s); {items.Count(x => x.UnsupportedReason == null)} supported. Playback has not been verified.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) { return Result([], AddonHealth.Failure(error), true); }
        }));
        return new(results.SelectMany(x => x.Items).ToArray(), results.Sum(x => x.Failures), results.SelectMany(x => x.Diagnostics ?? []).ToArray());
    }
    public async Task<ProviderResult<SubtitleChoice>> Subtitles(string type, string id, IReadOnlyList<InstalledAddon> installed, CancellationToken token)
    {
        var results = await Task.WhenAll(installed.Select(async addon =>
        {
            try
            {
                var queryId = ProviderId(addon, type, id);
                if (queryId == null || !Supports(await Manifest(addon, token), "subtitles", type, queryId)) return new ProviderResult<SubtitleChoice>([], 0);
                var response = await Request(AddonClient.Endpoint(addon.ManifestUrl, "subtitles", type, queryId), token);
                if (!response.TryGetProperty("subtitles", out var items) || items.ValueKind != JsonValueKind.Array) return new ProviderResult<SubtitleChoice>([], 0);
                var choices = items.EnumerateArray().Take(400).Select(x => new SubtitleChoice(addon.Name,
                    AddonClient.Text(x, "id"), AddonClient.Text(x, "lang"), AddonClient.Text(x, "label"), AddonClient.Text(x, "url"), cloudHeaders(x)))
                    .Where(x => x.Id.Length > 0 && x.Language.Length > 0 && OnlineSubtitles.IsDownloadUrl(x.Url)).ToArray();
                return new ProviderResult<SubtitleChoice>(choices, 0);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return new ProviderResult<SubtitleChoice>([], 1); }
        }));
        return new(results.SelectMany(x => x.Items).DistinctBy(x => (x.Language, x.Url)).Take(800).ToArray(), results.Sum(x => x.Failures));
    }
    private static IReadOnlyDictionary<string,string>? cloudHeaders(JsonElement item) => item.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object
        ? headers.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.String).ToDictionary(x => x.Name, x => x.Value.GetString()!, StringComparer.OrdinalIgnoreCase) : null;
    public static StreamChoice ParseStream(string provider, JsonElement stream, bool cloudstream = false)
    {
        var url = AddonClient.Text(stream, "url");
        string? reason = null;
        TorrentSource? torrent = null;
        if (stream.TryGetProperty("infoHash", out _))
        {
            try { torrent = TorrentSource.Parse(stream); }
            catch (FormatException error) { reason = error.Message; }
        }
        else if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) reason = "External or unsupported playback source";
        Dictionary<string, string>? headers = null;
        if (cloudstream && stream.TryGetProperty("drm", out var drm) && drm.ValueKind == JsonValueKind.True) reason = "DRM streams are not supported by this bridge";
        if (stream.TryGetProperty("behaviorHints", out var behavior) && behavior.ValueKind == JsonValueKind.Object && behavior.TryGetProperty("proxyHeaders", out var proxy))
        {
            if (proxy.ValueKind != JsonValueKind.Object) reason = "This provider returned invalid request headers";
            else
            {
                if (proxy.TryGetProperty("request", out var request))
                {
                    if (request.ValueKind != JsonValueKind.Object || request.EnumerateObject().Any(x=>x.Value.ValueKind != JsonValueKind.String)) reason = "This provider returned invalid request headers";
                    else
                    {
                        headers = new(StringComparer.OrdinalIgnoreCase);
                        foreach(var header in request.EnumerateObject())
                            if(!headers.TryAdd(header.Name,header.Value.GetString()!)) reason = "This provider returned duplicate request headers";
                        if (!HeaderStreamRelay.ValidHeaders(headers)) reason = "This provider returned invalid request headers";
                    }
                }
                if(proxy.TryGetProperty("response",out var response) && (response.ValueKind != JsonValueKind.Object || response.EnumerateObject().Any()))
                    reason = "Custom response headers are not supported yet";
            }
        }
        var name = AddonClient.Text(stream, "name", "Direct stream");
        var description = AddonClient.Text(stream, "description", AddonClient.Text(stream, "title"));
        var evidence = SourceSelection.Read(stream, name, description);
        if (torrent != null && evidence.Bytes > TorrentPlayback.CacheLimit) reason = "Above the 32 GB torrent cache limit";
        var audioUrl = cloudstream ? AddonClient.Text(stream, "audioUrl") : "";
        var audioOptions = cloudstream && stream.TryGetProperty("audioTracks", out var audioTracks) ? StreamAudioOption.Parse(audioTracks) : [];
        if (audioOptions.Count > 0) audioUrl = audioOptions[0].Url;
        if (audioUrl.Length > 0 && (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var audio) || audio.Scheme is not ("http" or "https"))) reason = "Unsupported external audio address";
        var subtitles = cloudstream && stream.TryGetProperty("subtitles", out var subs) && subs.ValueKind == JsonValueKind.Array
            ? subs.EnumerateArray().Select(x => new SubtitleChoice(provider, AddonClient.Text(x, "id"), AddonClient.Text(x, "lang"), AddonClient.Text(x, "label"), AddonClient.Text(x, "url"), cloudHeaders(x))).Where(x => OnlineSubtitles.IsDownloadUrl(x.Url)).ToArray() : [];
        return new(provider, name, description, url, reason, torrent, evidence, headers, audioUrl.Length == 0 ? null : audioUrl, audioOptions, subtitles);
    }
    public static TitleInfo ParseTitle(JsonElement meta, string addonId, string fallbackType)
    {
        var episodes = new List<EpisodeInfo>();
        if (meta.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array)
            foreach (var video in videos.EnumerateArray())
            {
                var id = AddonClient.Text(video, "id");
                if (id.Length == 0) continue;
                episodes.Add(new(id, AddonClient.Text(video, "title", AddonClient.Text(video, "name", id)), Number(video, "season"), Number(video, "episode"), AddonClient.Text(video, "overview"), !video.TryGetProperty("bingexNumberingKnown", out var known) || known.ValueKind == JsonValueKind.True));
            }
        var genres = meta.TryGetProperty("genres", out var genre) && genre.ValueKind == JsonValueKind.Array ? string.Join("  ·  ", genre.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString())) : "";
        var rating = meta.TryGetProperty("imdbRating", out var r) ? r.ToString() : "";
        return new(AddonClient.Text(meta, "id"), AddonClient.Text(meta, "type", fallbackType), AddonClient.Text(meta, "name", "Untitled"),
            AddonClient.Text(meta, "poster"), AddonClient.Text(meta, "background"), AddonClient.Text(meta, "description"),
            AddonClient.Text(meta, "releaseInfo", AddonClient.Text(meta, "year")), rating, genres, addonId, episodes.OrderBy(x => x.Season).ThenBy(x => x.Number).ToArray(), RuntimeSeconds(meta));
    }
    private static int? RuntimeSeconds(JsonElement meta)
    {
        if (!meta.TryGetProperty("runtime", out var runtime)) return null;
        var value = runtime.ValueKind == JsonValueKind.String ? runtime.GetString() : runtime.ValueKind == JsonValueKind.Number ? runtime.ToString() : null;
        var match = System.Text.RegularExpressions.Regex.Match(value ?? "", @"^\s*(\d{1,4})(?:\s*(?:min|minutes?))?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var minutes) && minutes is > 0 and <= 1440 ? minutes * 60 : null;
    }
    private static int Number(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
}
