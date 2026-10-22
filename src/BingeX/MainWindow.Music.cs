using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
namespace BingeX;
public partial class MainWindow
{
    private static bool IsMusicHistory(RecentMedia media,MediaContext? context)
    {
        if(context?.Type is "music" or "audio")return true;
        return context==null && Uri.TryCreate(media.Path,UriKind.Absolute,out var uri) && uri.IsFile && MusicLibrary.IsAudio(uri.LocalPath);
    }
    private void SelectAddonCategory(bool music)
    {
        MusicAddonsSection.IsVisible=music;VideoAddonsSection.IsVisible=!music;
        MusicAddonsFilter.Classes.Set("primary",music);VideoAddonsFilter.Classes.Set("primary",!music);
    }
    private void VideoAddonsFilter_Click(object? sender,RoutedEventArgs e)=>SelectAddonCategory(false);
    private void MusicAddonsFilter_Click(object? sender,RoutedEventArgs e)=>SelectAddonCategory(true);
    private MusicLibrary? musicLibrary;
    private readonly Avalonia.Threading.DispatcherTimer musicSearchTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private CancellationTokenSource musicScanWork=new(),musicServerWork=new();
    private bool musicScanning;
    private string? musicAlbum;
    private int musicLimit=60,serverAlbumOffset;
    private string serverAlbumQuery="";
    private SubsonicMusic? musicServer;
    private readonly List<MusicAlbum> serverAlbums=[];
    private IReadOnlyList<ServerMusicTrack> serverTracks=[];
    private int serverTrackIndex=-1;
    private string? musicCurrentUrl;
    private void SetupMusic()
    {
        musicSearchTimer.Tick+=(_,_)=>{musicSearchTimer.Stop();RenderMusic();};
        musicLibrary=new(store.DataDirectory);MusicAutoplayCheck.IsChecked=preferences.AutoplayLocalQueue;
        MusicAutoplayCheck.IsCheckedChanged+=(_,_)=>QueueAutoplayCheck.IsChecked=MusicAutoplayCheck.IsChecked;
        QueueAutoplayCheck.IsCheckedChanged+=(_,_)=>MusicAutoplayCheck.IsChecked=QueueAutoplayCheck.IsChecked;
        Closed+=(_,_)=>{musicSearchTimer.Stop();musicScanWork.Cancel();musicServerWork.Cancel();musicServer?.Dispose();};
        MusicMessage.Text=musicLibrary.Tracks.Length>0?$"{musicLibrary.Tracks.Length} tracks indexed. Add folders or rescan to update your collection.":"Add a music folder to get started.";
        RenderMusic();ApplyMusicOnlineMode();
    }
    private void LibraryMusic_Click(object? sender,RoutedEventArgs e)=>Page("Music");
    private void ManageMusicConnections_Click(object? sender,RoutedEventArgs e)
    {
        if(!onlineEnabled)return;Page("Add-ons");SelectAddonCategory(true);MusicServerPanel.IsExpanded=true;
        Avalonia.Threading.Dispatcher.UIThread.Post(()=>MusicServerPanel.BringIntoView(),Avalonia.Threading.DispatcherPriority.Loaded);
    }
    private static string AlbumKey(MusicTrack x)=>x.Folder+"|"+x.AlbumArtist+"|"+x.Album;
    private MusicTrack[] AlbumTracks()=>musicLibrary?.Tracks.Where(x=>AlbumKey(x)==musicAlbum).OrderBy(x=>x.Disc).ThenBy(x=>x.Track).ThenBy(x=>x.Title,StringComparer.OrdinalIgnoreCase).ToArray()??[];
    private void RenderMusic()
    {
        if(musicLibrary==null)return;
        MusicFolderItems.Children.Clear();
        foreach(var folder in musicLibrary.Folders)
        {
            var button=new Button{Content="Remove "+FolderName(folder),Margin=new Thickness(0,0,8,6),IsEnabled=!musicScanning};
            ToolTip.SetTip(button,"Remove from the music library only: "+folder);button.Click+=(_,_)=>{try{musicLibrary.Remove(folder);musicAlbum=null;RenderMusic();MusicMessage.Text="Music folder removed. Files were kept.";}catch{MusicMessage.Text="Could not save the music library.";}};MusicFolderItems.Children.Add(button);
        }
        MusicAlbums.Children.Clear();MusicTracks.Children.Clear();
        MusicBackAlbums.IsVisible=MusicPlayAlbum.IsVisible=musicAlbum!=null;
        if(musicAlbum!=null)
        {
            var tracks=AlbumTracks();MusicAlbumTitle.Text=tracks.FirstOrDefault() is {} first?first.Album+" · "+first.AlbumArtist:"Album no longer in library";
            foreach(var track in tracks.Take(musicLimit))
            {
                var row=new Grid{ColumnDefinitions=new("*,Auto")};
                var play=TrackButton(track.Track,track.Title,track.Artist,track.Seconds,MusicLibrary.Quality(track));
                ToolTip.SetTip(play,track.Path);play.Click+=(_,_)=>AddQueueFiles([track.Path],true);row.Children.Add(play);
                var add=new Button{Content="+ Queue",Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(add,1);add.Click+=(_,_)=>{AddQueueFiles([track.Path],false);MusicMessage.Text="Track added to the playback queue.";};row.Children.Add(add);MusicTracks.Children.Add(row);
            }
            MoreMusicButton.IsVisible=tracks.Length>musicLimit;return;
        }
        var query=MusicSearch.Text?.Trim()??"";
        var albums=musicLibrary.Tracks.Where(x=>(x.Title+" "+x.Artist+" "+x.Album+" "+x.AlbumArtist).Contains(query,StringComparison.OrdinalIgnoreCase)).GroupBy(AlbumKey).OrderBy(x=>x.First().AlbumArtist,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.First().Album,StringComparer.OrdinalIgnoreCase).ToArray();
        MusicAlbumTitle.Text=$"{musicLibrary.Tracks.Length} tracks · {albums.Length} matching albums";
        foreach(var album in albums.Take(musicLimit))
        {
            var first=album.First();var content=new StackPanel{Width=170,Spacing=8};
            var art=new Grid{Width=170,Height=170,Background=Brush.Parse("#302742")};art.Children.Add(new TextBlock{Text="♫",FontSize=58,Foreground=accentLightBrush,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
            if(album.FirstOrDefault(x=>x.Cover!=null)?.Cover is {} cover){var image=new Image{Stretch=Stretch.UniformToFill};art.Children.Add(image);_ = artwork.Fill(image,cover);}
            content.Children.Add(new Border{Child=art,CornerRadius=new CornerRadius(10),ClipToBounds=true});
            content.Children.Add(new TextBlock{Text=first.Album,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap,MaxLines=2});
            content.Children.Add(new TextBlock{Text=first.AlbumArtist,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12});
            content.Children.Add(new TextBlock{Text=album.Count()+" tracks",FontSize=11,Foreground=Brush.Parse("#AAA0BA")});
            var button=new Button{Content=content,Padding=new Thickness(8),Margin=new Thickness(0,0,10,10),VerticalAlignment=VerticalAlignment.Top};button.Click+=(_,_)=>{musicAlbum=album.Key;musicLimit=200;RenderMusic();};MusicAlbums.Children.Add(button);
        }
        MoreMusicButton.IsVisible=albums.Length>musicLimit;
    }
    private static Button TrackButton(int number,string title,string artist,double seconds,string quality)
    {
        var text=new StackPanel{Spacing=5};text.Children.Add(new TextBlock{Text=(number>0?number+". ":"")+title,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        text.Children.Add(new TextBlock{Text=artist+" · "+TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"m\:ss")+" · "+quality,FontSize=11,Foreground=Brush.Parse("#AAA0BA"),TextWrapping=TextWrapping.Wrap});
        return new Button{Content=text,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(12)};
    }
    private void MusicSearchChanged(object? sender,TextChangedEventArgs e){if(musicLibrary==null)return;musicAlbum=null;musicLimit=60;musicSearchTimer.Stop();musicSearchTimer.Start();}
    private void MusicBackAlbums_Click(object? sender,RoutedEventArgs e){musicAlbum=null;musicLimit=60;RenderMusic();}
    private void MoreMusic_Click(object? sender,RoutedEventArgs e){musicLimit+=100;RenderMusic();}
    private void MusicPlayAlbum_Click(object? sender,RoutedEventArgs e)
    {
        var tracks=AlbumTracks();if(tracks.Length==0)return;localQueue.Clear();AddQueueFiles(tracks.Select(x=>x.Path),true);
    }
    private async void AddMusicFolder_Click(object? sender,RoutedEventArgs e)
    {
        if(musicScanning)return;var folders=await StorageProvider.OpenFolderPickerAsync(new(){Title="Add music folder",AllowMultiple=false});
        if(!localClosing && folders.FirstOrDefault()?.TryGetLocalPath() is {} folder)await ScanMusic([folder]);
    }
    private async void RescanMusic_Click(object? sender,RoutedEventArgs e){if(musicLibrary!=null)await ScanMusic(musicLibrary.Folders);}
    private void CancelMusicScan_Click(object? sender,RoutedEventArgs e)=>musicScanWork.Cancel();
    private async Task ScanMusic(IEnumerable<string> folders)
    {
        if(musicScanning||musicLibrary==null)return;musicScanWork.Cancel();musicScanWork.Dispose();musicScanWork=new();var token=musicScanWork.Token;
        musicScanning=true;MusicBusy.IsVisible=CancelMusicScanButton.IsVisible=true;AddMusicFolderButton.IsEnabled=RescanMusicButton.IsEnabled=false;RenderMusic();
        try
        {
            var skipped=0;
            foreach(var folder in folders){MusicMessage.Text="Reading music in "+FolderName(folder)+"…";var scan=await Task.Run(()=>musicLibrary.Scan(folder,token),token);token.ThrowIfCancellationRequested();if(localClosing)return;musicLibrary.Apply(scan);skipped+=scan.Skipped;}
            MusicMessage.Text=$"{musicLibrary.Tracks.Length} tracks indexed."+(skipped>0?$" {skipped} entries could not be read; previously indexed tracks were kept.":" Files were not changed.");
        }
        catch(OperationCanceledException){if(!localClosing)MusicMessage.Text="Music scan cancelled. Your saved library was kept.";}
        catch{if(!localClosing)MusicMessage.Text="Could not finish the music scan. Check folder access and rescan.";}
        finally{musicScanning=false;if(!localClosing){MusicBusy.IsVisible=CancelMusicScanButton.IsVisible=false;AddMusicFolderButton.IsEnabled=RescanMusicButton.IsEnabled=true;RenderMusic();}}
    }
    private void ApplyMusicOnlineMode()
    {
        MusicServerPanel.IsVisible=onlineEnabled;MusicManageConnections.IsVisible=onlineEnabled;MusicOnlineHint.IsVisible=!onlineEnabled;
        if(!onlineEnabled)DisconnectMusicServer();
    }
    private CancellationToken BeginMusicServerRequest()
    {
        musicServerWork.Cancel();musicServerWork.Dispose();musicServerWork=new();MusicServerBusy.IsVisible=true;return musicServerWork.Token;
    }
    private void DisconnectMusicServer()
    {
        musicServerWork.Cancel();musicServer?.Dispose();musicServer=null;serverAlbums.Clear();serverTracks=[];serverTrackIndex=-1;
        MusicServerPassword.Text="";MusicServerBrowser.IsVisible=MusicDisconnectButton.IsVisible=MusicServerBusy.IsVisible=false;
        MusicConnectButton.IsEnabled=true;ServerAlbumItems.Children.Clear();ServerTrackItems.Children.Clear();ServerAlbumTitle.Text="";MusicServerMessage.Text="Browser disconnected. Current playback may continue until stopped or changed.";
        UpdateQueueActions();
    }
    private void DisconnectMusicServer_Click(object? sender,RoutedEventArgs e)=>DisconnectMusicServer();
    private async void ConnectMusicServer_Click(object? sender,RoutedEventArgs e)
    {
        if(!onlineEnabled)return;var password=MusicServerPassword.Text??"";DisconnectMusicServer();var token=BeginMusicServerRequest();SubsonicMusic? candidate=null;MusicConnectButton.IsEnabled=false;
        try
        {
            candidate=new(MusicServerAddress.Text??"",MusicServerUser.Text??"",password,MusicAllowHttp.IsChecked==true);
            var albums=await candidate.Albums("",0,token);token.ThrowIfCancellationRequested();if(localClosing)return;
            musicServer=candidate;candidate=null;serverAlbums.AddRange(albums);serverAlbumOffset=albums.Count;serverAlbumQuery="";MusicServerSearch.Text="";
            MusicServerBrowser.IsVisible=MusicDisconnectButton.IsVisible=true;RenderServerAlbums();MoreServerAlbums.IsVisible=albums.Count==100;MusicServerMessage.Text="Connected. Credentials are kept only for this session.";
        }
        catch(OperationCanceledException){}
        catch(ArgumentException error){if(!token.IsCancellationRequested)MusicServerMessage.Text=error.Message;}
        catch{if(!token.IsCancellationRequested)MusicServerMessage.Text="Could not connect. Check the server address, username, password and permissions.";}
        finally{candidate?.Dispose();if(!token.IsCancellationRequested&&!localClosing){MusicServerBusy.IsVisible=false;MusicConnectButton.IsEnabled=true;}}
    }
    private void RenderServerAlbums()
    {
        ServerAlbumItems.Children.Clear();ServerTrackItems.Children.Clear();ServerAlbumItems.IsVisible=true;ServerBackAlbums.IsVisible=false;ServerAlbumTitle.Text="Server albums";
        foreach(var album in serverAlbums)
        {
            var text=new StackPanel{Width=180,Spacing=6};text.Children.Add(new TextBlock{Text="♫ "+album.Name,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});text.Children.Add(new TextBlock{Text=album.Artist,FontSize=12,TextWrapping=TextWrapping.Wrap});
            var button=new Button{Content=text,Margin=new Thickness(0,0,10,10),Padding=new Thickness(14),VerticalAlignment=VerticalAlignment.Top};var owner=musicServer;button.Click+=async(_,_)=>{if(owner!=null&&ReferenceEquals(owner,musicServer))await LoadServerTracks(owner,album);};ServerAlbumItems.Children.Add(button);
        }
    }
    private async Task LoadServerAlbums(bool more)
    {
        if(!onlineEnabled||musicServer==null)return;var owner=musicServer;var token=BeginMusicServerRequest();
        try
        {
            var query=more?serverAlbumQuery:MusicServerSearch.Text??"";
            var albums=await owner.Albums(query,more?serverAlbumOffset:0,token);token.ThrowIfCancellationRequested();if(localClosing)return;
            if(!more){serverAlbums.Clear();serverAlbumQuery=query;}serverAlbums.AddRange(albums);serverAlbumOffset=serverAlbums.Count;RenderServerAlbums();MoreServerAlbums.IsVisible=albums.Count==100;MusicServerMessage.Text=serverAlbums.Count+" albums loaded.";
        }
        catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested)MusicServerMessage.Text="Could not load albums. Check the server and try again.";}
        finally{if(!token.IsCancellationRequested&&!localClosing)MusicServerBusy.IsVisible=false;}
    }
    private async void SearchMusicServer_Click(object? sender,RoutedEventArgs e)=>await LoadServerAlbums(false);
    private async void MoreServerAlbums_Click(object? sender,RoutedEventArgs e)=>await LoadServerAlbums(true);
    private void ServerBackAlbums_Click(object? sender,RoutedEventArgs e)=>RenderServerAlbums();
    private async Task LoadServerTracks(SubsonicMusic owner,MusicAlbum album)
    {
        var token=BeginMusicServerRequest();ServerTrackItems.Children.Clear();ServerAlbumItems.IsVisible=false;ServerBackAlbums.IsVisible=true;MoreServerAlbums.IsVisible=false;ServerAlbumTitle.Text=album.Name+" · "+album.Artist;
        try
        {
            var tracks=await owner.Tracks(album.Id,token);token.ThrowIfCancellationRequested();if(localClosing)return;
            foreach(var track in tracks.Take(1000))
            {
                var quality=track.Format.ToUpperInvariant()+(track.SampleRate>0?$" · {track.SampleRate/1000d:0.###} kHz":"")+(track.Bits>0?$" · {track.Bits}-bit":"")+" (server reported)";
                var button=TrackButton(track.Track,track.Title,track.Artist,track.Duration,quality);
                button.Click+=(_,_)=>{if(onlineEnabled&&ReferenceEquals(owner,musicServer)){serverTracks=tracks;serverTrackIndex=tracks.ToList().FindIndex(x=>x.Id==track.Id);PlayServerMusic();}};ServerTrackItems.Children.Add(button);
            }
            MusicServerMessage.Text=tracks.Count+" tracks"+(tracks.Count>1000?" · displaying the first 1000":"")+". Original format will be requested.";
        }
        catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested)MusicServerMessage.Text="Could not load this album. Try again.";}
        finally{if(!token.IsCancellationRequested&&!localClosing)MusicServerBusy.IsVisible=false;}
    }
    private void PlayServerMusic()
    {
        if(!onlineEnabled||musicServer==null||serverTrackIndex<0||serverTrackIndex>=serverTracks.Count||NativePlaybackBusy)return;
        var track=serverTracks[serverTrackIndex];musicCurrentUrl=musicServer.Stream(track.Id);
        // The player relay owns this authenticated URL only in memory. No history or audio-choice key contains it.
        Play(musicCurrentUrl,track.Artist+" — "+track.Title,requestHeaders:new Dictionary<string,string>{{"Accept","audio/*"}},skipHistory:true);
    }
    private bool IsServerMusicPlaying=>musicCurrentUrl!=null&&currentPath==musicCurrentUrl&&musicServer!=null;
    private bool MoveServerMusic(int direction)
    {
        if(!IsServerMusicPlaying||!onlineEnabled||NativePlaybackBusy)return false;var next=serverTrackIndex+direction;
        if(next<0||next>=serverTracks.Count)return false;serverTrackIndex=next;PlayServerMusic();return true;
    }
}
