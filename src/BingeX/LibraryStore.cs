using Microsoft.Data.Sqlite;

namespace BingeX;

public record RecentMedia(string Path, string Title, long Position, long Duration);
public record InstalledAddon(string Id, string Name, string ManifestUrl);

public sealed class LibraryStore : IDisposable
{
    private readonly SqliteConnection connection;
    public LibraryStore(string? dataDirectory = null)
    {
        var directory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BingeX");
        Directory.CreateDirectory(directory);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "library.db") }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS history(path TEXT PRIMARY KEY, title TEXT NOT NULL, position INTEGER NOT NULL, duration INTEGER NOT NULL, updated TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS addons(id TEXT PRIMARY KEY, name TEXT NOT NULL, url TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();
    }

    public void Save(RecentMedia media)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO history VALUES($path,$title,$position,$duration,$updated)";
        command.Parameters.AddWithValue("$path", media.Path);
        command.Parameters.AddWithValue("$title", media.Title);
        command.Parameters.AddWithValue("$position", media.Position);
        command.Parameters.AddWithValue("$duration", media.Duration);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public List<RecentMedia> History()
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path,title,position,duration FROM history ORDER BY updated DESC LIMIT 30";
        using var reader = command.ExecuteReader();
        var result = new List<RecentMedia>();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3)));
        return result;
    }

    public void Install(InstalledAddon addon)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO addons VALUES($id,$name,$url)";
        command.Parameters.AddWithValue("$id", addon.Id);
        command.Parameters.AddWithValue("$name", addon.Name);
        command.Parameters.AddWithValue("$url", addon.ManifestUrl);
        command.ExecuteNonQuery();
    }

    public List<InstalledAddon> Addons()
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,url FROM addons ORDER BY name";
        using var reader = command.ExecuteReader();
        var result = new List<InstalledAddon>();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public void RemoveAddon(string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM addons WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
    public void Dispose() => connection.Dispose();
}
