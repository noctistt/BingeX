namespace BingeX;

public record LocalVideo(string Path, string Folder, string Title, string RelativePath, long Size, long ModifiedTicks, long AddedTicks = 0, long LastWatchedTicks = 0);
public record FolderScan(string Folder, IReadOnlyList<LocalVideo> Videos, bool Complete, int Skipped);

public static class LocalLibraryScanner
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts",
        ".wmv", ".flv", ".ogv", ".3gp", ".3g2", ".vob", ".m2v", ".m1v", ".divx", ".asf", ".rm",
        ".rmvb", ".mxf", ".f4v", ".qt", ".m2t", ".ogm", ".mpe", ".mpv"
    };
    public static bool IsVideo(string path)=>Extensions.Contains(Path.GetExtension(path));
    public static string NormalizeFolder(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public static FolderScan Scan(string path, CancellationToken token)
    {
        var root = NormalizeFolder(path);
        var videos = new List<LocalVideo>();
        var pending = new Stack<string>();
        pending.Push(root);
        var skipped = 0;
        while (pending.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        // Do not follow junctions/symlinks into another tree or a cycle.
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attributes & FileAttributes.Directory) != 0) { pending.Push(entry); continue; }
                        if (!Extensions.Contains(Path.GetExtension(entry))) continue;
                        var file = new FileInfo(entry);
                        videos.Add(new(file.FullName, root, Path.GetFileNameWithoutExtension(entry),
                            Path.GetRelativePath(root, entry), file.Length, file.LastWriteTimeUtc.Ticks));
                    }
                    catch (Exception error) when (ReadFailure(error)) { skipped++; }
                }
            }
            catch (Exception error) when (ReadFailure(error)) { skipped++; }
        }
        return new(root, videos.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToArray(), skipped == 0, skipped);
    }
    private static bool ReadFailure(Exception error) => error is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
