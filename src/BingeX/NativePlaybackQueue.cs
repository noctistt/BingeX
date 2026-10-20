namespace BingeX;

// VLC 3 stop joins native decoder threads. Keep those waits off the dispatcher,
// and serialize them so an old stop cannot stop a newly selected stream.
public sealed class NativePlaybackQueue
{
    private readonly object sync = new();
    private Task tail = Task.CompletedTask;
    private int pending;
    public bool IsBusy => Volatile.Read(ref pending) != 0;
    public Task Enqueue(Action action)
    {
        lock (sync)
        {
            var previous = tail;
            Interlocked.Increment(ref pending);
            return tail = Task.Run(async () =>
            {
                try
                {
                    try { await previous.ConfigureAwait(false); } catch { /* A failed operation must not poison subsequent stops. */ }
                    action();
                }
                finally { Interlocked.Decrement(ref pending); }
            });
        }
    }
}
