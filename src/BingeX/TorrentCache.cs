using System.Text.RegularExpressions;

namespace BingeX;

internal static class TorrentCache
{
    public static bool IsOwned(string name) => Regex.IsMatch(name, "^(session-[a-f0-9]{32}|torrent-[a-f0-9]{40})$");
    public static void Reserve(string root, string activeDirectory, long size)
    {
        var others = Directory.EnumerateDirectories(root)
            .Where(x => x != activeDirectory && IsOwned(Path.GetFileName(x)))
            .OrderBy(Directory.GetLastWriteTimeUtc).ToList();
        long Reserved(string directory)
        {
            try { return long.TryParse(File.ReadAllText(Path.Combine(directory, "reserved.txt")), out var amount) ? Math.Max(0, amount) : 0; }
            catch (IOException) { return 0; }
        }
        var used = others.Sum(Reserved);
        foreach (var directory in others)
        {
            if (used + size <= TorrentPlayback.CacheLimit) break;
            var amount = Reserved(directory);
            if (Delete(root, directory)) used -= amount;
        }
        if (used + size > TorrentPlayback.CacheLimit) throw new IOException("The torrent cache is full and its remaining downloads are in use.");
        File.WriteAllText(Path.Combine(activeDirectory, "reserved.txt"), size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.SetLastWriteTimeUtc(activeDirectory, DateTime.UtcNow);
    }
    public static bool Delete(string root, string path)
    {
        try
        {
            if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return false;
            var resolved = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(resolved), root, StringComparison.OrdinalIgnoreCase) || !IsOwned(Path.GetFileName(resolved))) return false;
            if (!Directory.Exists(resolved)) return true;
            var pending = new Stack<string>(); pending.Push(resolved);
            while (pending.TryPop(out var directory))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return false;
                foreach (var item in Directory.EnumerateFileSystemEntries(directory))
                {
                    var attributes = File.GetAttributes(item);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(item);
                }
            }
            // A running download holds this file exclusively.
            using (new FileStream(Path.Combine(resolved, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
            Directory.Delete(resolved, true); return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
