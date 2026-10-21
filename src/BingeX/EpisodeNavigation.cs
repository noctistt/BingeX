namespace BingeX;

public static class EpisodeNavigation
{
    public static EpisodeInfo? Next(TitleInfo? title, MediaContext? context)
    {
        if (title == null || context?.EpisodeId == null || title.Id != context.Id || title.Type != context.Type) return null;
        var episodes = title.Episodes.DistinctBy(x => x.Id).ToArray();
        if (episodes.All(x => x.NumberingKnown) && episodes.Select(x => (x.Season,x.Number)).Distinct().Count()==episodes.Length)
            episodes = episodes.OrderBy(x => x.Season).ThenBy(x => x.Number).ToArray();
        var index = Array.FindIndex(episodes,x => x.Id == context.EpisodeId);
        return index >= 0 && index + 1 < episodes.Length ? episodes[index+1] : null;
    }
}
