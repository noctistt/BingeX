using System.Collections.Concurrent;
using System.Text.Json;

namespace BingeX;

public record CatalogInfo(InstalledAddon Addon, string Id, string Type, string Name, bool Searchable, bool Browsable)
{
    public override string ToString() => $"{Name} · {Type} · {Addon.Name}";
}
public record EpisodeInfo(string Id, string Name, int Season, int Number, string Overview);
public record TitleInfo(string Id, string Type, string Name, string Poster, string Background, string Description,
    string Release, string Rating, string Genres, string AddonId, IReadOnlyList<EpisodeInfo> Episodes);
public record MediaContext(string Id, string Type, string Name, string Poster, string AddonId, string? EpisodeId = null, string? EpisodeName = null)
{
    public string HistoryKey => $"stremio:{Type}:{EpisodeId ?? Id}";
    public string DisplayName => EpisodeName == null ? Name : $"{Name} · {EpisodeName}";
}
public record StreamChoice(string Provider, string Name, string Description, string Url, string? UnsupportedReason, TorrentSource? Torrent = null);
public record SubtitleChoice(string Provider, string Id, string Language, string Label, string Url);
public record ProviderResult<T>(IReadOnlyList<T> Items, int Failures);

public sealed class MediaLibrary(AddonClient client)
{
    private readonly ConcurrentDictionary<string, JsonElement> manifests = new();
    private readonly SemaphoreSlim requests = new(5);

    public void Invalidate() => manifests.Clear();
    private async Task<JsonElement> Request(string url, CancellationToken token)
    {
        await requests.WaitAsync(token);
        try { return await client.Get(url, token); }
        finally { requests.Release(); }
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
        var response = await Request(AddonClient.Endpoint(catalog.Addon.ManifestUrl, "catalog", catalog.Type, catalog.Id, extra), token);
        if (!response.TryGetProperty("metas", out var metas) || metas.ValueKind != JsonValueKind.Array) return [];
        return metas.EnumerateArray().Where(x => AddonClient.Text(x, "id").Length > 0).Take(100).Select(x => ParseTitle(x, catalog.Addon.Id, catalog.Type)).ToArray();
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
                    return ParseTitle(meta, addon.Id, preview.Type) with { Poster = AddonClient.Text(meta, "poster", preview.Poster) };
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { /* Try the next compatible metadata provider. */ }
        }
        return preview;
    }
    public async Task<ProviderResult<StreamChoice>> Streams(string type, string id, IReadOnlyList<InstalledAddon> installed, CancellationToken token)
    {
        var results = await Task.WhenAll(installed.Select(async addon =>
        {
            try
            {
                if (!Supports(await Manifest(addon, token), "stream", type, id)) return new ProviderResult<StreamChoice>([], 0);
                var response = await Request(AddonClient.Endpoint(addon.ManifestUrl, "stream", type, id), token);
                if (!response.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array) return new ProviderResult<StreamChoice>([], 0);
                return new ProviderResult<StreamChoice>(streams.EnumerateArray().Select(x => ParseStream(addon.Name, x)).ToArray(), 0);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return new ProviderResult<StreamChoice>([], 1); }
        }));
        return new(results.SelectMany(x => x.Items).ToArray(), results.Sum(x => x.Failures));
    }
    public async Task<ProviderResult<SubtitleChoice>> Subtitles(string type, string id, IReadOnlyList<InstalledAddon> installed, CancellationToken token)
    {
        var results = await Task.WhenAll(installed.Select(async addon =>
        {
            try
            {
                if (!Supports(await Manifest(addon, token), "subtitles", type, id)) return new ProviderResult<SubtitleChoice>([], 0);
                var response = await Request(AddonClient.Endpoint(addon.ManifestUrl, "subtitles", type, id), token);
                if (!response.TryGetProperty("subtitles", out var items) || items.ValueKind != JsonValueKind.Array) return new ProviderResult<SubtitleChoice>([], 0);
                var choices = items.EnumerateArray().Take(400).Select(x => new SubtitleChoice(addon.Name,
                    AddonClient.Text(x, "id"), AddonClient.Text(x, "lang"), AddonClient.Text(x, "label"), AddonClient.Text(x, "url")))
                    .Where(x => x.Id.Length > 0 && x.Language.Length > 0 && OnlineSubtitles.IsDownloadUrl(x.Url)).ToArray();
                return new ProviderResult<SubtitleChoice>(choices, 0);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return new ProviderResult<SubtitleChoice>([], 1); }
        }));
        return new(results.SelectMany(x => x.Items).DistinctBy(x => (x.Language, x.Url)).Take(800).ToArray(), results.Sum(x => x.Failures));
    }
    public static StreamChoice ParseStream(string provider, JsonElement stream)
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
        else if (stream.TryGetProperty("behaviorHints", out var hints) && hints.ValueKind == JsonValueKind.Object && hints.TryGetProperty("proxyHeaders", out _)) reason = "Requires custom request headers";
        return new(provider, AddonClient.Text(stream, "name", "Direct stream"), AddonClient.Text(stream, "description", AddonClient.Text(stream, "title")), url, reason, torrent);
    }
    public static TitleInfo ParseTitle(JsonElement meta, string addonId, string fallbackType)
    {
        var episodes = new List<EpisodeInfo>();
        if (meta.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array)
            foreach (var video in videos.EnumerateArray())
            {
                var id = AddonClient.Text(video, "id");
                if (id.Length == 0) continue;
                episodes.Add(new(id, AddonClient.Text(video, "title", AddonClient.Text(video, "name", id)), Number(video, "season"), Number(video, "episode"), AddonClient.Text(video, "overview")));
            }
        var genres = meta.TryGetProperty("genres", out var genre) && genre.ValueKind == JsonValueKind.Array ? string.Join("  ·  ", genre.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString())) : "";
        var rating = meta.TryGetProperty("imdbRating", out var r) ? r.ToString() : "";
        return new(AddonClient.Text(meta, "id"), AddonClient.Text(meta, "type", fallbackType), AddonClient.Text(meta, "name", "Untitled"),
            AddonClient.Text(meta, "poster"), AddonClient.Text(meta, "background"), AddonClient.Text(meta, "description"),
            AddonClient.Text(meta, "releaseInfo", AddonClient.Text(meta, "year")), rating, genres, addonId, episodes.OrderBy(x => x.Season).ThenBy(x => x.Number).ToArray());
    }
    private static int Number(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
}
