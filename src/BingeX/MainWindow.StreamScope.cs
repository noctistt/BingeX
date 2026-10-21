namespace BingeX;
public partial class MainWindow
{
    private void SetupStreamScope()
    {
        SetupSourceFilters();
        IncludeTorrentSourcesCheck.IsChecked=preferences.IncludeTorrentSources;
        IncludeTorrentSourcesCheck.PropertyChanged+=(_,e)=>
        {
            if(e.Property.Name!=nameof(IncludeTorrentSourcesCheck.IsChecked))return;
            preferences=preferences with{IncludeTorrentSources=IncludeTorrentSourcesCheck.IsChecked==true};
            store.SavePreferences(preferences);
            if(!preferences.IncludeTorrentSources && pendingSource?.Torrent!=null)CancelSourceSwitch();
            if(availableSourceContext is {} context)RenderSourceChoices(availableSources,context);
        };
        SearchAllProvidersSwitch.IsChecked=preferences.SearchAllStreamProviders;
        SearchAllProvidersSwitch.PropertyChanged+=async(_,e)=>
        {
            if(e.Property.Name!=nameof(SearchAllProvidersSwitch.IsChecked))return;
            preferences=preferences with{SearchAllStreamProviders=SearchAllProvidersSwitch.IsChecked==true};
            store.SavePreferences(preferences);
            if(selectedContext?.Id.StartsWith("cs:")==true && (selectedContext.EpisodeId!=null || selectedTitle?.Episodes.Count==0)) await FindStreams();
        };
    }
}
