namespace BingeX;
public partial class MainWindow
{
    private void SetupStreamScope()
    {
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
