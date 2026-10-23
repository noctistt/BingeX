using System.Globalization;
using System.Text.RegularExpressions;
namespace BingeX;
public record LyricLine(long Time,string Text);
public sealed record MusicLyrics(LyricLine[] Lines,string Plain)
{
    public static MusicLyrics Parse(string text)
    {
        if(text.Length>256_000)throw new InvalidDataException();
        var offset=Regex.Match(text,@"\[offset:\s*([+-]?\d+)\]",RegexOptions.IgnoreCase);
        var shift=offset.Success&&long.TryParse(offset.Groups[1].Value,out var value)?Math.Clamp(value,-600_000,600_000):0;
        var lines=new List<LyricLine>();var plain=new List<string>();
        foreach(var raw in text.Replace("\r","").Split('\n')){
            var stamps=Regex.Matches(raw,@"\[(\d{1,3}):([0-5]\d)(?:[.:](\d{1,3}))?\]");
            var content=Regex.Replace(Regex.Replace(raw,@"\[[^\]]*\]",""),@"<\d{1,3}:[0-5]\d(?:\.\d{1,3})?>","").Trim().Replace("\0","");
            if(content.Length>2000)content=content[..2000];
            if(content.Length>0)plain.Add(content);
            foreach(Match stamp in stamps){
                if(lines.Count>=4000)throw new InvalidDataException();
                var fraction=stamp.Groups[3].Value.PadRight(3,'0');
                var time=(long.Parse(stamp.Groups[1].Value,CultureInfo.InvariantCulture)*60+long.Parse(stamp.Groups[2].Value,CultureInfo.InvariantCulture))*1000+long.Parse(fraction,CultureInfo.InvariantCulture)-shift;
                lines.Add(new(Math.Max(0,time),content));
            }
        }
        var grouped=lines.OrderBy(x=>x.Time).GroupBy(x=>x.Time).Select(x=>new LyricLine(x.Key,string.Join("\n",x.Select(y=>y.Text).Distinct()))).ToArray();
        return new(grouped,string.Join("\n",plain));
    }
    public int Active(long playbackMilliseconds,long delayMilliseconds)
    {
        var target=playbackMilliseconds-delayMilliseconds;var low=0;var high=Lines.Length-1;var found=-1;
        while(low<=high){var mid=low+(high-low)/2;if(Lines[mid].Time<=target){found=mid;low=mid+1;}else high=mid-1;}return found;
    }
}
