using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace BingeX;
public record MusicFreeSaved(MusicFreeProvider Provider,string Source);
// Keeps user-installed sources and selected provider; browsing and playback URLs remain session-only.
public sealed class MusicFreeProviderStore(string directory)
{
    private string Folder=>Path.Combine(directory,"musicfree");
    private string FilePath=>Path.Combine(Folder,"provider.json");
    private record Collection(MusicFreeSaved[] Providers,string? Selected);
    private Collection Load()
    {
        if(!File.Exists(FilePath))return new([],null);
        if(new FileInfo(FilePath).Length>8_000_000)throw new InvalidDataException();
        var text=File.ReadAllText(FilePath);using var json=JsonDocument.Parse(text);
        var result=json.RootElement.TryGetProperty("Providers",out _)?JsonSerializer.Deserialize<Collection>(text):JsonSerializer.Deserialize<MusicFreeSaved>(text) is {} legacy?new Collection([legacy],legacy.Provider.Name):null;
        if(result==null||result.Providers.Length>12||result.Providers.Any(x=>x.Source.Length>512*1024||x.Provider.Sha256!=Hash(x.Source)))throw new InvalidDataException();
        if(result.Providers.Select(x=>x.Provider.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=result.Providers.Length)throw new InvalidDataException();return result;
    }
    public IReadOnlyList<MusicFreeSaved> ReadAll()=>Load().Providers;
    public (MusicFreeProvider Provider,string Source)? Read()
    {
        var collection=Load();var saved=collection.Providers.FirstOrDefault(x=>x.Provider.Name==collection.Selected)??collection.Providers.FirstOrDefault();return saved==null?null:(saved.Provider,saved.Source);
    }
    private void Write(Collection collection)
    {
        Directory.CreateDirectory(Folder);var text=JsonSerializer.Serialize(collection);if(text.Length>6_000_000)throw new InvalidDataException();
        var temporary=Path.Combine(Folder,Guid.NewGuid()+".tmp");
        try{File.WriteAllText(temporary,text);File.Move(temporary,FilePath,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void Save(MusicFreeProvider provider,string source)
    {
        if(source.Length>512*1024)throw new InvalidDataException();var collection=Load();
        var others=collection.Providers.Where(x=>!x.Provider.Name.Equals(provider.Name,StringComparison.OrdinalIgnoreCase)).ToList();if(others.Count>=12)throw new InvalidDataException();
        others.Add(new(provider with{Sha256=Hash(source)},source));Write(new(others.ToArray(),provider.Name));
    }
    public void Select(string name){var collection=Load();if(!collection.Providers.Any(x=>x.Provider.Name==name))throw new InvalidDataException();Write(collection with{Selected=name});}
    public void Remove(string? name=null)
    {
        var collection=Load();name??=collection.Selected??collection.Providers.FirstOrDefault()?.Provider.Name;
        var remaining=collection.Providers.Where(x=>x.Provider.Name!=name).ToArray();
        if(remaining.Length==0){if(File.Exists(FilePath))File.Delete(FilePath);}else Write(new(remaining,collection.Selected==name?remaining[0].Provider.Name:collection.Selected));
    }
    private static string Hash(string source)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
