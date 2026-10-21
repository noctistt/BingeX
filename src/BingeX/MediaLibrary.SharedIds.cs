using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BingeX;

public sealed partial class MediaLibrary
{
    private record SharedEntry(string Type,string Id,string Shared);
    private readonly ConcurrentDictionary<(string Type,string Id),string> sharedIds = ReadSharedIds(sharedIdFile);
    private static ConcurrentDictionary<(string Type,string Id),string> ReadSharedIds(string? file)
    {
        var result = new ConcurrentDictionary<(string Type,string Id),string>();
        try
        {
            if (file != null && File.Exists(file))
                foreach(var row in (JsonSerializer.Deserialize<SharedEntry[]>(File.ReadAllText(file)) ?? []).Take(10000))
                    if (row.Id.StartsWith("cs:") && row.Type is "movie" or "series" && Regex.IsMatch(row.Shared,@"^tt\d{5,12}(?::\d{1,3}:\d{1,4})?$")) result[(row.Type,row.Id)] = row.Shared;
        }
        catch { }
        return result;
    }
    public bool HasSharedId(string type, string id) => !id.StartsWith("cs:") || sharedIds.ContainsKey((type,id));
    private string? ProviderId(InstalledAddon addon, string type, string id) => addon.ManifestUrl.StartsWith("cloudstream://") || !id.StartsWith("cs:") ? id : sharedIds.GetValueOrDefault((type,id));
    private void RegisterSharedIds(JsonElement meta, string type)
    {
        void Register(JsonElement item, string kind)
        {
            var id = AddonClient.Text(item,"id"); var shared = AddonClient.Text(item,"bingexStremioId");
            var pattern = kind == "series" ? @"^tt\d{5,12}(?::\d{1,3}:\d{1,4})?$" : @"^tt\d{5,12}$";
            if (id.StartsWith("cs:") && Regex.IsMatch(shared,pattern)) sharedIds[(kind,id)] = shared;
        }
        Register(meta,type);
        var rootId = AddonClient.Text(meta,"id");
        var rootShared = sharedIds.GetValueOrDefault((type,rootId));
        if (meta.TryGetProperty("videos",out var videos) && videos.ValueKind == JsonValueKind.Array)
            foreach(var video in videos.EnumerateArray())
            {
                Register(video,"series");
                if (rootShared != null && Regex.IsMatch(rootShared,@"^tt\d{5,12}$") && video.TryGetProperty("bingexNumberingKnown",out var known) && known.ValueKind == JsonValueKind.True)
                    sharedIds[("series",AddonClient.Text(video,"id"))] = $"{rootShared}:{Number(video,"season")}:{Number(video,"episode")}";
            }
    }
    // Called only after the user explicitly selects a catalog title.
    public void LinkTitle(TitleInfo extensionTitle, TitleInfo catalogTitle)
    {
        if (!extensionTitle.Id.StartsWith("cs:") || extensionTitle.Type != catalogTitle.Type || !Regex.IsMatch(catalogTitle.Id,@"^tt\d{5,12}$")) throw new InvalidOperationException("Choose a matching IMDb catalog title.");
        sharedIds[(extensionTitle.Type,extensionTitle.Id)] = catalogTitle.Id;
        if (extensionTitle.Type == "series")
            foreach(var episode in extensionTitle.Episodes.Where(x => x.NumberingKnown && x.Season >= 0 && x.Number > 0))
                sharedIds[("series",episode.Id)] = $"{catalogTitle.Id}:{episode.Season}:{episode.Number}";
        if (sharedIdFile != null)
        {
            File.WriteAllText(sharedIdFile + ".tmp",JsonSerializer.Serialize(sharedIds.Take(10000).Select(x => new SharedEntry(x.Key.Type,x.Key.Id,x.Value))));
            File.Move(sharedIdFile + ".tmp",sharedIdFile,true);
        }
    }
}
