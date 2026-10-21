namespace BingeX;

public sealed record AudioTrackSelection(int Id, string Name, string Language="")
{
    public int? Resolve(IEnumerable<(int Id, string Name)> available)
    {
        var tracks = available.Where(x => x.Id >= 0).ToArray();
        // IDs can change when a catalog title obtains a fresh streaming source.
        var named = tracks.Where(x => string.Equals(x.Name, Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Any(x => x.Id == Id)) return Id;
        if(Name.Length>0 && named.Length==1)return named[0].Id;
        if(Language.Length>0)
        {
            var matching=tracks.Where(x=>SubtitlePreference.EmbeddedLanguage(x.Name)==Language).ToArray();
            return matching.Length==1 ? matching[0].Id : null;
        }
        if (Name.Length > 0) return null;
        return tracks.Any(x => x.Id == Id) ? Id : null;
    }
}
