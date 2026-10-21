using System.Text.RegularExpressions;

namespace BingeX;

public record LocalEpisode(string Show, int Season, int Episode);

public static class LocalVideoPresentation
{
    public static LocalEpisode? Episode(LocalVideo video)
    {
        var match = Regex.Match(video.Title, @"^(?<show>.*?)(?<![A-Za-z0-9])[Ss](?<s>\d{1,3})[ ._-]*[Ee](?<e>\d{1,4})\b|^(?<show>.*?)(?<![A-Za-z0-9])(?<s>\d{1,3})[xX](?<e>\d{1,4})\b");
        var parent = Path.GetDirectoryName(video.RelativePath) ?? "";
        if (match.Success)
        {
            var show = Clean(match.Groups["show"].Value);
            if (show.Length == 0) show = FolderShow(video, parent);
            return new(show, int.Parse(match.Groups["s"].Value), int.Parse(match.Groups["e"].Value));
        }
        var season = Regex.Match(Path.GetFileName(parent), @"^(?:Season\s*|S)(\d{1,3})$", RegexOptions.IgnoreCase);
        var episode = Regex.Match(video.Title, @"^(?:Episode\s*|E)(\d{1,4})\b", RegexOptions.IgnoreCase);
        return season.Success && episode.Success ? new(FolderShow(video, parent), int.Parse(season.Groups[1].Value), int.Parse(episode.Groups[1].Value)) : null;
    }
    public static bool Matches(LocalVideo video,string query)
    {
        var text=Clean(video.Title+" "+video.RelativePath);
        return query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).All(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase));
    }
    public static IReadOnlyList<LocalVideoGroup> Groups(IEnumerable<LocalVideo> videos,int grouping,int sort)
    {
        var ordered=Sort(videos,sort).ToArray();
        if(grouping==2)return ordered.Length==0 ? [] : [new("all","All videos",ordered)];
        return ordered.GroupBy(video=>
        {
            var parent=Path.GetDirectoryName(video.RelativePath) ?? "";
            if(grouping==1 && Episode(video) is {} episode)
            {
                var anchor=Regex.IsMatch(Path.GetFileName(parent),@"^(?:Season\s*|S)\d+$",RegexOptions.IgnoreCase) ? Path.GetDirectoryName(parent) ?? "" : parent;
                return (Key:video.Folder+"|"+anchor+"|"+episode.Show+"|"+episode.Season,Name:episode.Show+" · Season "+episode.Season);
            }
            return (Key:video.Folder+"|"+parent,Name:parent.Length>0 ? parent : Path.GetFileName(video.Folder));
        }).Select(group=>new LocalVideoGroup(group.Key.Key,group.Key.Name,
            (sort==0 && grouping==1 ? group.OrderBy(x=>Episode(x)?.Episode ?? 0).ThenBy(x=>x.Title,StringComparer.OrdinalIgnoreCase) : group.AsEnumerable()).ToArray())).ToArray();
    }
    private static string FolderShow(LocalVideo video, string parent)
    {
        if (Regex.IsMatch(Path.GetFileName(parent), @"^(?:Season\s*|S)\d+$", RegexOptions.IgnoreCase)) parent = Path.GetDirectoryName(parent) ?? "";
        return Clean(parent.Length > 0 ? Path.GetFileName(parent) : Path.GetFileName(video.Folder));
    }
    private static string Clean(string value) => Regex.Replace(value.Trim(' ', '.', '_', '-'), @"[._]+", " ").Trim();
    public static IEnumerable<LocalVideo> Sort(IEnumerable<LocalVideo> videos, int sort) => sort switch
    {
        1 => videos.OrderByDescending(x => x.AddedTicks).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase),
        2 => videos.OrderByDescending(x => x.LastWatchedTicks).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase),
        _ => videos.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
    };
}

public record LocalVideoGroup(string Key,string Name,IReadOnlyList<LocalVideo> Videos);
