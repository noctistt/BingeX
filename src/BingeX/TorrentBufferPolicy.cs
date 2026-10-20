namespace BingeX;

public static class TorrentBufferPolicy
{
    public const int StartupBytes = 8 * 1024 * 1024;
    public static long StartupTarget(long fileLength, int? durationSeconds) => Math.Min(fileLength,
        durationSeconds is > 0 ? Math.Clamp((long)(fileLength / (double)durationSeconds.Value * 12), 4L * 1024 * 1024, 32L * 1024 * 1024) : StartupBytes);
    public static long ContiguousBytes(long fileOffset, long fileLength, long position, int pieceLength, Func<int, bool> available)
    {
        if (pieceLength <= 0 || fileLength <= 0 || position < 0 || position >= fileLength) return 0;
        var start = fileOffset + position;
        var end = Math.Min(fileOffset + fileLength, start + 128L * 1024 * 1024);
        var cursor = start;
        while (cursor < end)
        {
            var index = cursor / pieceLength;
            if (index > int.MaxValue || !available((int)index)) break;
            cursor = Math.Min(end, (index + 1) * pieceLength);
        }
        return cursor - start;
    }
    public static double? EstimateSeconds(long bufferedBytes, long fileLength, long durationMs) => fileLength > 0 && durationMs > 0
        ? bufferedBytes / (double)fileLength * durationMs / 1000 : null;
}

public sealed class DeliveryRate
{
    private readonly Queue<(double Time, long Bytes)> samples = new();
    public long Add(double seconds, long bytes)
    {
        samples.Enqueue((seconds, bytes));
        while (samples.Count > 2 && seconds - samples.Peek().Time > 10) samples.Dequeue();
        var first = samples.Peek();
        return seconds > first.Time ? (long)Math.Max(0, (bytes - first.Bytes) / (seconds - first.Time)) : 0;
    }
}
