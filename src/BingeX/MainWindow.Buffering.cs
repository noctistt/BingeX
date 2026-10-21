using System.Diagnostics;
using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private readonly PlaybackWaitTracker playbackWait = new();
    private readonly Stopwatch waitClock = Stopwatch.StartNew();
    private void ResetPlaybackWaiting()
    {
        playbackWait.Reset(waitClock.Elapsed);
        PlaybackWaiting.IsVisible = false;
    }
    private void UpdatePlaybackWaiting()
    {
        if (!PlayerPage.IsVisible || currentPath == null || playbackStarting || PlayerLoading.IsVisible || playbackEnded)
        { ResetPlaybackWaiting(); return; }
        var waiting = playbackWait.Update(waitClock.Elapsed, playbackSnapshot.Time, playbackSnapshot.IsPlaying, playbackSnapshot.State == VLCState.Buffering);
        PlaybackWaiting.IsVisible = waiting;
        PlaybackWaitingLabel.Text = playbackSnapshot.State == VLCState.Buffering ? "Buffering…" : "Waiting for playback…";
        if (waiting && activePage == "Player") Status("Buffering · " + currentTitle);
    }
}
