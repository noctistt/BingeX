using System.Text.RegularExpressions;
namespace BingeX;
public enum SourceAudioKind { Unknown, Subbed, Dubbed, DualAudio }
public record SourceAudioInfo(SourceAudioKind Kind,IReadOnlyList<string> Languages)
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StreamChoice,SourceAudioInfo> cache=new();
    public static SourceAudioInfo Parse(StreamChoice source)=>cache.GetValue(source,Read);
    private static SourceAudioInfo Read(StreamChoice source)
    {
        var text=source.Name+" "+source.Description;
        bool Has(string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        var languages=(source.AudioOptions ?? []).Select(x=>x.Language).Where(x=>!string.IsNullOrWhiteSpace(x) && x!="und")
            .Select(SubtitlePreference.LanguageKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var dual=Has(@"\b(?:dual|multi)[\s-]*(?:audio|language)\b") || languages.Count>1;
        var dub=Has(@"\b(?:dub|dubbed)\b"); var sub=Has(@"\b(?:sub|subbed)\b");
        foreach(var name in new[]{"English","Hindi","Japanese","Chinese","Korean","Tamil","Telugu","Malayalam","Kannada","Spanish","Portuguese","French","German","Russian","Arabic","Italian","Indonesian","Thai"})
            if((Has(@"\b"+name+@"[\s._-]+(?:dub(?:bed)?|audio)\b") || Has(@"\b(?:dub(?:bed)?|audio)\W+"+name+@"\b")) && !languages.Contains(name.ToLowerInvariant())) languages.Add(name.ToLowerInvariant());
        return new(dual ? SourceAudioKind.DualAudio : dub && !sub ? SourceAudioKind.Dubbed : sub && !dub ? SourceAudioKind.Subbed : SourceAudioKind.Unknown,languages);
    }
    public string Label => Kind switch { SourceAudioKind.Subbed=>"Subbed",SourceAudioKind.Dubbed=>"Dubbed",SourceAudioKind.DualAudio=>"Dual audio",_=>"Unknown audio type" };
}
public record ShowAudioPreference(string Language="",string Label="",string SourceFilter="All");
