using System.Diagnostics;
using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private sealed record PlaybackSnapshot(VLCState State, long Length, long Time, bool IsSeekable,
        int AudioTrack, int Spu, TrackOption[] Audio, TrackOption[] Subtitles, long CapturedAt, float Rate = 1)
    {
        public bool IsPlaying => State == VLCState.Playing;
        public static PlaybackSnapshot Empty { get; } = new(VLCState.Stopped, 0, 0, false, -1, -1, [], [], 0);
    }

    private PlaybackSnapshot playbackSnapshot = PlaybackSnapshot.Empty;
    private bool pollingPlayer;
    private bool playbackFailureHandled;
    private readonly SubtitlePlaybackClock captionClock = new();
    private long CaptionPosition
    {
        get
        {
            captionClock.Observe(playbackSnapshot.Time, Stopwatch.GetElapsedTime(0, playbackSnapshot.CapturedAt),
                playbackSnapshot.IsPlaying, playbackSnapshot.Rate);
            return captionClock.Position(Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()));
        }
    }

    private static PlaybackSnapshot ReadPlaybackSnapshot(MediaPlayer native, PlaybackSnapshot previous)
    {
        var state = native.State;
        // Native metadata getters may wait on VLC's input/decoder locks. All
        // polling happens on the same worker queue as stop/start, never in UI
        // timers, drawing, page navigation or save-progress handlers.
        if (state == VLCState.Buffering)
            return previous with { State = state, CapturedAt = Stopwatch.GetTimestamp() };
        if (state is VLCState.Opening or VLCState.Stopped or VLCState.Error)
            return new(state, 0, 0, false, -1, -1, [], [], Stopwatch.GetTimestamp());
        var time = native.Time;
        var capturedAt = Stopwatch.GetTimestamp();
        return new(state, native.Length, time, native.IsSeekable, native.AudioTrack, native.Spu,
            native.AudioTrackDescription.Where(x => x.Id >= 0).Select(x => new TrackOption(x.Id, x.Name)).ToArray(),
            native.SpuDescription.Where(x => x.Id >= 0).Select(x => new TrackOption(x.Id, x.Name)).ToArray(),
            capturedAt, native.Rate);
    }

    private Task QueuePlayerCommand(Action<MediaPlayer> command)
    {
        var native = player;
        var generation = mediaGeneration;
        return native == null || localClosing ? Task.CompletedTask : nativePlayback.Enqueue(() =>
        {
            using var timing = diagnostics.Measure("native-command");
            if (generation == Volatile.Read(ref mediaGeneration)) command(native);
        });
    }

    private void HandlePlaybackFailure()
    {
        if (playbackFailureHandled || localClosing) return;
        playbackFailureHandled = true;
        PlayerLoading.IsVisible = false;
        var failure = torrentSession?.Failure ?? "Playback failed. The source may be unavailable, damaged, or unsupported.";
        if (TryScheduleNextSource(failure)) return;
        if (torrentSession != null) StopTorrentPlayback();
        PlayerNotice(failure);
        ShowSourceRecovery("Playback interrupted",failure);
    }
}
