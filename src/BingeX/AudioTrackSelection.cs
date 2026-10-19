namespace BingeX;

public sealed record AudioTrackSelection(int Id, string Name)
{
    public int? Resolve(IEnumerable<(int Id, string Name)> available)
    {
        var tracks = available.Where(x => x.Id >= 0).ToArray();
        // IDs can change when a catalog title obtains a fresh streaming source.
        var named = tracks.Where(x => string.Equals(x.Name, Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Any(x => x.Id == Id)) return Id;
        if (Name.Length > 0) return named.Length == 1 ? named[0].Id : null;
        return tracks.Any(x => x.Id == Id) ? Id : null;
    }
}
