using System.Security.Cryptography;
using System.Text.Json;
namespace BingeX;

public sealed record EchoRuntimeProfile(int Version,string Java,string Classes,string Sdk,string Engine,Dictionary<string,string> Files, EchoCompatibilityProfile? Compatibility=null)
{
    public static (EchoRuntime Runtime,string SdkHash,bool HasCompatibility) Load(string file,string dataDirectory,bool useCompatibility=false)
    {
        if(new FileInfo(file).Length>65536)throw new InvalidDataException("Runtime profile is too large.");
        var profile=JsonSerializer.Deserialize<EchoRuntimeProfile>(File.ReadAllText(file),new JsonSerializerOptions{MaxDepth=8})??throw new InvalidDataException();
        if(profile.Version is not (1 or 2)||profile.Files==null||profile.Files.Count is <4 or >256)throw new InvalidDataException("Unsupported music runtime profile.");
        if((profile.Version==1&&profile.Compatibility!=null)||(profile.Version==2&&profile.Compatibility==null))throw new InvalidDataException("Invalid compatibility profile version.");
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
        string[]? compatibility=null;
        if(profile.Compatibility is {} extra)
        {
            if(extra.Jars is not {Count:>0 and <=32})throw new InvalidDataException("Invalid compatibility classpath.");
            compatibility=extra.Jars.Select(Resolve).ToArray();
            if(compatibility.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=compatibility.Length||compatibility.Any(x=>!x.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)||!fingerprints.ContainsKey(x)))throw new InvalidDataException("Unverified compatibility components.");
            using var jar=System.IO.Compression.ZipFile.OpenRead(compatibility[0]);
            if(jar.GetEntry("BingeXEchoCompatibility.class")==null)throw new InvalidDataException("Compatibility entry point is missing.");
        }
        if(useCompatibility&&compatibility==null)throw new InvalidDataException("Choose a prepared enhanced compatibility runtime first.");
        return(new(java,EchoHelperCode.Prepare(dataDirectory)??classes,sdk,engine,Path.Combine(dataDirectory,"echo","cache"),useCompatibility?compatibility:null),fingerprints[sdk],compatibility!=null);
    }
}

public sealed record EchoCompatibilityProfile(List<string> Jars);
