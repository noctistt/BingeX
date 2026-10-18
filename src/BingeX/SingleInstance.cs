using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BingeX;

// Each library has one owner. Development libraries remain independent of the installed app.
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly string pipeName;
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentQueue<string[]> requests = new();
    private readonly Task? listener;
    public bool IsPrimary { get; }

    public SingleInstance(string dataDirectory)
    {
        var identity = Environment.UserDomainName + "\\" + Environment.UserName + "|" + Path.GetFullPath(dataDirectory);
        if (OperatingSystem.IsWindows()) identity = identity.ToUpperInvariant();
        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        pipeName = "BingeX-" + suffix;
        mutex = new Mutex(true, (OperatingSystem.IsWindows() ? "Local\\" : "") + pipeName, out var created);
        IsPrimary = created;
        if (IsPrimary) listener = Task.Run(Listen);
    }

    public bool TryDequeue(out string[] args) => requests.TryDequeue(out args!);

    public async Task<bool> Forward(string[] args)
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(args);
            if (payload.Length > 65536) return false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token);
            await pipe.WriteAsync(BitConverter.GetBytes(payload.Length), timeout.Token);
            await pipe.WriteAsync(payload, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            var ack = new byte[1];
            await pipe.ReadExactlyAsync(ack, timeout.Token);
            return ack[0] == 1;
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException) { return false; }
    }

    private async Task Listen()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                var header = new byte[4];
                await pipe.ReadExactlyAsync(header, timeout.Token);
                var length = BitConverter.ToInt32(header);
                if (length is < 2 or > 65536) continue;
                var payload = new byte[length];
                await pipe.ReadExactlyAsync(payload, timeout.Token);
                var args = JsonSerializer.Deserialize<string[]>(payload);
                if (args == null || args.Any(x => x == null)) continue;
                requests.Enqueue(args);
                await pipe.WriteAsync(new byte[] { 1 }, timeout.Token);
                await pipe.FlushAsync(timeout.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or JsonException) { }
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        listener?.GetAwaiter().GetResult();
        stop.Dispose();
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
