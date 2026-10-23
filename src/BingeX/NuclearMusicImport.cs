using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Jint;
namespace BingeX;
public record NuclearMusicImported(MusicFreeProvider Provider,string Source);
public record NuclearMusicPackage(string Main,string Name,string Version,string Author,Dictionary<string,string> Files);
public static class NuclearMusicImport
{
    public static NuclearMusicPackage Read(byte[] bytes)
    {
        if(bytes.Length>512*1024)throw new InvalidDataException();
        using var archive=new ZipArchive(new MemoryStream(bytes));if(archive.Entries.Count>100)throw new InvalidDataException();
        var files=new Dictionary<string,string>(StringComparer.Ordinal);long total=0;
        foreach(var entry in archive.Entries){
            var name=entry.FullName;if(name.EndsWith('/'))continue;
            if(name.Contains('\\')||name.Contains(':')||name.StartsWith('/')||name.Split('/').Any(x=>x is ".." or "." or ""))throw new InvalidDataException();
            total+=entry.Length;if(total>512*1024||entry.Length>256*1024)throw new InvalidDataException();
            if(name!="package.json"&&!name.EndsWith(".ts")&&!name.EndsWith(".js"))continue;
            using var reader=new StreamReader(entry.Open(),Encoding.UTF8,true);var content=new StringBuilder();var buffer=new char[4096];int count;while((count=reader.Read(buffer))>0){if(content.Length+count>256*1024)throw new InvalidDataException();content.Append(buffer,0,count);}if(!files.TryAdd(name,content.ToString()))throw new InvalidDataException();
        }
        using var manifest=JsonDocument.Parse(files["package.json"]);var m=manifest.RootElement;
        var main=m.GetProperty("main").GetString()??"";if(main.StartsWith("./"))main=main[2..];
        var displayName=m.GetProperty("nuclear").GetProperty("displayName").GetString()??"";
        if(!files.ContainsKey(main)||displayName.Length is 0 or >120)throw new InvalidDataException();files.Remove("package.json");
        return new(main,displayName,m.GetProperty("version").GetString()??"",m.TryGetProperty("author",out var author)&&author.ValueKind==JsonValueKind.String?author.GetString()??"":"",files);
    }
    public static NuclearMusicImported Compile(NuclearMusicPackage package,CancellationToken token)
    {
        if(package.Files.Count>60||package.Files.Sum(x=>x.Value.Length)>512*1024)throw new InvalidDataException();
        // Larger temporary import budget; playback/search keep the normal worker limits.
        using var compiler=new Engine(o=>o.DisableStringCompilation().LimitMemory(128_000_000).MaxStatements(8_000_000).TimeoutInterval(TimeSpan.FromSeconds(25)).CancellationToken(token));
        using var stream=typeof(NuclearMusicImport).Assembly.GetManifestResourceStream("BingeX.Assets.nuclear-transpiler.js")!;using var reader=new StreamReader(stream);compiler.Execute(reader.ReadToEnd());
        var output=new StringBuilder("/* BingeX Nuclear */\nconst __nuclearFactories={\n");
        foreach(var (name,text) in package.Files){
            token.ThrowIfCancellationRequested();if(name.Length>200)throw new InvalidDataException();
            var code=compiler.Invoke("__nuclearTranspile",text).AsString();
            output.Append(JsonSerializer.Serialize(name)).Append(":function(module,exports,require){\n").Append(code).Append("\n},\n");
        }
        output.Append("};\nconst __nuclearMain=").Append(JsonSerializer.Serialize(package.Main)).Append(";\n");
        output.Append(NuclearMusicRuntime.Adapter);
        output.Append("\nmodule.exports.platform=").Append(JsonSerializer.Serialize(package.Name)).Append(";module.exports.version=").Append(JsonSerializer.Serialize(package.Version)).Append(";module.exports.author=").Append(JsonSerializer.Serialize(package.Author)).Append(';');
        var source=output.ToString();if(source.Length>512*1024)throw new InvalidDataException();
        var inspect=MusicFreeEngine.Run(source,"inspect","{}",token);var provider=JsonSerializer.Deserialize<MusicFreeProvider>(inspect,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException();
        return new(provider,source);
    }
}
