using Avalonia.Interactivity;
using LibVLCSharp.Shared;
namespace BingeX;
public partial class MainWindow
{
    private bool sourceResumePending;
    private DateTime? stalledSourceSince;
    private long recoveryLastTime;
    private bool recoveryTimedOut;
    private void ResetSourceRecovery()
    {
        stalledSourceSince=null; recoveryLastTime=0; recoveryTimedOut=false;
        PlayerRecovery.IsVisible=false;
    }
    private void ShowSourceRecovery(string title,string detail)
    {
        PlayerRecoveryTitle.Text=title; PlayerRecoveryDetail.Text=detail;
        PlayerRecovery.IsVisible=onlineEnabled && currentContext!=null && !miniPlayer;
        PlayerLoading.IsVisible=false;
        RevealPlayerControls(); Status(title+" · "+currentTitle);
    }
    private void UpdateSourceRecovery()
    {
        var canChoose=onlineEnabled && currentContext!=null && currentContext.Type!="torrent-file";
        ChooseSourceButton.IsVisible=canChoose;
        if(!canChoose || miniPlayer)PlayerRecovery.IsVisible=false;
        if(activeSource==null || activeSourceContext?.HistoryKey!=currentContext?.HistoryKey || playbackEnded || playbackStarting || pendingSource!=null)return;
        var snapshot=playbackSnapshot;
        if(snapshot.Time>recoveryLastTime && snapshot.IsPlaying)
        {
            recoveryLastTime=snapshot.Time; stalledSourceSince=null; recoveryTimedOut=false; PlayerRecovery.IsVisible=false;
            return;
        }
        recoveryLastTime=snapshot.Time;
        if(snapshot.State==VLCState.Paused){stalledSourceSince=null;return;}
        var waiting=snapshot.State is VLCState.Opening or VLCState.Buffering || PlaybackWaiting.IsVisible;
        if(!waiting){stalledSourceSince=null;return;}
        stalledSourceSince??=DateTime.UtcNow;
        if(!recoveryTimedOut && DateTime.UtcNow-stalledSourceSince.Value>TimeSpan.FromSeconds(30))
        {
            recoveryTimedOut=true;
            ShowSourceRecovery("Stream is taking too long","No playback progress for 30 seconds. The source may still recover; you can choose another source.");
        }
    }
    private async void ChooseAnotherSource_Click(object? sender,RoutedEventArgs e)=>await OpenCurrentSources();
    private async Task OpenCurrentSources()
    {
        if(!onlineEnabled || currentContext is not {} context || context.Type=="torrent-file")return;
        SaveProgress(); CancelEpisodeAutoplay(); CancelSourceSwitch(); sourcePlan=null;
        var title=playingEpisodeTitle ?? new TitleInfo(context.Id,context.Type,context.Name,context.Poster,"","","","","",context.AddonId,[]);
        await ShowTitle(title,context.EpisodeId);
        if(selectedTitle?.Id!=context.Id || activePage!="Details")return;
        if(context.EpisodeId!=null && selectedContext?.HistoryKey!=context.HistoryKey)
        {
            // The playing episode ID remains usable even if metadata no longer lists it.
            selectedContext=context;
            await FindStreams();
        }
        else if(context.EpisodeId==null)await FindStreams();
    }
}
