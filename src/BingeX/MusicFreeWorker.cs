using System.Diagnostics;
using System.Text.Json;
namespace BingeX;
public record MusicFreeRequest(string Source,string Operation,string Arguments);
public record MusicFreeProvider(string Name,string Version,string Author,string Sha256="",bool Trusted=false);
public record MusicFreeTrack(string Id,string Title,string Artist,string? Artwork,double Duration,JsonElement Item);
public record MusicFreeCatalog(string Title,JsonElement Item);
public record MusicFreeCatalogGroup(string Title,MusicFreeCatalog[] Catalogs);
public record MusicFreeResults(bool IsEnd,MusicFreeTrack[] Items);
public record MusicFreeSource(string Url,Dictionary<string,string>? Headers);
public static class MusicFreeWorker
{
    internal static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    public static int Run()
    {
        try{
            var chars=new char[1_100_001];var length=0;int read;while(length<chars.Length&&(read=Console.In.Read(chars,length,chars.Length-length))>0)length+=read;
            if(length>=chars.Length)throw new InvalidDataException();
            var request=JsonSerializer.Deserialize<MusicFreeRequest>(new string(chars,0,length),Json)??throw new InvalidDataException();
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(28));
            var output=request.Operation=="compile-nuclear"
                ?JsonSerializer.Serialize(NuclearMusicImport.Compile(JsonSerializer.Deserialize<NuclearMusicPackage>(request.Source,Json)??throw new InvalidDataException(),deadline.Token))
                :MusicFreeEngine.Run(request.Source,request.Operation,request.Arguments,deadline.Token);
            Console.WriteLine(JsonSerializer.Serialize(new{ok=true,data=JsonSerializer.Deserialize<JsonElement>(output)}));return 0;
        }catch{Console.WriteLine("{\"ok\":false}");return 1;}
    }
    public static async Task<T> Call<T>(string source,string operation,object arguments,CancellationToken token)
    {
        var assembly=typeof(MusicFreeWorker).Assembly.Location;
        var host=Path.ChangeExtension(assembly,OperatingSystem.IsWindows()?".exe":null);
        var info=new ProcessStartInfo{UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        if(File.Exists(host))info.FileName=host;else{info.FileName=Environment.ProcessPath!;info.ArgumentList.Add(assembly);}
        info.ArgumentList.Add("--musicfree-worker");
        using var process=Process.Start(info)??throw new InvalidOperationException();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(32));
        using var stop=deadline.Token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch{}});
        var output=ReadBounded(process.StandardOutput,deadline.Token);var errors=ReadBounded(process.StandardError,deadline.Token);
        try{
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new MusicFreeRequest(source,operation,JsonSerializer.Serialize(arguments))).AsMemory(),deadline.Token);process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);var text=await output;await errors;deadline.Token.ThrowIfCancellationRequested();
            using var result=JsonDocument.Parse(text);if(process.ExitCode!=0||!result.RootElement.GetProperty("ok").GetBoolean())throw new InvalidDataException();
            return result.RootElement.GetProperty("data").Deserialize<T>(Json)??throw new InvalidDataException();
        }catch{try{if(!process.HasExited)process.Kill(true);}catch{}throw;}
    }
    private static async Task<string> ReadBounded(StreamReader reader,CancellationToken token)
    {
        var result=new System.Text.StringBuilder();var buffer=new char[4096];int count;
        while((count=await reader.ReadAsync(buffer.AsMemory(),token))>0){if(result.Length+count>600_000)throw new InvalidDataException();result.Append(buffer,0,count);}return result.ToString();
    }
}
