namespace BingeX;

// VLC time can repeat across polls. Keep one interpolation anchor until the
// reported position changes, rather than rewinding on every poll.
public sealed class SubtitlePlaybackClock
{
    private bool initialized, playing;
    private long reported;
    private double anchor;
    private double rate = 1;
    private TimeSpan anchoredAt, sampledAt;
    public void Reset() => initialized = false;
    public void Observe(long position, TimeSpan capturedAt, bool isPlaying, double speed)
    {
        if (initialized && capturedAt == sampledAt) return;
        speed = double.IsFinite(speed) && speed > 0 ? speed : 1;
        var previous = Position(capturedAt);
        var changed = !initialized || position != reported || playing != isPlaying || rate != speed;
        sampledAt = capturedAt;
        if (changed)
        {
            // Small backwards corrections during continuous playback must not
            // cross the same cue boundary twice. Real seeks reset explicitly.
            anchor = initialized && playing && isPlaying && position >= reported && previous - position <= 750 * speed
                ? Math.Max(position, previous) : position;
            anchoredAt = capturedAt;
            reported = position;
        }
        initialized = true; playing = isPlaying; rate = speed;
    }
    public long Position(TimeSpan now)
    {
        if (!initialized) return 0;
        // Bound prediction so a stalled source cannot run captions indefinitely.
        var advance = playing ? Math.Clamp((now - anchoredAt).TotalMilliseconds, 0, 750) * rate : 0;
        return (long)Math.Max(0, anchor + advance);
    }
}
