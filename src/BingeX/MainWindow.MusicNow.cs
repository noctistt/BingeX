using Avalonia.Input;
using Avalonia.Interactivity;
namespace BingeX;
public partial class MainWindow
{
    private bool currentIsMusic, musicSeekUpdating, musicSeekDragging;
    private void SetupMusicNowBar()
    {
        foreach(var seek in new[]{MusicNowSeek}){
            seek.AddHandler(PointerPressedEvent,(_,_)=>musicSeekDragging=true,RoutingStrategies.Tunnel);
            seek.AddHandler(PointerReleasedEvent,(_,_)=>{musicSeekDragging=false;SeekMusicNow();},RoutingStrategies.Tunnel);
        }
        MusicNowSeek.PropertyChanged+=(_,e)=>{if(e.Property.Name=="Value"&&!musicSeekUpdating&&!musicSeekDragging)SeekMusicNow();};
    }
    private void SeekMusicNow()
    {
        if(NativePlaybackBusy||player==null||!playbackSnapshot.IsSeekable||playbackSnapshot.Length<=0)return;
        resumeAt=0;musicLyricClock.Reset();var target=(long)(MusicNowSeek.Value/1000*playbackSnapshot.Length);
        _=QueuePlayerCommand(native=>native.Time=target);
    }
    private void UpdateMusicNowBar()
    {
        MusicNowBar.IsVisible=currentIsMusic&&currentPath!=null;
        MusicNowTitle.Text=currentTitle;
        StatusLabel.IsVisible=!MusicNowBar.IsVisible;
        MusicNowPrevious.IsEnabled=QueuePreviousButton.IsEnabled;MusicNowNext.IsEnabled=QueueNextButton.IsEnabled;
        MusicNowState.Text=playbackSnapshot.State switch { LibVLCSharp.Shared.VLCState.Opening or LibVLCSharp.Shared.VLCState.Buffering=>"Buffering…",LibVLCSharp.Shared.VLCState.Error=>"Playback failed. Retry or choose another track.",LibVLCSharp.Shared.VLCState.Paused=>"Paused",_=>"" };MusicNowState.IsVisible=MusicNowState.Text.Length>0;
        MusicNowPlay.Content=playbackSnapshot.IsPlaying?"Pause":"Play";
        MusicNowPlay.IsEnabled=!NativePlaybackBusy;
        MusicNowTime.Text=Clock(playbackSnapshot.Time)+" / "+(playbackSnapshot.Length>0?Clock(playbackSnapshot.Length):"--:--");
        MusicNowSeek.IsEnabled=!NativePlaybackBusy&&playbackSnapshot.IsSeekable&&playbackSnapshot.Length>0;
        if(!musicSeekDragging){musicSeekUpdating=true;MusicNowSeek.Value=playbackSnapshot.Length>0?playbackSnapshot.Time*1000d/playbackSnapshot.Length:0;musicSeekUpdating=false;}
    }
}
