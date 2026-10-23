using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace BingeX;
// One user-selected provider. Source is retained; search results and signed media URLs are session-only.
public sealed class MusicFreeProviderStore(string directory)
{
    private string Folder=>Path.Combine(directory,"musicfree");
    private string FilePath=>Path.Combine(Folder,"provider.json");
    public (MusicFreeProvider Provider,string Source)? Read()
    {
        if(!File.Exists(FilePath))return null;
        var json=File.ReadAllText(FilePath);if(json.Length>600_000)throw new InvalidDataException();
        var saved=JsonSerializer.Deserialize<Saved>(json)??throw new InvalidDataException();
        if(saved.Provider.Sha256!=Hash(saved.Source))throw new InvalidDataException();return(saved.Provider,saved.Source);
    }
    public void Save(MusicFreeProvider provider,string source)
    {
        if(source.Length>512*1024)throw new InvalidDataException();Directory.CreateDirectory(Folder);
        var temporary=Path.Combine(Folder,Guid.NewGuid()+".tmp");
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(new Saved(provider with{Sha256=Hash(source)},source)));File.Move(temporary,FilePath,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void Remove(){if(File.Exists(FilePath))File.Delete(FilePath);}
    private static string Hash(string source)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    private record Saved(MusicFreeProvider Provider,string Source);
}
