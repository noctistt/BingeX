using Avalonia.Controls;
using Avalonia.Interactivity;
namespace BingeX;
public partial class MainWindow
{
    private void ReloadMusicFreeProviders()
    {
        musicFreeProviders=musicFreeStore!.ReadAll();
        var selected=musicFreeStore.Read();musicFreeProvider=selected?.Provider;musicFreeCode=selected?.Source;
    }
    private void ClearMusicFreeBrowse()
    {
        musicFreeItems.Clear();MusicFreeResultsPanel.Children.Clear();MusicFreeMore.IsVisible=false;MusicFreeSearch.Text="";musicFreePage=0;musicFreeQuery="";
        musicFreeCatalogProvider=null;musicFreeCatalogs=[];musicFreeSelecting=true;MusicFreeSection.ItemsSource=null;MusicFreeGenre.ItemsSource=null;musicFreeSelecting=false;MusicFreeBrowseFilters.IsVisible=false;
        MusicFreeExploreButton.IsVisible=true;MusicFreeExploreButton.Classes.Set("primary",true);MusicFreeMessage.Text="Explore this provider or search for a song or artist.";
    }
    private async void MusicFreeProviderChanged(object? sender,SelectionChangedEventArgs e)
    {
        if(musicFreeSelecting||MusicFreeProviderPicker.SelectedItem is not string name||name==musicFreeProvider?.Name)return;
        await SelectMusicFreeProvider(name);
    }
    private async Task SelectMusicFreeProvider(string name)
    {
        CancelMusicFree();try{
            musicFreeStore!.Select(name);ReloadMusicFreeProviders();ClearMusicFreeBrowse();UpdateMusicFreeProvider();
            if(onlineEnabled&&MusicPage.IsVisible)await EnsureMusicFreeExplore();
        }catch{MusicFreeMessage.Text="Could not switch provider. Try again.";}
    }
    private async void ExploreMusicFree_Click(object? sender,RoutedEventArgs e)
    {
        if(!MusicFreeReady())return;
        if(musicFreeCatalogProvider!=musicFreeCode)await EnsureMusicFreeExplore();else await BrowseMusicFree();
    }
    private async Task EnsureMusicFreeExplore()
    {
        if(musicFreeWorking||!MusicFreeReady()||musicFreeCatalogProvider==musicFreeCode)return;
        var token=StartMusicFree();var code=musicFreeCode!;MusicFreeMessage.Text="Loading explore options…";
        var browse=false;
        try{
            var groups=await MusicFreeWorker.Call<MusicFreeCatalogGroup[]>(code,"catalogs",new{},token);token.ThrowIfCancellationRequested();if(localClosing||code!=musicFreeCode)return;
            musicFreeCatalogs=groups.Where(x=>x.Title.Length>0&&x.Catalogs.Length>0).ToArray();musicFreeCatalogProvider=code;
            musicFreeSelecting=true;MusicFreeSection.ItemsSource=musicFreeCatalogs.Select(x=>x.Title).ToArray();MusicFreeSection.SelectedIndex=musicFreeCatalogs.Length>0?0:-1;UpdateMusicFreeGenres();musicFreeSelecting=false;
            MusicFreeBrowseFilters.IsVisible=musicFreeCatalogs.Length>0;MusicFreeExploreButton.IsVisible=musicFreeCatalogs.Length>0;
            browse=musicFreeCatalogs.Length>0;
            if(!browse)MusicFreeMessage.Text="This provider offers search only. Search for a song or artist above.";
        }catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested&&!localClosing)MusicFreeMessage.Text="Explore is unavailable for this provider. Retry Explore, or use Search.";}
        finally{musicFreeSelecting=false;if(!token.IsCancellationRequested&&!localClosing){musicFreeWorking=false;MusicFreeBusy.IsVisible=false;MusicFreeSearchButton.IsEnabled=MusicFreeInstallButton.IsEnabled=true;}}
        if(browse&&!token.IsCancellationRequested)await BrowseMusicFree();
    }
    private void UpdateMusicFreeGenres()
    {
        var index=MusicFreeSection.SelectedIndex;
        MusicFreeGenre.ItemsSource=index>=0&&index<musicFreeCatalogs.Length?musicFreeCatalogs[index].Catalogs.Select(x=>x.Title).ToArray():[];
        MusicFreeGenre.SelectedIndex=index>=0&&index<musicFreeCatalogs.Length?0:-1;
    }
    private async void MusicFreeSectionChanged(object? sender,SelectionChangedEventArgs e)
    {
        if(musicFreeSelecting)return;musicFreeSelecting=true;UpdateMusicFreeGenres();musicFreeSelecting=false;await BrowseMusicFree();
    }
    private async void MusicFreeGenreChanged(object? sender,SelectionChangedEventArgs e){if(!musicFreeSelecting)await BrowseMusicFree();}
    private async Task BrowseMusicFree()
    {
        if(!MusicFreeReady())return;
        var section=MusicFreeSection.SelectedIndex;var genre=MusicFreeGenre.SelectedIndex;
        if(section<0||section>=musicFreeCatalogs.Length||genre<0||genre>=musicFreeCatalogs[section].Catalogs.Length)return;
        var item=musicFreeCatalogs[section].Catalogs[genre];var title=musicFreeCatalogs[section].Title+" · "+item.Title;
        var token=StartMusicFree();var code=musicFreeCode!;musicFreeBrowsing=true;MusicFreeExploreButton.Classes.Set("primary",true);
        musicFreeItems.Clear();MusicFreeResultsPanel.Children.Clear();MusicFreeMore.IsVisible=false;MusicFreeMessage.Text="Loading "+title+"…";
        try{
            var results=await MusicFreeWorker.Call<MusicFreeResults>(code,"browse",new{item=item.Item,page=1},token);token.ThrowIfCancellationRequested();if(localClosing||code!=musicFreeCode)return;
            foreach(var track in results.Items.Where(x=>x.Id.Length>0&&x.Title.Length>0))if(!musicFreeItems.Any(x=>x.Id==track.Id))musicFreeItems.Add(track);
            RenderMusicFree();MusicFreeMessage.Text=musicFreeItems.Count>0?$"{title} · {musicFreeItems.Count} tracks":$"No tracks available in {title}. Try another genre.";
        }catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested&&!localClosing)MusicFreeMessage.Text="This catalogue could not load. Choose another genre, retry Explore, or use Search.";}
        finally{if(!token.IsCancellationRequested&&!localClosing){musicFreeWorking=false;MusicFreeBusy.IsVisible=false;MusicFreeSearchButton.IsEnabled=MusicFreeInstallButton.IsEnabled=true;}}
    }
}
