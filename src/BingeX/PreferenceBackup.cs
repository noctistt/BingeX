using System.Text.Json;
using System.Text.RegularExpressions;
namespace BingeX;
public static class PreferenceBackup
{
    public const int MaxBytes=65536;
    // Explicit allowlist: adding future profile data never automatically exports it.
    private static readonly string[] fields=["Volume","SubtitleSize","SubtitleColor","SubtitleBackground","AutoplayEpisodes","SearchAllStreamProviders","IncludeTorrentSources","LocalLibrarySort","LocalLibraryGrouping","SeekSeconds","ResumePlayback","ClickToPause","StartupAnimation","DefaultSpeedIndex","AutoSwitchSources","PreferredAudioLanguage","PreferredSubtitleLanguage","AccentColor","CompactBrowsing","ReducedMotion"];
    public static string Export(PlayerPreferences preferences)
    {
        var values=JsonSerializer.SerializeToElement(preferences);
        return JsonSerializer.Serialize(new{Format="BingeX.settings",Version=1,Preferences=fields.ToDictionary(x=>x,x=>values.GetProperty(x))},new JsonSerializerOptions{WriteIndented=true});
    }
    public static PlayerPreferences Import(string text)
    {
        if(System.Text.Encoding.UTF8.GetByteCount(text)>MaxBytes)throw new InvalidDataException("Settings backup exceeds 64 KB.");
        using var document=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=8});
        var root=document.RootElement;
        void Check(bool valid){if(!valid)throw new InvalidDataException("This is not a supported BingeX settings backup.");}
        Check(root.ValueKind==JsonValueKind.Object);
        var entries=root.EnumerateObject().ToArray();
        Check(entries.Length==3 && entries.Select(x=>x.Name).Distinct().Count()==3 && entries.All(x=>x.Name is "Format" or "Version" or "Preferences"));
        Check(root.GetProperty("Format").ValueKind==JsonValueKind.String && root.GetProperty("Format").GetString()=="BingeX.settings");
        Check(root.GetProperty("Version").ValueKind==JsonValueKind.Number && root.GetProperty("Version").TryGetInt32(out var version) && version==1);
        var values=root.GetProperty("Preferences");Check(values.ValueKind==JsonValueKind.Object);
        var properties=values.EnumerateObject().ToArray();
        Check(properties.Select(x=>x.Name).Distinct().Count()==properties.Length);
        var defaults=JsonSerializer.SerializeToElement(new PlayerPreferences());
        foreach(var property in properties)Check(fields.Contains(property.Name) && (property.Value.ValueKind==defaults.GetProperty(property.Name).ValueKind || (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False && defaults.GetProperty(property.Name).ValueKind is JsonValueKind.True or JsonValueKind.False)));
        var preferences=values.Deserialize<PlayerPreferences>();Check(preferences!=null);
        var p=preferences!;
        Check(p.Volume is >=0 and <=100 && new[]{20,28,36,44}.Contains(p.SubtitleSize) && new[]{"White","Yellow","Mint"}.Contains(p.SubtitleColor));
        Check(new[]{5,10,15,30,60}.Contains(p.SeekSeconds) && p.DefaultSpeedIndex is >=0 and <=5 && p.LocalLibrarySort is >=0 and <=2 && p.LocalLibraryGrouping is >=0 and <=2);
        Check(new[]{"Purple","Blue","Teal","Rose","Amber"}.Contains(p.AccentColor));
        Check(Regex.IsMatch(p.PreferredAudioLanguage,@"^[a-z]{0,32}$") && Regex.IsMatch(p.PreferredSubtitleLanguage,@"^[a-z]{0,32}$"));
        return p;
    }
    public static string Changes(PlayerPreferences current,PlayerPreferences next)
    {
        var before=JsonSerializer.SerializeToElement(current);var after=JsonSerializer.SerializeToElement(next);
        var changes=fields.Where(x=>before.GetProperty(x).GetRawText()!=after.GetProperty(x).GetRawText()).Select(x=>Label(x)+": "+Display(x,before.GetProperty(x))+" → "+Display(x,after.GetProperty(x)));
        return string.Join(Environment.NewLine,changes);
        static string Label(string field)=>field switch{
            "DefaultSpeedIndex"=>"Playback speed","LocalLibrarySort"=>"Library sorting","LocalLibraryGrouping"=>"Library grouping",
            "SeekSeconds"=>"Seek interval (seconds)","AutoSwitchSources"=>"Offer another source on failure","SearchAllStreamProviders"=>"Search all providers",
            _=>Regex.Replace(field,@"(?<!^)([A-Z])"," $1")};
        static string Display(string field,JsonElement value)
        {
            if(field=="DefaultSpeedIndex")return new[]{"0.5×","0.75×","1×","1.25×","1.5×","2×"}[Math.Clamp(value.GetInt32(),0,5)];
            if(field=="LocalLibrarySort")return new[]{"Name","Recently added","Last watched"}[Math.Clamp(value.GetInt32(),0,2)];
            if(field=="LocalLibraryGrouping")return new[]{"Folder","Series and season","All videos"}[Math.Clamp(value.GetInt32(),0,2)];
            return value.ValueKind switch{JsonValueKind.True=>"On",JsonValueKind.False=>"Off",JsonValueKind.String=>value.GetString() is {Length:>0} text?System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text):"Source default",_=>value.ToString()};
        }
    }
}
