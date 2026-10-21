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
        var relay = headerStreamRelay;
        headerStreamRelay = null;
        return native == null ? Task.CompletedTask : nativePlayback.Enqueue(() =>
        {
            using var timing = diagnostics.Measure("native-stop");
            // Release upstream reads before VLC waits for its HTTP input to stop.
            relay?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            native.Stop();
        });
    }
    private async Task FinishWindowCloseAsync()
    {
        localClosing = true;
        cloudstreamLifetime.Cancel();
        playbackRequest++; playbackStarting = false;
        addonCheckLifetime.Cancel();
        SaveProgress();
        CancelLocalScan(); CancelOnlineSubtitles();
        timer.Stop(); overlayTimer.Stop(); videoClickTimer.Stop();
        StopTorrentPlayback();
        await StopNativePlayerAsync();
        await torrentCleanup;
        if (headerStreamRelay != null) await headerStreamRelay.DisposeAsync();
        await cloudstream.DisposeAsync();
    }
}
