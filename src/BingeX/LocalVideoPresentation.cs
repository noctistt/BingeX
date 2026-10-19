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
