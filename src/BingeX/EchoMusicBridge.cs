using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BingeX;

public sealed record EchoRuntime(string JavaExecutable,string ClassesDirectory,string SdkJar,string EngineDirectory,string CacheDirectory);
public sealed record EchoExtension(string Path,string Sha256,string EntryClass,string SdkSha256);
public sealed record EchoFeed(string Feed,IReadOnlyList<EchoTab> Tabs);
public sealed record EchoTab(string Id,string Title);
public sealed record EchoItem(string Kind,string? Handle,string Title,string? Feed,IReadOnlyList<EchoItem>? Items);
public sealed record EchoPage(IReadOnlyList<EchoItem> Items,string Next);
public sealed record EchoVariant(string Handle,string Title,int Quality);
public sealed record EchoSource(string Url,IReadOnlyDictionary<string,string> Headers,string Type,bool Live);

/// <summary>Development-only Echo transport. No runtime discovery, downloads or provider presets.</summary>
public sealed class EchoMusicBridge:IAsyncDisposable
{
    private const int MessageLimit=1048576;
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true};
    private readonly Process process;
    private readonly SemaphoreSlim requests=new(1);
    private readonly Task stderr;
    private int sequence,stopped;
    private bool resourcesDisposed;
    public IReadOnlyList<string> Capabilities {get;private set;}=[];
    public bool IsStopped=>Volatile.Read(ref stopped)!=0;
    public EchoMusicBridge(EchoRuntime runtime)
    {
        foreach(var file in new[]{runtime.JavaExecutable,runtime.SdkJar,Path.Combine(runtime.ClassesDirectory,"EchoMusicHelper.class"),Path.Combine(runtime.EngineDirectory,"engine-jvm.jar")})
            if(!File.Exists(file))throw new FileNotFoundException("Echo helper runtime is incomplete.");
        Directory.CreateDirectory(runtime.CacheDirectory);
        var start=new ProcessStartInfo(Path.GetFullPath(runtime.JavaExecutable))
        {
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
        };
        var classpath=string.Join(Path.PathSeparator,Path.GetFullPath(runtime.ClassesDirectory),Path.GetFullPath(runtime.SdkJar),Path.Combine(Path.GetFullPath(runtime.EngineDirectory),"*"));
        foreach(var argument in new[]{"-Xmx1024m","-cp",classpath,"EchoMusicHelper",Path.GetFullPath(runtime.CacheDirectory)})start.ArgumentList.Add(argument);
        process=Process.Start(start)??throw new IOException("Echo helper did not start.");
        stderr=Task.Run(async()=>{var buffer=new char[4096];try{while(await process.StandardError.ReadAsync(buffer)>0){}}catch(ObjectDisposedException){}catch(IOException){}});
    }
    private async Task<string> ReadLine(CancellationToken token)
    {
        var line=new StringBuilder();var character=new char[1];
        while(await process.StandardOutput.ReadAsync(character.AsMemory(),token)>0)
        {
            if(character[0]=='\n')return line.ToString();if(character[0]!='\r')line.Append(character[0]);
            if(line.Length>MessageLimit)throw new IOException("Echo helper response exceeded the limit.");
        }
        throw new IOException("Echo helper exited.");
    }
    public async Task<JsonElement> CallAsync(string method,object parameters,CancellationToken token=default,TimeSpan? timeout=null)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(timeout??TimeSpan.FromSeconds(45));
        await requests.WaitAsync(deadline.Token);
        try
        {
            if(IsStopped)throw new ObjectDisposedException(nameof(EchoMusicBridge));
            var id=++sequence;var message=JsonSerializer.Serialize(new{id,method,@params=parameters});
            if(message.Length>MessageLimit)throw new ArgumentException("Echo request exceeded the limit.");
            await process.StandardInput.WriteLineAsync(message.AsMemory(),deadline.Token);await process.StandardInput.FlushAsync(deadline.Token);
            using var response=JsonDocument.Parse(await ReadLine(deadline.Token),new JsonDocumentOptions{MaxDepth=32});var root=response.RootElement;
            if(!root.TryGetProperty("id",out var responseId)||!responseId.TryGetInt32(out var received)||received!=id)throw new IOException("Echo helper protocol mismatch.");
            if(root.TryGetProperty("error",out var error))throw new NotSupportedException(error.TryGetProperty("code",out var code)&&code.GetString()=="unsupported"?"This extension requires unsupported Echo features.":"The Echo extension request failed.");
            return root.GetProperty("result").Clone();
        }
        catch{Stop();throw;}
        finally{requests.Release();}
    }
    public async Task InitializeAsync(EchoExtension extension,CancellationToken token=default)
    {
        // Verify before handing a package to Java; helper independently checks both hashes as well.
        try
        {
            foreach(var hash in new[]{extension.Sha256,extension.SdkSha256})if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new ArgumentException("Expected SHA-256 fingerprints are required.");
            await using var source=File.OpenRead(extension.Path);var actual=Convert.ToHexString(await SHA256.HashDataAsync(source,token));
            if(!actual.Equals(extension.Sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Echo extension integrity check failed.");
            var hello=await CallAsync("hello",new{},token);if(hello.GetProperty("protocol").GetInt32()!=1)throw new NotSupportedException("Echo helper version is unsupported.");
            var loaded=await CallAsync("load",new{path=Path.GetFullPath(extension.Path),sha256=extension.Sha256,entryClass=extension.EntryClass,sdkSha256=extension.SdkSha256},token,TimeSpan.FromSeconds(90));
            Capabilities=loaded.GetProperty("capabilities").Deserialize<string[]>(JsonOptions)??[];
        }
        catch{Stop();throw;}
    }
    public async Task<EchoFeed> SearchAsync(string query,CancellationToken token=default)=>
        (await CallAsync("search",new{query},token)).Deserialize<EchoFeed>(JsonOptions)??throw new IOException("Invalid Echo feed.");
    public async Task<EchoPage> PageAsync(string feed,string tab,string? next=null,CancellationToken token=default)
    {
        object request=string.IsNullOrEmpty(next)?new{feed,tab}:(object)new{next};
        return (await CallAsync("page",request,token)).Deserialize<EchoPage>(JsonOptions)??throw new IOException("Invalid Echo page.");
    }
    public async Task<IReadOnlyList<EchoVariant>> VariantsAsync(string track,CancellationToken token=default)=>
        (await CallAsync("variants",new{handle=track},token)).Deserialize<EchoVariant[]>(JsonOptions)??[];
    public async Task<IReadOnlyList<EchoSource>> ResolveAsync(string variant,CancellationToken token=default)
    {
        var sources=(await CallAsync("resolve",new{handle=variant},token)).Deserialize<EchoSource[]>(JsonOptions)??[];
        foreach(var source in sources)
            if(source.Headers==null || !Uri.TryCreate(source.Url,UriKind.Absolute,out var address)||address.Scheme is not("https" or "http")||address.UserInfo.Length>0||!HeaderStreamRelay.ValidHeaders(source.Headers))
            {Stop();throw new IOException("Echo returned an invalid source.");}
        return sources;
    }
    private void Stop()
    {
        if(Interlocked.Exchange(ref stopped,1)!=0)return;
        try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
    }
    public async ValueTask DisposeAsync()
    {
        // Terminate first so an in-flight native/network extension call cannot block disposal.
        Stop();await requests.WaitAsync();
        try{if(resourcesDisposed)return;await process.WaitForExitAsync();await stderr;process.Dispose();resourcesDisposed=true;}
        finally{requests.Release();}
    }
}
