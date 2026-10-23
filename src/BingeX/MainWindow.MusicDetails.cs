using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Text.Json;
namespace BingeX;
public record MusicNowTrack(string Key,string Title,string Artist,string Album,string? Artwork,double Duration,string Source);
public partial class MainWindow
{
    private MusicNowTrack? musicNowTrack;
    private readonly SubtitlePlaybackClock musicLyricClock=new();
    private readonly Avalonia.Threading.DispatcherTimer musicLyricTimer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    private readonly MusicLyricsClient musicLyricsClient=new();
    private CancellationTokenSource musicLyricsWork=new(),musicNowArtWork=new();
    private MusicLyrics? musicLyrics;
    private int musicLyricIndex=-2;
    private bool musicLyricOffsetUpdating;
    private long musicLyricEpoch;
    private readonly Dictionary<string,(MusicLyrics Lyrics,string Source,decimal Offset)> musicLyricCache=new();
    private readonly List<Button> musicLyricButtons=[];
    private void SetupMusicNowDetails()
    {
        musicLyricTimer.Tick+=(_,_)=>{if(currentIsMusic&&MusicNowDetails.IsExpanded&&MusicPage.IsVisible)UpdateMusicLyricHighlight();};musicLyricTimer.Start();
        Closed+=(_,_)=>{musicLyricTimer.Stop();musicLyricsWork.Cancel();musicNowArtWork.Cancel();musicLyricsClient.Dispose();};
    }
    private void CancelMusicLyrics(){musicLyricsWork.Cancel();musicLyricsWork.Dispose();musicLyricsWork=new();MusicLyricsBusy.IsVisible=false;MusicLyricsFind.IsEnabled=true;}
    private static string MusicItemText(JsonElement item,string field)=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
    private void SetMusicNowTrack(string path,string title)
    {
        musicLyricEpoch++;musicLyricClock.Reset();CancelMusicLyrics();musicNowArtWork.Cancel();musicNowArtWork.Dispose();musicNowArtWork=new();
        musicLyrics=null;musicLyricIndex=-2;musicLyricButtons.Clear();MusicLyricLines.Children.Clear();MusicLyricMatches.Children.Clear();MusicLyricsPlain.Text="";MusicNowArtwork.Source=null;MusicNowView.IsVisible=currentIsMusic;
        MusicNowQueueItems.Children.Clear();
        if(!currentIsMusic){musicNowTrack=null;return;}
        if(IsMusicFreePlaying&&musicFreeIndex>=0&&musicFreeIndex<musicFreeQueue.Length){
            var track=musicFreeQueue[musicFreeIndex];musicNowTrack=new("musicfree:"+musicFreeQueueProvider+":"+track.Id,track.Title,track.Artist,MusicItemText(track.Item,"album"),track.Artwork,track.Duration,musicFreeQueueProvider??"Music provider");
        }else if(IsServerMusicPlaying&&serverTrackIndex>=0&&serverTrackIndex<serverTracks.Count){
            var track=serverTracks[serverTrackIndex];musicNowTrack=new("server:"+musicServer!.GetHashCode()+":"+track.Id,track.Title,track.Artist,track.Album,null,track.Duration,"Music server");
        }else if(musicLibrary?.Tracks.FirstOrDefault(x=>string.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase)) is {} local){musicNowTrack=new("local:"+path,local.Title,local.Artist,local.Album,local.Cover,local.Seconds,"Local music");}
        else musicNowTrack=new("session:"+mediaGeneration,title,"","",null,0,"Music");
        MusicNowTrackTitle.Text=musicNowTrack.Title;MusicNowTrackArtist.Text=musicNowTrack.Artist.Length>0?musicNowTrack.Artist:"Artist details unavailable";
        MusicNowTrackAlbum.Text=musicNowTrack.Album;MusicNowTrackSource.Text=musicNowTrack.Source;
        MusicLyricsTitle.Text=musicNowTrack.Title;MusicLyricsArtist.Text=musicNowTrack.Artist;MusicLyricsAlbum.Text=musicNowTrack.Album;
        musicLyricOffsetUpdating=true;MusicLyricsOffset.Value=0;musicLyricOffsetUpdating=false;
        if(musicNowTrack.Artwork is {} cover)_=artwork.Fill(MusicNowArtwork,cover,musicNowArtWork.Token);
        MusicLyricsMessage.Text="Open Now Playing to look for matching lyrics, or load a local .lrc file.";
        RenderMusicNowQueue();
        if(musicLyricCache.TryGetValue(musicNowTrack.Key,out var cached)){
            musicLyricOffsetUpdating=true;MusicLyricsOffset.Value=cached.Offset;musicLyricOffsetUpdating=false;ApplyMusicLyrics(cached.Lyrics,cached.Source);
        }else{
            if(Uri.TryCreate(path,UriKind.Absolute,out var uri)&&uri.IsFile)_=LoadMusicSidecar(uri.LocalPath,musicLyricEpoch);
            else if(MusicNowDetails.IsExpanded&&onlineEnabled)_=FindMusicLyrics(true);
        }
    }
    private async Task LoadMusicSidecar(string path,long epoch)
    {
        string? text=null;
        try{text=await Task.Run(()=>{var sidecar=Path.ChangeExtension(path,".lrc");return File.Exists(sidecar)&&new FileInfo(sidecar).Length<=256_000?File.ReadAllText(sidecar):null;});}catch{}
        if(localClosing||epoch!=musicLyricEpoch||musicLyrics!=null)return;
        if(text!=null){try{ApplyMusicLyrics(MusicLyrics.Parse(text),"Local lyrics");return;}catch{MusicLyricsMessage.Text="Local lyrics could not be read. Choose another file.";}}
        if(MusicNowDetails.IsExpanded&&onlineEnabled)await FindMusicLyrics(true);
    }

    private void RenderMusicNowQueue()
    {
        MusicNowQueueItems.Children.Clear();
        if(IsMusicFreePlaying){var queue=musicFreeQueue;foreach(var pair in queue.Select((track,index)=>(track,index)).Take(100)){
            var button=new Button{Content=pair.track.Title+" · "+pair.track.Artist,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};button.Classes.Set("primary",pair.index==musicFreeIndex);
            button.Click+=async(_,_)=>{if(!musicFreeWorking&&ReferenceEquals(queue,musicFreeQueue))await PlayMusicFree(pair.index);};MusicNowQueueItems.Children.Add(button);
        }}else if(IsServerMusicPlaying){var tracks=serverTracks;foreach(var pair in tracks.Select((track,index)=>(track,index)).Take(100)){
            var owner=musicServer;var button=new Button{Content=pair.track.Title+" · "+pair.track.Artist,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};button.Click+=(_,_)=>{if(ReferenceEquals(owner,musicServer)&&ReferenceEquals(tracks,serverTracks)){serverTrackIndex=pair.index;PlayServerMusic();}};MusicNowQueueItems.Children.Add(button);
        }}else{foreach(var path in localQueue.Items.Take(100)){var button=new Button{Content=musicLibrary?.Tracks.FirstOrDefault(x=>x.Path==path)?.Title??Path.GetFileNameWithoutExtension(path),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};button.Click+=(_,_)=>PlayQueueItem(path);MusicNowQueueItems.Children.Add(button);}}
    }
    private void OpenMusicNow_Click(object? sender,RoutedEventArgs e){Page("Music");MusicNowDetails.IsExpanded=true;Avalonia.Threading.Dispatcher.UIThread.Post(()=>MusicNowView.BringIntoView());}
    private async void MusicNowExpanded(object? sender,RoutedEventArgs e){if(musicNowTrack!=null&&musicLyrics==null&&onlineEnabled)await FindMusicLyrics(true);}
    private async void FindMusicLyrics_Click(object? sender,RoutedEventArgs e)=>await FindMusicLyrics(false);
    private async Task FindMusicLyrics(bool automatic)
    {
        if(musicNowTrack==null||!currentIsMusic)return;
        if(!onlineEnabled){MusicLyricsMessage.Text="Online mode is off. Local lyrics remain available.";return;}
        var track=musicNowTrack;var epoch=musicLyricEpoch;var title=MusicLyricsTitle.Text?.Trim()??"";var artist=MusicLyricsArtist.Text?.Trim()??"";var album=MusicLyricsAlbum.Text?.Trim()??"";
        CancelMusicLyrics();var token=musicLyricsWork.Token;MusicLyricsBusy.IsVisible=true;MusicLyricsFind.IsEnabled=false;MusicLyricMatches.Children.Clear();MusicLyricsMessage.Text="Finding lyrics on LRCLIB…";
        try{
            var duration=track.Duration>0?track.Duration:playbackSnapshot.Length/1000d;
            var matches=await musicLyricsClient.Search(title,artist,album,duration,token);token.ThrowIfCancellationRequested();if(localClosing||epoch!=musicLyricEpoch)return;
            var exact=matches.Where(x=>MusicLyricsClient.Exact(x,title,artist,album,duration)).ToArray();
            if(exact.Length==1){SelectMusicLyrics(exact[0]);return;}
            foreach(var match in matches){
                var difference=duration>0?$" · length difference {Math.Abs(match.Duration-duration):0.#}s":"";
                var label=match.TrackName+" · "+match.ArtistName+" · "+Clock((long)(match.Duration*1000))+difference+(!string.IsNullOrWhiteSpace(match.SyncedLyrics)?" · synced":" · plain");
                var button=new Button{Content=new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap},HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
                button.Click+=(_,_)=>{if(epoch==musicLyricEpoch)SelectMusicLyrics(match);};MusicLyricMatches.Children.Add(button);
            }
            MusicLyricsMessage.Text=matches.Count==0?"No lyrics found. Edit the matching details or load a local .lrc file.":"Choose the recording that matches your track. Remixes may use different timing.";
        }catch(OperationCanceledException){}
        catch{if(!token.IsCancellationRequested&&epoch==musicLyricEpoch&&!localClosing)MusicLyricsMessage.Text="Lyrics could not be loaded. Retry or use a local .lrc file.";}
        finally{if(!token.IsCancellationRequested&&epoch==musicLyricEpoch&&!localClosing){MusicLyricsBusy.IsVisible=false;MusicLyricsFind.IsEnabled=true;}}
    }
    private void SelectMusicLyrics(MusicLyricMatch match)
    {
        if(match.Instrumental){ApplyMusicLyrics(new([],"Instrumental — no lyrics"),"LRCLIB · instrumental");return;}
        try{ApplyMusicLyrics(match.Lyrics,"LRCLIB · "+match.ArtistName+" — "+match.TrackName);}catch{MusicLyricsMessage.Text="This lyric file is not supported. Choose another match.";}
    }
    private void ApplyMusicLyrics(MusicLyrics lyrics,string source)
    {
        musicLyrics=lyrics;musicLyricIndex=-2;MusicLyricMatches.Children.Clear();MusicLyricLines.Children.Clear();musicLyricButtons.Clear();
        MusicLyricsPlain.Text=lyrics.Lines.Length==0?lyrics.Plain:"";
        foreach(var line in lyrics.Lines.Take(1000)){var button=new Button{Content=new TextBlock{Text=line.Text,TextWrapping=TextWrapping.Wrap},HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch,HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Left};button.Click+=(_,_)=>{if(!NativePlaybackBusy&&playbackSnapshot.IsSeekable&&playbackSnapshot.Length>0){resumeAt=0;musicLyricClock.Reset();var target=Math.Clamp(line.Time+(long)(MusicLyricsOffset.Value??0),0,Math.Max(0,playbackSnapshot.Length));_=QueuePlayerCommand(native=>native.Time=target);}};musicLyricButtons.Add(button);MusicLyricLines.Children.Add(button);}
        MusicLyricsMessage.Text=source+(lyrics.Lines.Length>0?" · synced":" · plain lyrics")+(lyrics.Lines.Length>1000?" · showing first 1000 lines":"");CacheMusicLyrics(source);UpdateMusicLyricHighlight();
    }
    private void CacheMusicLyrics(string? source=null)
    {
        if(musicNowTrack==null||musicLyrics==null)return;if(musicLyricCache.Count>=25&&!musicLyricCache.ContainsKey(musicNowTrack.Key))musicLyricCache.Remove(musicLyricCache.Keys.First());
        var label=source??(musicLyricCache.TryGetValue(musicNowTrack.Key,out var old)?old.Source:"Lyrics");musicLyricCache[musicNowTrack.Key]=(musicLyrics,label,MusicLyricsOffset.Value??0);
    }
    private void MusicLyricsOffsetChanged(object? sender,Avalonia.Controls.NumericUpDownValueChangedEventArgs e){if(musicLyricOffsetUpdating)return;musicLyricIndex=-2;CacheMusicLyrics();UpdateMusicLyricHighlight();}
    private void UpdateMusicLyricHighlight()
    {
        if(musicLyrics==null||!currentIsMusic)return;musicLyricClock.Observe(playbackSnapshot.Time,System.Diagnostics.Stopwatch.GetElapsedTime(0,playbackSnapshot.CapturedAt),playbackSnapshot.IsPlaying,playbackSnapshot.Rate);var position=musicLyricClock.Position(System.Diagnostics.Stopwatch.GetElapsedTime(0,System.Diagnostics.Stopwatch.GetTimestamp()));var index=musicLyrics.Active(position,(long)(MusicLyricsOffset.Value??0));if(index==musicLyricIndex)return;
        if(musicLyricIndex>=0&&musicLyricIndex<musicLyricButtons.Count)musicLyricButtons[musicLyricIndex].Classes.Set("primary",false);musicLyricIndex=index;
        if(index>=0&&index<musicLyricButtons.Count){musicLyricButtons[index].Classes.Set("primary",true);if(MusicNowDetails.IsExpanded&&MusicPage.IsVisible)musicLyricButtons[index].BringIntoView();}
    }
    private async void ImportMusicLyrics_Click(object? sender,RoutedEventArgs e)
    {
        if(musicNowTrack==null)return;var epoch=musicLyricEpoch;
        var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Load music lyrics",AllowMultiple=false,FileTypeFilter=[new FilePickerFileType("Lyrics"){Patterns=["*.lrc","*.txt"]}]});
        if(localClosing||epoch!=musicLyricEpoch||files.FirstOrDefault()?.TryGetLocalPath() is not {} path)return;
        await LoadMusicLyricsFile(path,epoch);
    }
    private async Task LoadMusicLyricsFile(string path,long epoch)
    {
        try{if(new FileInfo(path).Length>256_000)throw new InvalidDataException();var text=await File.ReadAllTextAsync(path);if(localClosing||epoch!=musicLyricEpoch)return;CancelMusicLyrics();ApplyMusicLyrics(MusicLyrics.Parse(text),"Local lyrics");}catch{if(!localClosing&&epoch==musicLyricEpoch)MusicLyricsMessage.Text="Could not load these lyrics. Choose an LRC or text file smaller than 256 KB.";}
    }
}
