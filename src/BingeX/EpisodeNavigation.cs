namespace BingeX;

public static class EpisodeNavigation
{
    private static string Identity(EpisodeInfo episode) => episode.NumberingKnown && episode.Season >= 0 && episode.Number > 0
        ? $"episode:{episode.Season}:{episode.Number}"
        : episode.Season >= 0 && episode.Number > 0 && episode.Name.Trim().Length > 0 && episode.Overview.Trim().Length > 0
            ? $"described:{episode.Season}:{episode.Number}:{episode.Name.Trim().Length}:{episode.Name.Trim()}:{episode.Overview.Trim()}"
            : "id:" + episode.Id;
    public static IReadOnlyList<EpisodeInfo> Entries(TitleInfo title)
    {
        var entries = title.Episodes.DistinctBy(x=>x.Id).GroupBy(Identity).Select(group=>
        {
            var first = group.First();
            return first.Overview.Length == 0 ? first with { Overview = group.Select(x=>x.Overview).FirstOrDefault(x=>x.Length>0) ?? "" } : first;
        }).ToArray();
        return entries.All(x=>x.NumberingKnown) ? entries.OrderBy(x=>x.Season).ThenBy(x=>x.Number).ToArray() : entries;
    }
    public static IReadOnlyList<EpisodeInfo> Variants(TitleInfo title,string id)
    {
        var episode = title.Episodes.FirstOrDefault(x=>x.Id==id);
        return episode == null ? [] : title.Episodes.Where(x=>Identity(x)==Identity(episode)).DistinctBy(x=>x.Id).ToArray();
    }
    public static EpisodeInfo? Next(TitleInfo? title, MediaContext? context)
    {
        if (title == null || context?.EpisodeId == null || title.Id != context.Id || title.Type != context.Type) return null;
        var current = title.Episodes.FirstOrDefault(x=>x.Id==context.EpisodeId);
        if (current == null) return null;
        var episodes = Entries(title).ToArray();
        var index = Array.FindIndex(episodes,x => Identity(x) == Identity(current));
        return index >= 0 && index + 1 < episodes.Length ? episodes[index+1] : null;
    }
}
