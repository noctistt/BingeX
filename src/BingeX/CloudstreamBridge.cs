using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BingeX;

/// <summary>Experimental desktop engine transport. Does not install or trust extensions.</summary>
public sealed class CloudstreamBridge : IAsyncDisposable
{
    private readonly Process process;
    private readonly SemaphoreSlim requests = new(1);
    private readonly Task drainErrors;
    private int sequence;
    private bool disposed;
    private bool resourcesDisposed;

    public CloudstreamBridge(string javaExecutable, string engineDirectory, string dataDirectory)
    {
        if (!File.Exists(javaExecutable)) throw new FileNotFoundException("Cloudstream Java runtime is missing.");
        if (!File.Exists(Path.Combine(engineDirectory, "engine-jvm.jar")))
            throw new FileNotFoundException("Cloudstream engine is missing.");
        Directory.CreateDirectory(dataDirectory);
        var start = new ProcessStartInfo(Path.GetFullPath(javaExecutable))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[] { "-Xmx512m", "-cp", Path.Combine(Path.GetFullPath(engineDirectory), "*"),
                     "dev.akash.cs3engine.jvm.MainKt", "--stdio", Path.GetFullPath(dataDirectory) })
            start.ArgumentList.Add(argument);
        process = Process.Start(start) ?? throw new IOException("Cloudstream helper could not start.");
        // Drain continuously, without storing potentially private provider logs.
        drainErrors = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is not null) { }
        });
    }

    public async Task<JsonElement> CallAsync(string method, object parameters,
        CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(45));
        await requests.WaitAsync(deadline.Token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var id = ++sequence;
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }).AsMemory(), deadline.Token);
            await process.StandardInput.FlushAsync(deadline.Token);
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(deadline.Token)
                    ?? throw new IOException("Cloudstream helper exited.");
                using var message = JsonDocument.Parse(line);
                var root = message.RootElement;
                if (!root.TryGetProperty("id", out var responseId))
                {
                    // No browser, authentication, or extension UI host in the compatibility probe.
                    if (root.TryGetProperty("event", out var eventName) && eventName.GetString() is "webRequest" or "ui" or "uiOpened")
                        throw new NotSupportedException("This extension requires an interactive host.");
                    continue;
                }
                if (responseId.ValueKind != JsonValueKind.Number || responseId.GetInt32() != id) continue;
                if (root.TryGetProperty("error", out _))
                    throw new InvalidOperationException("Cloudstream extension request failed.");
                return root.GetProperty("result").Clone();
            }
        }
        catch
        {
            // A cancelled request can still execute in Java: terminate before another request is sent.
            Stop();
            throw;
        }
        finally { requests.Release(); }
    }

    private void Stop()
    {
        disposed = true;
        if (resourcesDisposed) return;
        if (!process.HasExited) process.Kill(entireProcessTree: true);
    }

    public async ValueTask DisposeAsync()
    {
        await requests.WaitAsync();
        try
        {
            if (resourcesDisposed) return;
            Stop();
            await process.WaitForExitAsync();
            await drainErrors;
            process.Dispose();
            resourcesDisposed = true;
        }
        finally { requests.Release(); }
    }
}
