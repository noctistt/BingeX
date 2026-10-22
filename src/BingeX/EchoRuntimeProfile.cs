using System.Security.Cryptography;
using System.Text.Json;
namespace BingeX;

public sealed record EchoRuntimeProfile(int Version,string Java,string Classes,string Sdk,string Engine,Dictionary<string,string> Files)
{
    public static (EchoRuntime Runtime,string SdkHash) Load(string file,string dataDirectory)
    {
        if(new FileInfo(file).Length>65536)throw new InvalidDataException("Runtime profile is too large.");
        var profile=JsonSerializer.Deserialize<EchoRuntimeProfile>(File.ReadAllText(file),new JsonSerializerOptions{MaxDepth=8})??throw new InvalidDataException();
        if(profile.Version!=1||profile.Files==null||profile.Files.Count is <4 or >256)throw new InvalidDataException("Unsupported music runtime profile.");
        var root=Path.GetDirectoryName(Path.GetFullPath(file))!;
        string Resolve(string path)=>Path.GetFullPath(path,root);
        var fingerprints=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in profile.Files)
        {
            if(entry.Value.Length!=64||!entry.Value.All(Uri.IsHexDigit))throw new InvalidDataException("Missing runtime fingerprint.");
            var path=Resolve(entry.Key);
            using var input=File.OpenRead(path);var hash=Convert.ToHexString(SHA256.HashData(input));
            if(!hash.Equals(entry.Value,StringComparison.OrdinalIgnoreCase)||!fingerprints.TryAdd(path,hash))throw new InvalidDataException("Music runtime integrity check failed.");
        }
        var java=Resolve(profile.Java);var classes=Resolve(profile.Classes);var sdk=Resolve(profile.Sdk);var engine=Resolve(profile.Engine);
        var required=new[]{java,sdk,Path.Combine(classes,"EchoMusicHelper.class"),Path.Combine(engine,"engine-jvm.jar")}
            .Concat(Directory.EnumerateFiles(classes,"EchoMusicHelper*.class")).Concat(Directory.EnumerateFiles(engine,"*.jar"));
        if(required.Any(x=>!fingerprints.ContainsKey(x)))throw new InvalidDataException("Music runtime contains unverified components.");
        return(new(java,classes,sdk,engine,Path.Combine(dataDirectory,"echo","cache")),fingerprints[sdk]);
    }
}
