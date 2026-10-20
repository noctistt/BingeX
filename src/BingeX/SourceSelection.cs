using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BingeX;

public record SourceEvidence(long? Bytes, int? Seeders, int? Resolution);
public record SourcePerformance(bool Failed, long BytesPerSecond);

public static class SourceSelection
{
    public static string Key(StreamChoice source) => source.Torrent is { } torrent
        ? $"torrent:{torrent.InfoHash}:{torrent.FileIndex?.ToString() ?? "largest"}" : source.Url.Length > 0 ? source.Url : $"unsupported:{source.Provider}:{source.Name}:{source.Description}";
    public static SourceEvidence Read(JsonElement stream, string name, string description)
    {
        long? bytes = PositiveLong(stream, "size");
        if (stream.TryGetProperty("behaviorHints", out var hints) && hints.ValueKind == JsonValueKind.Object) bytes = PositiveLong(hints, "videoSize") ?? bytes;
        var text = name + " " + description;
        var size = Regex.Match(text, @"(?<n>\d+(?:[.,]\d+)?)\s*(?<u>GiB|GB|MiB|MB)\b", RegexOptions.IgnoreCase);
        if (bytes == null && size.Success && double.TryParse(size.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture, out var amount))
        {
            var value = amount * (size.Groups["u"].Value.StartsWith("G", StringComparison.OrdinalIgnoreCase) ? 1024d * 1024 * 1024 : 1024d * 1024);
            if (value > 0 && value < long.MaxValue) bytes = (long)value;
        }
        int? seeds = PositiveLong(stream, "seeders") is { } numeric && numeric <= int.MaxValue ? (int)numeric : null;
        var match = Regex.Match(text, @"(?:👤|👥|seeders?\s*[:=]?)\s*(\d+)\b", RegexOptions.IgnoreCase);
        if (seeds == null && match.Success && int.TryParse(match.Groups[1].Value, out var reported)) seeds = reported;
        int? resolution = Regex.IsMatch(text, @"\b(?:2160p|4k)\b", RegexOptions.IgnoreCase) ? 2160
            : Regex.IsMatch(text, @"\b1080p?\b", RegexOptions.IgnoreCase) ? 1080
            : Regex.IsMatch(text, @"\b720p?\b", RegexOptions.IgnoreCase) ? 720 : null;
        return new(bytes, seeds, resolution);
    }
    private static long? PositiveLong(JsonElement value, string key) => value.TryGetProperty(key, out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt64(out var result) && result >= 0 ? result : null;
    public static IReadOnlyList<StreamChoice> Rank(IEnumerable<StreamChoice> sources, IReadOnlyDictionary<string, SourcePerformance> performance) => sources
        .Select((source, index) => (source, index)).OrderByDescending(x => Score(x.source, performance)).ThenBy(x => x.index)
        .DistinctBy(x => Key(x.source)).Select(x => x.source).ToArray();
    private static double Score(StreamChoice source, IReadOnlyDictionary<string, SourcePerformance> performance)
    {
        if (source.UnsupportedReason != null) return -10000;
        var evidence = source.Evidence;
        double score = source.Torrent == null ? 100 : 0;
        score += evidence?.Resolution switch { 1080 => 30, 720 => 20, 2160 => -5, _ => 0 };
        if (evidence?.Bytes is { } bytes) score -= bytes / (1024d * 1024 * 1024);
        if (evidence?.Seeders is { } seeds) score += seeds == 0 ? -40 : Math.Log10(1 + seeds) * 12;
        if (performance.TryGetValue(Key(source), out var measured)) score += measured.Failed ? -300 : Math.Min(50, measured.BytesPerSecond / (1024d * 1024) * 5);
        return score;
    }
    public static string Summary(StreamChoice source)
    {
        var evidence = source.Evidence;
        return string.Join(" · ", new[] { evidence?.Resolution is { } resolution ? resolution + "p" : "", evidence?.Bytes is { } size ? $"{size / (1024d * 1024 * 1024):0.##} GB" : "", evidence?.Seeders is { } seeds ? $"{seeds} advertised seeders" : "" }.Where(x => x.Length > 0));
    }
}

public sealed class SourceAttemptPlan(IReadOnlyList<StreamChoice> sources)
{
    private readonly HashSet<string> tried = new(StringComparer.Ordinal);
    public void Mark(StreamChoice source) => tried.Add(SourceSelection.Key(source));
    public StreamChoice? Next => tried.Count >= 3 ? null : sources.FirstOrDefault(x => x.UnsupportedReason == null && !tried.Contains(SourceSelection.Key(x)));
}
