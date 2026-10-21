using Avalonia.Controls;
using Avalonia.Media;
namespace BingeX;
public partial class MainWindow
{
    private string sourceAudioFilter="All",sourceAudioContext="";
    private IEnumerable<StreamChoice> FilteredSources => availableSources.Where(x=>sourceAudioFilter=="All" || SourceAudioInfo.Parse(x).Kind.ToString()==sourceAudioFilter);
    private IEnumerable<StreamChoice> PlaybackSources
    {
        get
        {
            var language=availableSourceContext==null ? "" : store.ShowAudioPreference(availableSourceContext).Language;
            return FilteredSources.Where(x=>language.Length==0 || SourceAudioInfo.Parse(x).Languages.Count==0 || SourceAudioInfo.Parse(x).Languages.Contains(language))
                .OrderByDescending(x=>language.Length>0 && SourceAudioInfo.Parse(x).Languages.Contains(language));
        }
    }
    private void SetSourceAudioContext(MediaContext context)
    {
        var key=context.Type+":"+context.Id;
        if(sourceAudioContext==key)return;
        sourceAudioContext=key;
        var saved=store.ShowAudioPreference(context).SourceFilter;
        sourceAudioFilter=saved=="All" || Enum.TryParse<SourceAudioKind>(saved,out _) ? saved : "All";
    }
    private void RenderSourceAudioFilters(MediaContext context)
    {
        SourceAudioFilterPanel.IsVisible=availableSources.Count>0;
        SourceAudioFilters.Children.Clear();
        foreach(var filter in new[]{"All","Subbed","Dubbed","DualAudio","Unknown"})
        {
            var count=filter=="All" ? availableSources.Count : availableSources.Count(x=>SourceAudioInfo.Parse(x).Kind.ToString()==filter);
            var button=new Button{Content=(filter=="DualAudio" ? "Dual audio" : filter)+$" ({count})",Margin=new Avalonia.Thickness(0,0,8,6),Padding=new Avalonia.Thickness(12,7)};
            button.Classes.Set("primary",sourceAudioFilter==filter);
            button.Click+=(_,_)=>
            {
                sourceAudioFilter=filter;
                var saved=store.ShowAudioPreference(context);
                store.SaveShowAudioPreference(context,saved with{SourceFilter=filter});
                RenderSourceChoices(availableSources,context);
            };
            SourceAudioFilters.Children.Add(button);
        }
        var visible=FilteredSources.Count();
        SourceAudioFilterNotice.Text=visible>0 && !PlaybackSources.Any() ? "These sources do not match your saved audio language. Pick a source manually to change language." : sourceAudioFilter=="All" ? "Labels come from the provider. Unlabelled audio stays Unknown." : $"{visible} of {availableSources.Count} sources match. Choose All to see other sources.";
    }
    private void RememberSourceAudioChoice(StreamChoice source,MediaContext context)
    {
        var info=SourceAudioInfo.Parse(source);
        if(info.Languages.Count!=1)return;
        var language=info.Languages[0];
        store.SaveShowAudioPreference(context,store.ShowAudioPreference(context) with{Language=language,Label=System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(language)});
    }
    private void RememberShowAudio(string language,string label)
    {
        if(currentContext==null)return;
        var key=language.Length>0 ? SubtitlePreference.LanguageKey(language) : SubtitlePreference.EmbeddedLanguage(label);
        if(key is "und" or "unknown")key=SubtitlePreference.EmbeddedLanguage(label);
        store.SaveShowAudioPreference(currentContext,store.ShowAudioPreference(currentContext) with{Language=key,Label=label});
    }
}
