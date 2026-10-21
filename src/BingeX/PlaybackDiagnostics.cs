using System.Diagnostics;

namespace BingeX;

// Best-effort local timings only: callers supply fixed stage names, never media
// names, URLs, headers or credentials. Disk work cannot block the dispatcher.
internal sealed class PlaybackDiagnostics(string dataDirectory)
{
    private readonly object sync = new();
    private Task tail = Task.CompletedTask;
    private int pending;
    public IDisposable Measure(string stage) => new Scope(this, stage);

    public void Record(string stage, TimeSpan elapsed)
    {
        if (elapsed.TotalMilliseconds < 250) return;
        if (Interlocked.Increment(ref pending) > 128) { Interlocked.Decrement(ref pending); return; }
        var line = $"{DateTime.UtcNow:O} {stage} {elapsed.TotalMilliseconds:0}ms{Environment.NewLine}";
        lock (sync)
        {
            var previous = tail;
            tail = Task.Run(async () =>
            {
                try
                {
                    await previous.ConfigureAwait(false);
                    var path = Path.Combine(dataDirectory, "playback-performance.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 65536)
                        File.Move(path, path + ".previous", overwrite: true);
                    await File.AppendAllTextAsync(path, line).ConfigureAwait(false);
                }
                catch (Exception) { /* Diagnostics must never interrupt playback. */ }
                finally { Interlocked.Decrement(ref pending); }
            });
        }
    }

    private sealed class Scope(PlaybackDiagnostics owner, string stage) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        public void Dispose() => owner.Record(stage, Stopwatch.GetElapsedTime(started));
    }
}
