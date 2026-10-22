using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using BingeX;
internal static class EchoLinkChecks
{
    private sealed class Fixture(Func<HttpRequestMessage,HttpResponseMessage> handle):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(handle(request));}}
    public static async Task Run(string root,string package)
    {
        void Check(bool valid,string message){if(!valid)throw new Exception(message);}
        async Task Reject(Func<Task> action){try{await action();}catch(Exception e)when(e is IOException or InvalidDataException or System.Text.Json.JsonException){return;}throw new Exception("Invalid link or content was accepted.");}
        Check(EchoLinks.Normalize(" https://github.com/owner/music ").AbsoluteUri=="https://api.github.com/repos/owner/music/releases/latest","GitHub project links must resolve release metadata.");
        Check(EchoLinks.Normalize("https://github.com/owner/music/releases/tag/v1").AbsolutePath.EndsWith("/releases/tags/v1"),"Tagged releases must remain pinned.");
        Check(EchoLinks.Normalize("musicCode").Host=="v.gd","Echo shortcodes use the Echo link service.");
        foreach(var invalid in new[]{"http://example.test/music.eapk","file:///a.eapk","https://user:password@example.test/file.eapk"}){var failed=false;try{EchoLinks.Normalize(invalid);}catch(InvalidDataException){failed=true;}Check(failed,"Reject unsafe link formats.");}
        var data=File.ReadAllBytes(package);var hash=Convert.ToHexString(SHA256.HashData(data));
        using var links=new EchoLinks(new Fixture(request=>
        {
            var url=request.RequestUri!;
            if(url.Host=="api.github.com")return new(HttpStatusCode.OK){Content=new StringContent("{\"assets\":[{\"name\":\"music.eapk\",\"browser_download_url\":\"https://files.example/music.eapk\",\"digest\":\"sha256:"+hash+"\"}]}",Encoding.UTF8,"application/json")};
            if(url.AbsolutePath=="/repo.json")return new(HttpStatusCode.OK){Content=new StringContent("[{\"id\":\"fixture\",\"name\":\"Fixture music\",\"updateUrl\":\"https://api.github.com/repos/owner/music/releases\"}]")};
            if(url.AbsolutePath=="/redirect.eapk")return new(HttpStatusCode.Found){Headers={Location=new Uri("http://unsafe.example/file.eapk")}};
            return new(HttpStatusCode.OK){Content=new ByteArrayContent(data)};
        }));
        var choices=await links.Read("https://github.com/owner/music");Check(choices.Count==1&&choices[0].Sha256==hash,"Release fingerprint must survive mapping.");
        var installed=await links.Install(choices[0],root);Check(new EchoPackages(root).List().Any(x=>x.Sha256==installed.Sha256),"Direct release asset installs a valid package.");
        var repo=await links.Read("https://repo.example/repo.json");Check(repo.Single().Name=="Fixture music","Echo repository schema must load.");await links.Install(repo[0],root);
        await Reject(()=>links.Install(new("bad","https://files.example/music.eapk",new string('0',64)),root));
        await Reject(()=>links.Install(new("redirect","https://files.example/redirect.eapk"),root));
        using var oversized=new EchoLinks(new Fixture(_=>{var content=new ByteArrayContent([1]);content.Headers.ContentLength=33*1024*1024;return new(HttpStatusCode.OK){Content=content};}));
        await Reject(()=>oversized.Install(new("large","https://files.example/large.eapk"),root));
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{await links.Install(choices[0],root,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"Cancellation must stop downloads.");
        Check(!Directory.EnumerateFiles(Path.Combine(root,"echo","downloads")).Any(),"Failed and cancelled downloads must leave no temporary files.");
        Console.WriteLine("PASS: Echo repository links, GitHub project/tag/release resolution, direct package download, fingerprints, HTTPS redirects, cancellation and bounded cleanup.");
    }
}
