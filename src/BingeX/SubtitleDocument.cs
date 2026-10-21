using System.Net;
using System.Text.RegularExpressions;

namespace BingeX;

public record SubtitleCue(long Start, long End, string Text);

public sealed partial class SubtitleDocument
{
    public IReadOnlyList<SubtitleCue> Cues { get; }
    private readonly long[] maximumEnd;

    private SubtitleDocument(IEnumerable<SubtitleCue> cues)
    {
        // Segment boundaries can repeat the same cue with a short gap. Merge
        // only equal text that overlaps or is separated by at most 100 ms.
        // Keep real repetitions after a pause and different overlapping dialogue.
        var merged = new List<SubtitleCue>();
        foreach (var group in cues.GroupBy(x => x.Text, StringComparer.Ordinal))
        {
            SubtitleCue? previous = null;
            foreach (var cue in group.OrderBy(x => x.Start).ThenBy(x => x.End))
            {
                if (previous != null && cue.Start <= previous.End + 100)
                    previous = previous with { End = Math.Max(previous.End, cue.End) };
                else
                {
                    if (previous != null) merged.Add(previous);
                    previous = cue;
                }
            }
            if (previous != null) merged.Add(previous);
        }
        Cues = merged.OrderBy(x => x.Start).ThenBy(x => x.End).ToArray();
        maximumEnd = new long[Cues.Count];
        for (var i = 0; i < Cues.Count; i++) maximumEnd[i] = Math.Max(i == 0 ? 0 : maximumEnd[i - 1], Cues[i].End);
    }
    public static SubtitleDocument Parse(string text)
    {
        var lines = text.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cues = new List<SubtitleCue>();
        for (var i = 0; i < lines.Length; i++)
        {
            var arrow = lines[i].IndexOf("-->", StringComparison.Ordinal);
            if (arrow < 0) continue;
            var left = lines[i][..arrow].Trim();
            var right = lines[i][(arrow + 3)..].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (!Timestamp(left, out var start) || !Timestamp(right, out var end) || end <= start) continue;
            var content = new List<string>();
            while (++i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])) content.Add(lines[i]);
            var plain = WebUtility.HtmlDecode(Markup().Replace(string.Join("\n", content), ""));
            plain = Overrides().Replace(plain, "").Trim();
            if (plain.Length > 0) cues.Add(new(start, end, plain[..Math.Min(10000, plain.Length)]));
        }
        if (cues.Count == 0) throw new FormatException("No valid subtitle cues were found.");
        return new(cues);
    }
    public string TextAt(long playbackMilliseconds, long delayMilliseconds)
    {
        var time = playbackMilliseconds - delayMilliseconds;
        var low = 0; var high = Cues.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Cues[middle].Start <= time) low = middle + 1; else high = middle;
        }
        var active = new List<string>();
        for (var i = low - 1; i >= 0 && maximumEnd[i] > time; i--)
        {
            if (Cues[i].End > time) active.Add(Cues[i].Text);
            if (active.Count == 4) break;
        }
        active.Reverse();
        return string.Join("\n", active);
    }
    private static bool Timestamp(string text, out long milliseconds)
    {
        milliseconds = 0;
        var match = Timecode().Match(text);
        if (!match.Success) return false;
        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        var minutes = int.Parse(match.Groups[2].Value);
        var seconds = int.Parse(match.Groups[3].Value);
        if (minutes > 59 || seconds > 59) return false;
        var fraction = int.Parse(match.Groups[4].Value.PadRight(3, '0'));
        milliseconds = ((hours * 60L + minutes) * 60 + seconds) * 1000 + fraction;
        return true;
    }
    [GeneratedRegex(@"^(?:(\d{1,3}):)?(\d{2}):(\d{2})[.,](\d{1,3})$")]
    private static partial Regex Timecode();
    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Markup();
    [GeneratedRegex(@"\{\\[^}]*\}")]
    private static partial Regex Overrides();
}
