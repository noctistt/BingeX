using System.Globalization;
using System.Text.RegularExpressions;
namespace BingeX;
public record SubtitlePreference(string Mode,string Language="",string Label="",string Provider="")
{
    public static string LanguageKey(string code) => OnlineSubtitles.LanguageName(code.Trim().Replace('_','-').Split('-')[0]).Trim().ToLowerInvariant();
    public static string EmbeddedLanguage(string label)
    {
        foreach(var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures).Where(x=>x.Name.Length>0).OrderByDescending(x=>x.EnglishName.Length))
            if(label.Contains(culture.EnglishName,StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(label,@"\b"+Regex.Escape(culture.ThreeLetterISOLanguageName)+@"\b",RegexOptions.IgnoreCase))
                return LanguageKey(culture.TwoLetterISOLanguageName);
        return "";
    }
    public bool Matches(SubtitleChoice choice) => Language.Length>0 && LanguageKey(choice.Language)==Language;
}
