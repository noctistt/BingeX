using Avalonia.Controls;
using Avalonia.Interactivity;
namespace BingeX;
public partial class MainWindow
{
    private string sourceProviderFilter="",sourceResolutionFilter="",sourceLanguageFilter="";
    private bool updatingSourceFilters;
    private sealed record SourceFilterOption(string Key,string Label)
    {
        public override string ToString()=>Label;
    }
    private static string SourceResolution(StreamChoice source)=>source.Evidence?.Resolution?.ToString() ?? "unknown";
    private bool MatchesSourceFilters(StreamChoice source)=>
        (sourceProviderFilter.Length==0 || source.Provider==sourceProviderFilter) &&
        (sourceResolutionFilter.Length==0 || SourceResolution(source)==sourceResolutionFilter) &&
        (sourceLanguageFilter.Length==0 || (sourceLanguageFilter=="unknown" ? SourceAudioInfo.Parse(source).Languages.Count==0 : SourceAudioInfo.Parse(source).Languages.Contains(sourceLanguageFilter)));
    private void SetupSourceFilters()
    {
        SourceProviderPicker.SelectionChanged+=(_,_)=>ApplySourceFilters();
        SourceResolutionPicker.SelectionChanged+=(_,_)=>ApplySourceFilters();
        SourceLanguagePicker.SelectionChanged+=(_,_)=>ApplySourceFilters();
    }
    private void ApplySourceFilters()
    {
        if(updatingSourceFilters || availableSourceContext is not {} context)return;
        sourceProviderFilter=(SourceProviderPicker.SelectedItem as SourceFilterOption)?.Key ?? "";
        sourceResolutionFilter=(SourceResolutionPicker.SelectedItem as SourceFilterOption)?.Key ?? "";
        sourceLanguageFilter=(SourceLanguagePicker.SelectedItem as SourceFilterOption)?.Key ?? "";
        RenderSourceChoices(availableSources,context);
    }
    private void ResetSourceFilters_Click(object? sender,RoutedEventArgs e)
    {
        sourceProviderFilter=sourceResolutionFilter=sourceLanguageFilter=""; sourceAudioFilter="All";
        if(availableSourceContext is {} context)RenderSourceChoices(availableSources,context);
    }
    private void RenderSourceFilters()
    {
        var sources=EligibleSources.ToArray();
        updatingSourceFilters=true;
        try
        {
            Set(SourceProviderPicker,[new("","All providers"),..sources.Select(x=>x.Provider).Distinct().Order().Select(x=>new SourceFilterOption(x,x))],sourceProviderFilter);
            Set(SourceResolutionPicker,[new("","All resolutions"),..sources.Select(SourceResolution).Distinct().OrderByDescending(x=>int.TryParse(x,out var n)?n:0).Select(x=>new SourceFilterOption(x,x=="unknown"?"Unknown":x=="2160"?"4K / 2160p":x+"p"))],sourceResolutionFilter);
            Set(SourceLanguagePicker,[new("","All audio languages"),..sources.SelectMany(x=>SourceAudioInfo.Parse(x).Languages).Distinct().Order().Select(x=>new SourceFilterOption(x,System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x))),..(sources.Any(x=>SourceAudioInfo.Parse(x).Languages.Count==0)?new[]{new SourceFilterOption("unknown","Unknown")}:[])],sourceLanguageFilter);
        }
        finally {updatingSourceFilters=false;}
        var count=FilteredSources.Count();
        SourceFilterEmpty.IsVisible=sources.Length>0 && count==0;
        SourceFilterEmpty.Text="No sources match these filters. Reset filters to see all available sources.";
        ResetSourceFilters.IsEnabled=sourceProviderFilter.Length>0 || sourceResolutionFilter.Length>0 || sourceLanguageFilter.Length>0 || sourceAudioFilter!="All";
        void Set(ComboBox picker,SourceFilterOption[] options,string selected)
        {
            // Keep the same options and selection while late provider results arrive.
            var current=picker.ItemsSource as SourceFilterOption[];
            if(selected.Length>0 && !options.Any(x=>x.Key==selected))options=[..options,current?.FirstOrDefault(x=>x.Key==selected) ?? new SourceFilterOption(selected,selected)];
            if(current==null || !current.SequenceEqual(options))picker.ItemsSource=options;
            else options=current;
            picker.SelectedItem=options.FirstOrDefault(x=>x.Key==selected);
        }
    }
}
