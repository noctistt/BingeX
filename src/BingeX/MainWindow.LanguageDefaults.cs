namespace BingeX;
public partial class MainWindow
{
    private sealed record LanguageChoice(string Key,string Label){public override string ToString()=>Label;}
    private static readonly string[] defaultLanguages=["Arabic","Bengali","Chinese","Dutch","English","French","German","Hindi","Indonesian","Italian","Japanese","Kannada","Korean","Malayalam","Marathi","Portuguese","Punjabi","Russian","Spanish","Tamil","Telugu","Thai","Turkish","Ukrainian","Urdu","Vietnamese"];
    private void PopulateLanguageDefaults()
    {
        LanguageChoice[] languages=defaultLanguages.Select(x=>new LanguageChoice(x.ToLowerInvariant(),x)).ToArray();
        SettingsAudioLanguage.ItemsSource=new[]{new LanguageChoice("","Use source default")}.Concat(languages).ToArray();
        SettingsSubtitleLanguage.ItemsSource=new[]{new LanguageChoice("","Use source default"),new LanguageChoice("off","Off")}.Concat(languages).ToArray();
        SettingsAudioLanguage.SelectedItem=((LanguageChoice[])SettingsAudioLanguage.ItemsSource).FirstOrDefault(x=>x.Key==preferences.PreferredAudioLanguage) ?? ((LanguageChoice[])SettingsAudioLanguage.ItemsSource)[0];
        SettingsSubtitleLanguage.SelectedItem=((LanguageChoice[])SettingsSubtitleLanguage.ItemsSource).FirstOrDefault(x=>x.Key==preferences.PreferredSubtitleLanguage) ?? ((LanguageChoice[])SettingsSubtitleLanguage.ItemsSource)[0];
    }
    private string EffectiveAudioLanguage(MediaContext? context)
    {
        var saved=context==null ? "" : store.ShowAudioPreference(context).Language;
        return saved.Length>0 ? saved : preferences.PreferredAudioLanguage;
    }
    private SubtitlePreference? EffectiveSubtitlePreference()
    {
        var saved=currentContext==null ? null : store.SubtitlePreference(currentContext);
        if(saved!=null)return saved;
        return preferences.PreferredSubtitleLanguage switch {""=>null,"off"=>new("off"),var language=>new("preferred",language)};
    }
}
