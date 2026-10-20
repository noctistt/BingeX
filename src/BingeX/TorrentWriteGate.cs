using MonoTorrent;
using MonoTorrent.PieceWriter;
using ReusableTasks;

namespace BingeX;

// A magnet can learn its file list while keeping its peer connections. Payload writes
// wait here until the application has checked that list and its disk budget.
internal sealed class TorrentWriteGate(IPieceWriter writer, string directory, CancellationToken token) : IPieceWriter
{
    private readonly TaskCompletionSource<bool> approval = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int OpenFiles => writer.OpenFiles;
    public int MaximumOpenFiles => writer.MaximumOpenFiles;
    public void Approve() { approval.TrySetResult(true); }
    private void CheckPath(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Torrent data cannot be written outside its cache folder.");
    }
    public async ReusableTask WriteAsync(ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
    {
        await approval.Task.WaitAsync(token);
        token.ThrowIfCancellationRequested(); CheckPath(file.FullPath);
        await writer.WriteAsync(file, offset, buffer);
    }
    public async ReusableTask<int> ReadAsync(ITorrentManagerFile file, long offset, Memory<byte> buffer)
    {
        CheckPath(file.FullPath); return await writer.ReadAsync(file, offset, buffer);
    }
    public async ReusableTask<bool> ExistsAsync(ITorrentManagerFile file)
    {
        CheckPath(file.FullPath); return await writer.ExistsAsync(file);
    }
    public ReusableTask CloseAsync(ITorrentManagerFile file) => writer.CloseAsync(file);
    public ReusableTask FlushAsync(ITorrentManagerFile file) => writer.FlushAsync(file);
    public async ReusableTask MoveAsync(ITorrentManagerFile file, string path, bool overwrite)
    {
        await approval.Task.WaitAsync(token);
        CheckPath(file.FullPath); CheckPath(path); await writer.MoveAsync(file, path, overwrite);
    }
    public ReusableTask SetMaximumOpenFilesAsync(int count) => writer.SetMaximumOpenFilesAsync(count);
    public void Dispose() => writer.Dispose();
}
