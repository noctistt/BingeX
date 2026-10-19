using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace BingeX;

public record RecentMedia(string Path, string Title, long Position, long Duration);
public record InstalledAddon(string Id, string Name, string ManifestUrl);

public sealed partial class LibraryStore : IDisposable
{
    private readonly SqliteConnection connection;
    public string DataDirectory { get; }
    public LibraryStore(string? dataDirectory = null)
    {
        var directory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BingeX");
        Directory.CreateDirectory(directory);
        DataDirectory = directory;
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "library.db") }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS history(path TEXT PRIMARY KEY, title TEXT NOT NULL, position INTEGER NOT NULL, duration INTEGER NOT NULL, updated TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS addons(id TEXT PRIMARY KEY, name TEXT NOT NULL, url TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS media_context(path TEXT PRIMARY KEY, context TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS preferences(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS media_audio(path TEXT PRIMARY KEY, track_id INTEGER NOT NULL, track_name TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS library_folders(path TEXT PRIMARY KEY COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS library_files(folder TEXT COLLATE NOCASE NOT NULL,path TEXT COLLATE NOCASE NOT NULL,title TEXT NOT NULL,relative_path TEXT NOT NULL,size INTEGER NOT NULL,modified INTEGER NOT NULL,PRIMARY KEY(folder,path));
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

    public void SaveContext(MediaContext context)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO media_context VALUES($path,$context)";
        command.Parameters.AddWithValue("$path", context.HistoryKey);
        command.Parameters.AddWithValue("$context", JsonSerializer.Serialize(context));
        command.ExecuteNonQuery();
    }
    public RecentMedia? FindHistory(string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path,title,position,duration FROM history WHERE path=$path";
        command.Parameters.AddWithValue("$path", key);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3)) : null;
    }
    public MediaContext? Context(string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT context FROM media_context WHERE path=$path";
        command.Parameters.AddWithValue("$path", key);
        var json = command.ExecuteScalar() as string;
        try { return json == null ? null : JsonSerializer.Deserialize<MediaContext>(json); }
        catch (JsonException) { return null; }
    }
    public string ThumbnailPath(string path)
    {
        var directory = Path.Combine(DataDirectory, "thumbnails");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path))) + ".png");
    }

    public AudioTrackSelection? AudioSelection(string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT track_id,track_name FROM media_audio WHERE path=$path";
        command.Parameters.AddWithValue("$path", key);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new(reader.GetInt32(0), reader.GetString(1)) : null;
    }
    public void SaveAudioSelection(string key, AudioTrackSelection selection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO media_audio VALUES($path,$id,$name)";
        command.Parameters.AddWithValue("$path", key);
        command.Parameters.AddWithValue("$id", selection.Id);
        command.Parameters.AddWithValue("$name", selection.Name);
        command.ExecuteNonQuery();
    }

    public PlayerPreferences Preferences()
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM preferences WHERE key='player'";
        try
        {
            var preferences = JsonSerializer.Deserialize<PlayerPreferences>(command.ExecuteScalar() as string ?? "{}");
            return preferences == null ? new() : preferences with
            {
                Volume = Math.Clamp(preferences.Volume, 0, 100),
                SubtitleSize = new[] { 20, 28, 36, 44 }.Contains(preferences.SubtitleSize) ? preferences.SubtitleSize : 28,
                SubtitleColor = new[] { "White", "Yellow", "Mint" }.Contains(preferences.SubtitleColor) ? preferences.SubtitleColor : "White"
            };
        }
        catch (JsonException) { return new(); }
    }
    public void SavePreferences(PlayerPreferences preferences)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO preferences VALUES('player',$value)";
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(preferences));
        command.ExecuteNonQuery();
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
