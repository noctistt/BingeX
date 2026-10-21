using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BingeX;

public record StreamAudioOption(string Url, string Label, string Language, bool Original)
{
    public override string ToString() => Label;
    public static IReadOnlyList<StreamAudioOption> Parse(JsonElement tracks)
    {
        if (tracks.ValueKind != JsonValueKind.Array) return [];
        var result = new List<StreamAudioOption>();
        foreach (var track in tracks.EnumerateArray())
        {
            var url = AddonClient.Text(track, "url");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) continue;
            if (track.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object && headers.EnumerateObject().Any()) continue;
            var query = uri.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2))
                .Where(x => x.Length == 2).GroupBy(x => Uri.UnescapeDataString(x[0]), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => Uri.UnescapeDataString(x.First()[1]), StringComparer.OrdinalIgnoreCase);
            var tags = query.GetValueOrDefault("xtags", "");
            var language = AddonClient.Text(track, "language", query.GetValueOrDefault("lang", ""));
            var match = Regex.Match(tags, @"(?:^|:)lang=([^:]+)");
            if (language.Length == 0 && match.Success) language = match.Groups[1].Value;
            var original = tags.Split(':').Contains("acont=original");
            var label = AddonClient.Text(track, "name");
            if (label.Length == 0 && language.Length > 0)
            {
                try { label = CultureInfo.GetCultureInfo(language).EnglishName; }
                catch (CultureNotFoundException) { label = language; }
            }
            if (label.Length == 0) label = $"Audio track {result.Count + 1}";
            if (original) label += " · Original";
            // Repeated encodings of the same language are alternatives, not new languages.
            if (result.Any(x => language.Length > 0 ? x.Language == language && x.Original == original : x.Url == url)) continue;
            result.Add(new(url, label, language, original));
        }
        return result.OrderByDescending(x => x.Original).ToArray();
    }
}
