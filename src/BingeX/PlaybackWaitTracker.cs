namespace BingeX;

public sealed class PlaybackWaitTracker
{
    private long position = -1;
    private TimeSpan progressAt;
    public void Reset(TimeSpan now) { position = -1; progressAt = now; }
    public bool Update(TimeSpan now, long currentPosition, bool playing, bool buffering)
    {
        if (buffering) { position = currentPosition; progressAt = now; return true; }
        if (!playing || currentPosition <= 0) { Reset(now); return false; }
        if (position != currentPosition) { position = currentPosition; progressAt = now; return false; }
        return now - progressAt >= TimeSpan.FromSeconds(2);
    }
}
