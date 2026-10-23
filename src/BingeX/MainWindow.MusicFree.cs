using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
namespace BingeX;
public partial class MainWindow
{
    private MusicFreeProviderStore? musicFreeStore;
    private string? musicFreeCode,musicFreeCurrentUrl;
    private MusicFreeProvider? musicFreeProvider;
    private readonly List<MusicFreeTrack> musicFreeItems=[];
    private MusicFreeTrack[] musicFreeQueue=[];
    private int musicFreeIndex=-1,musicFreePage;
    private string musicFreeQuery="";
    private CancellationTokenSource musicFreeWork=new();
    private bool musicFreeWorking,musicFreeSelecting,musicFreeBrowsing;
    private IReadOnlyList<MusicFreeSaved> musicFreeProviders=[];
    private MusicFreeCatalogGroup[] musicFreeCatalogs=[];
    private string? musicFreeCatalogProvider,musicFreeQueueCode,musicFreeQueueProvider;

    private void SetupMusicFree()
    {
        musicFreeStore=new(store.DataDirectory);
        try{ReloadMusicFreeProviders();}catch{MusicFreeInstallMessage.Text="The saved providers could not be read. Add their original links again.";}
        UpdateMusicFreeProvider();
        MusicFreeTrust.IsCheckedChanged+=(_,_)=>{if(musicFreeSelecting)return;if(MusicFreeTrust.IsChecked!=true)CancelMusicFree();if(musicFreeProvider!=null&&musicFreeCode!=null){try{musicFreeProvider=musicFreeProvider with{Trusted=MusicFreeTrust.IsChecked==true};musicFreeStore.Save(musicFreeProvider,musicFreeCode);musicFreeProviders=musicFreeStore.ReadAll();UpdateQueueActions();}catch{MusicFreeInstallMessage.Text="Could not save the trust preference.";}}};
        Closed+=(_,_)=>musicFreeWork.Cancel();
    }
    private void UpdateMusicFreeProvider()
    {
        MusicFreeProviderName.Text=musicFreeProvider is {} p?$"{p.Name} · {p.Version}":"No music provider installed";
        MusicFreeTitle.Text=musicFreeProvider?.Name??"Online music";
        musicFreeSelecting=true;
        MusicFreeProviderPicker.ItemsSource=musicFreeProviders.Select(x=>x.Provider.Name).ToArray();
        MusicFreeProviderPicker.SelectedItem=musicFreeProvider?.Name;
        MusicFreeTrust.IsChecked=musicFreeProvider?.Trusted==true;
        musicFreeSelecting=false;
        MusicFreeRemove.IsVisible=musicFreeProvider!=null;UpdateQueueActions();
    }
    private void CancelMusicFree()
    {
        musicFreeWork.Cancel();musicFreeWork.Dispose();musicFreeWork=new();musicFreeWorking=false;
        MusicFreeBusy.IsVisible=false;MusicFreeSearchButton.IsEnabled=MusicFreeInstallButton.IsEnabled=true;UpdateQueueActions();
    }
    private CancellationToken StartMusicFree()
    {
        CancelMusicFree();musicFreeWorking=true;MusicFreeBusy.IsVisible=true;MusicFreeSearchButton.IsEnabled=MusicFreeInstallButton.IsEnabled=false;return musicFreeWork.Token;
    }
    private bool MusicFreeReady()
    {
        if(!onlineEnabled)return false;
        if(MusicFreeTrust.IsChecked==true&&musicFreeCode!=null)return true;
        MusicFreeMessage.Text=musicFreeCode==null?"Add a compatible provider in Add-ons → Music to search.":"Confirm trust in Add-ons → Music, then search again.";
        return false;
    }
    private void ManageMusicFree_Click(object? sender,RoutedEventArgs e){Page("Add-ons");SelectAddonCategory(true);}
    private async void InstallMusicFree_Click(object? sender,RoutedEventArgs e)=>await InstallMusicFree(MusicFreeLink.Text??"");
    private async Task InstallMusicFree(string link)
    {
        if(!onlineEnabled||musicFreeWorking)return;
        if(MusicFreeTrust.IsChecked!=true){MusicFreeInstallMessage.Text="Confirm that you trust the provider before adding it.";return;}
        var token=StartMusicFree();MusicFreeInstallMessage.Text="Checking provider…";
        try{
            string source;MusicFreeProvider provider;
            if(MusicFreeHttp.PublicUri(link.Trim()).AbsolutePath.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)){
                MusicFreeInstallMessage.Text="Preparing Nuclear provider…";
                var package=await Task.Run(()=>NuclearMusicImport.Read(MusicFreeHttp.DownloadArchive(link.Trim(),token)),token);
                var imported=await MusicFreeWorker.Call<NuclearMusicImported>(System.Text.Json.JsonSerializer.Serialize(package),"compile-nuclear",new{},token);source=imported.Source;provider=imported.Provider;
            }else{source=await Task.Run(()=>MusicFreeHttp.Download(link.Trim(),token),token);provider=await MusicFreeWorker.Call<MusicFreeProvider>(source,"inspect",new{},token);}
            token.ThrowIfCancellationRequested();
            if(localClosing||!onlineEnabled)return;
            provider=provider with{Trusted=true};musicFreeStore!.Save(provider,source);ReloadMusicFreeProviders();ClearMusicFreeBrowse();UpdateMusicFreeProvider();
            MusicFreeLink.Text="";MusicFreeInstallMessage.Text="Provider added. Open Music to explore or search.";MusicFreeMessage.Text="Search for a song or artist to begin.";
        }catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested&&!localClosing)MusicFreeInstallMessage.Text="Could not add this provider. Use a compatible MusicFree .js or Nuclear plugin.zip HTTPS link. Nuclear support is experimental; unsupported dependencies or package layouts cannot be imported. Your previous providers are kept.";}
        finally{if(!token.IsCancellationRequested&&!localClosing){musicFreeWorking=false;MusicFreeBusy.IsVisible=false;MusicFreeSearchButton.IsEnabled=MusicFreeInstallButton.IsEnabled=true;}}
    }
    private void RemoveMusicFree_Click(object? sender,RoutedEventArgs e)
    {
        CancelMusicFree();try{
            var name=musicFreeProvider?.Name;musicFreeStore!.Remove(name);
            if(name==musicFreeQueueProvider){musicFreeQueue=[];musicFreeIndex=-1;musicFreeQueueCode=null;}
            ReloadMusicFreeProviders();ClearMusicFreeBrowse();UpdateMusicFreeProvider();MusicFreeInstallMessage.Text="Provider removed. Current playback can continue.";
        }catch{MusicFreeInstallMessage.Text="Could not remove the provider. Try again.";}
    }
    private async void SearchMusicFree_Click(object? sender,RoutedEventArgs e)=>await SearchMusicFree(false);
    private async void MoreMusicFree_Click(object? sender,RoutedEventArgs e){if(!musicFreeBrowsing)await SearchMusicFree(true);}
    private async Task SearchMusicFree(bool more)
    {
        if(!MusicFreeReady())return;
        var query=more?musicFreeQuery:MusicFreeSearch.Text?.Trim()??"";if(query.Length==0){MusicFreeMessage.Text="Enter a song or artist.";return;}
        if(more&&musicFreeWorking)return;
        var token=StartMusicFree();var page=more?musicFreePage+1:1;var code=musicFreeCode!;
        if(!more){musicFreeItems.Clear();MusicFreeResultsPanel.Children.Clear();MusicFreeMore.IsVisible=false;}
        musicFreeBrowsing=false;MusicFreeExploreButton.Classes.Set("primary",false);MusicFreeMessage.Text="Searching…";
        try{
            var result=await MusicFreeWorker.Call<MusicFreeResults>(code,"search",new{query,page},token);token.ThrowIfCancellationRequested();if(localClosing||code!=musicFreeCode)return;
            foreach(var item in result.Items.Where(x=>x.Id.Length>0&&x.Title.Length>0))if(!musicFreeItems.Any(x=>x.Id==item.Id))musicFreeItems.Add(item);
            musicFreePage=page;musicFreeQuery=query;RenderMusicFree();MusicFreeMore.IsVisible=!result.IsEnd&&musicFreeItems.Count<300;
            MusicFreeMessage.Text=musicFreeItems.Count>0?$"{musicFreeItems.Count} tracks · select one to play. Quality depends on the provider.":"No tracks found. Try another search.";
        }catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested&&!localClosing)MusicFreeMessage.Text="This provider could not complete the search. Try again; the service may be unavailable or incompatible.";}
        finally{if(!token.IsCancellationRequested&&!localClosing){musicFreeWorking=false;MusicFreeBusy.IsVisible=false;MusicFreeSearchButton.IsEnabled=MusicFreeInstallButton.IsEnabled=true;}}
    }
    private void RenderMusicFree()
    {
        MusicFreeResultsPanel.Children.Clear();
        foreach(var item in musicFreeItems){
            var content=new StackPanel{Width=156,Spacing=6};var image=new Image{Width=156,Height=156,Stretch=Stretch.UniformToFill};
            var art=new Grid{Width=156,Height=156,Background=Brush.Parse("#302742")};art.Children.Add(new TextBlock{Text="♫",FontSize=42,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});art.Children.Add(image);
            if(item.Artwork is {} url&&Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https")_=artwork.Fill(image,url);
            content.Children.Add(new Border{Child=art,CornerRadius=new(8),ClipToBounds=true});
            content.Children.Add(new TextBlock{Text=item.Title,MaxLines=2,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeight.SemiBold});content.Children.Add(new TextBlock{Text=item.Artist,MaxLines=1,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12});
            content.Children.Add(new TextBlock{Text=Clock((long)(Math.Clamp(item.Duration,0,86400)*1000)),FontSize=11});
            var button=new Button{Content=content,Padding=new(8),Margin=new(0,0,8,8),VerticalAlignment=VerticalAlignment.Top};
            button.Click+=async(_,_)=>{if(!musicFreeWorking&&MusicFreeReady()&&!NativePlaybackBusy){musicFreeQueue=musicFreeItems.ToArray();musicFreeQueueCode=musicFreeCode;musicFreeQueueProvider=musicFreeProvider?.Name;await PlayMusicFree(Array.FindIndex(musicFreeQueue,x=>x.Id==item.Id));}};MusicFreeResultsPanel.Children.Add(button);
        }
    }
    private async Task PlayMusicFree(int index)
    {
        if(!onlineEnabled||!MusicFreeQueueReady||NativePlaybackBusy||index<0||index>=musicFreeQueue.Length)return;
        var queue=musicFreeQueue;var track=queue[index];var code=musicFreeQueueCode!;var request=playbackRequest;var token=StartMusicFree();MusicFreeMessage.Text="Opening "+track.Title+"…";
        try{
            var media=await MusicFreeWorker.Call<MusicFreeSource>(code,"resolve",new{item=track.Item},token);token.ThrowIfCancellationRequested();if(localClosing||code!=musicFreeQueueCode||!ReferenceEquals(queue,musicFreeQueue)||request!=playbackRequest)return;
            MusicFreeHttp.PublicUri(media.Url);if(media.Headers?.Count>0)throw new NotSupportedException();
            musicFreeIndex=index;musicFreeCurrentUrl=media.Url;Play(media.Url,track.Artist+" — "+track.Title,skipHistory:true,musicPlayback:true);MusicFreeMessage.Text="Playing · "+track.Title;
        }catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested&&!localClosing)MusicFreeMessage.Text="This track is unavailable or needs unsupported features. Try another track or retry.";}
        finally{if(!token.IsCancellationRequested&&!localClosing){musicFreeWorking=false;MusicFreeBusy.IsVisible=false;MusicFreeSearchButton.IsEnabled=MusicFreeInstallButton.IsEnabled=true;UpdateQueueActions();}}
    }
    private bool MusicFreeQueueReady=>musicFreeProviders.Any(x=>x.Provider.Trusted&&x.Provider.Name==musicFreeQueueProvider&&x.Source==musicFreeQueueCode);
    private bool IsMusicFreePlaying=>musicFreeCurrentUrl!=null&&currentPath==musicFreeCurrentUrl;
    private bool MoveMusicFree(int direction)
    {
        var next=musicFreeIndex+direction;if(!onlineEnabled||!MusicFreeQueueReady||musicFreeWorking||NativePlaybackBusy||next<0||next>=musicFreeQueue.Length)return false;_=PlayMusicFree(next);return true;
    }
}
