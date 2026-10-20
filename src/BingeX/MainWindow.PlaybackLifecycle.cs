using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private readonly NativePlaybackQueue nativePlayback = new();
    private bool playbackStarting;
    private int playbackRequest;
    private bool NativePlaybackBusy => playbackStarting || nativePlayback.IsBusy;
    private Task StopNativePlayerAsync()
    {
        videoClickTimer.Stop(); videoPressPoint = null;
        mediaGeneration++;
        var native = player;
        return native == null ? Task.CompletedTask : nativePlayback.Enqueue(native.Stop);
    }
    private async Task FinishWindowCloseAsync()
    {
        localClosing = true;
        playbackRequest++; playbackStarting = false;
        addonCheckLifetime.Cancel();
        SaveProgress();
        CancelLocalScan(); CancelOnlineSubtitles();
        timer.Stop(); overlayTimer.Stop(); videoClickTimer.Stop();
        StopTorrentPlayback();
        await StopNativePlayerAsync();
        await torrentCleanup;
    }
}
