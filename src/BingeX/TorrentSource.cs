using System.Text.Json;
using System.Text.RegularExpressions;

namespace BingeX;

public record TorrentSource(string InfoHash, int? FileIndex, string[] Trackers, string[] Peers, string? TorrentFile = null)
{
    public static TorrentSource FromFile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 8 * 1024 * 1024) throw new IOException("Choose a readable .torrent file smaller than 8 MB.");
        var torrent = MonoTorrent.Torrent.Load(path);
        if (torrent.InfoHashes.V1 == null) throw new IOException("V2-only torrents are not supported in this preview.");
        return new(torrent.InfoHashes.V1.ToHex(), null, [], [], Path.GetFullPath(path));
    }
    public static TorrentSource Parse(JsonElement stream)
    {
        var hash = AddonClient.Text(stream, "infoHash").ToLowerInvariant();
        if (!Regex.IsMatch(hash, "^[a-f0-9]{40}$")) throw new FormatException("This torrent has an invalid or unsupported info hash.");
        int? index = null;
        if (stream.TryGetProperty("fileIdx", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < 0) throw new FormatException("This torrent has an invalid video file index.");
            index = number;
        }
        var trackers = new List<string>();
        var peers = new List<string>();
        if (stream.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array)
            foreach (var source in sources.EnumerateArray().Take(64))
            {
                if (source.ValueKind != JsonValueKind.String) continue;
                var text = source.GetString() ?? "";
                if (text.Length > 2048) continue;
                if (text.StartsWith("tracker:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(text[8..], UriKind.Absolute, out var tracker) && tracker.Scheme is "udp" or "https" or "http") trackers.Add(tracker.AbsoluteUri);
                else if (text.StartsWith("peer:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate("ipv4://" + text[5..], UriKind.Absolute, out var peer) && peer.Port > 0 && peer.HostNameType == UriHostNameType.IPv4) peers.Add(peer.AbsoluteUri);
            }
        return new(hash, index, trackers.Distinct().ToArray(), peers.Distinct().ToArray());
    }
}
