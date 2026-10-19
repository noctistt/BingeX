using Microsoft.Data.Sqlite;

namespace BingeX;

public sealed partial class LibraryStore
{
    public IReadOnlyList<string> Folders()
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path FROM library_folders ORDER BY path COLLATE NOCASE";
        using var reader = command.ExecuteReader();
        var folders = new List<string>();
        while (reader.Read()) folders.Add(reader.GetString(0));
        return folders;
    }
    public void AddFolder(string path)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO library_folders VALUES($path)";
        command.Parameters.AddWithValue("$path", LocalLibraryScanner.NormalizeFolder(path));
        command.ExecuteNonQuery();
    }
    public void RemoveFolder(string path)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM library_files WHERE folder=$path; DELETE FROM library_folders WHERE path=$path";
        command.Parameters.AddWithValue("$path", LocalLibraryScanner.NormalizeFolder(path));
        command.ExecuteNonQuery();
        transaction.Commit();
    }
    public void ApplyFolderScan(FolderScan scan)
    {
        // A disconnected drive or unreadable subtree must not wipe the cached library.
        if (!Folders().Contains(scan.Folder, StringComparer.OrdinalIgnoreCase)) return;
        using var transaction = connection.BeginTransaction();
        if (scan.Complete)
        {
            using var remove = connection.CreateCommand();
            remove.Transaction = transaction;
            remove.CommandText = "DELETE FROM library_files WHERE folder=$folder";
            remove.Parameters.AddWithValue("$folder", scan.Folder);
            remove.ExecuteNonQuery();
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR REPLACE INTO library_files VALUES($folder,$path,$title,$relative,$size,$modified)";
        command.Parameters.AddWithValue("$folder", scan.Folder);
        var path = command.Parameters.Add("$path", SqliteType.Text);
        var title = command.Parameters.Add("$title", SqliteType.Text);
        var relative = command.Parameters.Add("$relative", SqliteType.Text);
        var size = command.Parameters.Add("$size", SqliteType.Integer);
        var modified = command.Parameters.Add("$modified", SqliteType.Integer);
        foreach (var video in scan.Videos)
        {
            path.Value = video.Path; title.Value = video.Title; relative.Value = video.RelativePath;
            size.Value = video.Size; modified.Value = video.ModifiedTicks;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public IReadOnlyList<LocalVideo> LocalVideos(string? folder = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path,folder,title,relative_path,size,modified FROM library_files" +
            (folder == null ? "" : " WHERE folder=$folder") + " ORDER BY title COLLATE NOCASE,path,folder";
        if (folder != null) command.Parameters.AddWithValue("$folder", folder);
        using var reader = command.ExecuteReader();
        var videos = new List<LocalVideo>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        while (reader.Read())
            if (seen.Add(reader.GetString(0))) videos.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetInt64(5)));
        return videos;
    }
    public void RemoveHistory(string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM history WHERE path=$path";
        command.Parameters.AddWithValue("$path", key);
        command.ExecuteNonQuery();
    }
}
