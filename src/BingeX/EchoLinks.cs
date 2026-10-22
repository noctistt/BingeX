using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace BingeX;

public sealed record EchoLinkChoice(string Name,string Url,string? Sha256=null);
public sealed class EchoInstallException(string message):IOException(message);

/// <summary>Explicit user-selected links only. Download never activates extension code.</summary>
public sealed class EchoLinks:IDisposable
{
    private readonly HttpClient http;
    public EchoLinks(HttpMessageHandler? handler=null)
    {
        http=new(handler??new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false});http.Timeout=TimeSpan.FromSeconds(90);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BingeX/0.14.5");
    }
    public static Uri Normalize(string value)
    {
        value=value.Trim();if(Regex.IsMatch(value,@"\A[A-Za-z0-9_-]{1,64}\z"))value="https://v.gd/"+value;
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||value.Length>8192||uri.Fragment.Length>0)throw new InvalidDataException("Use an HTTPS music extension or repository link.");
        var parts=uri.AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries);
        if(uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)&&parts.Length>=2)
        {
            if(parts.Length>=5&&parts[2]=="blob"&&uri.AbsolutePath.EndsWith(".eapk",StringComparison.OrdinalIgnoreCase))return new Uri("https://raw.githubusercontent.com/"+string.Join('/',parts.Take(2).Concat(parts.Skip(3))));
            if(parts.Length==2||parts.Length==3&&parts[2]=="releases"||parts.Length==4&&parts[2]=="releases"&&parts[3]=="latest")return new Uri($"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases/latest");
            if(parts.Length>=5&&parts[2]=="releases"&&parts[3]=="tag")return new Uri($"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases/tags/{string.Join('/',parts.Skip(4))}");
        }
        if(uri.Host.Equals("api.github.com",StringComparison.OrdinalIgnoreCase)&&parts.Length==4&&parts[0]=="repos"&&parts[3]=="releases")return new Uri($"https://api.github.com/repos/{parts[1]}/{parts[2]}/releases/latest");
        return uri;
    }
    private async Task<HttpResponseMessage> Open(Uri uri,CancellationToken token)
    {
        for(int hop=0;hop<6;hop++)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);
            var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if(response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location=response.Headers.Location;response.Dispose();if(location==null)throw new IOException("Missing redirect target.");
                uri=Normalize(new Uri(uri,location).AbsoluteUri);continue;
            }
            if(!response.IsSuccessStatusCode)
            {
                var code=(int)response.StatusCode;bool limited=code==429||code==403&&response.Headers.TryGetValues("X-RateLimit-Remaining",out var values)&&values.Contains("0");response.Dispose();
                throw new EchoInstallException(limited?"The extension host is rate limiting requests. Try again later.":code==404?"This extension release or file is no longer available (404).":$"The extension host refused the request (HTTP {code}).");
            }return response;
        }
        throw new IOException("Too many extension link redirects.");
    }
    private static async Task Copy(HttpContent content,Stream target,int limit,CancellationToken token)
    {
        if(content.Headers.ContentLength>limit)throw new InvalidDataException("Extension response is too large.");
        using var stream=await content.ReadAsStreamAsync(token);var block=new byte[65536];int total=0,count;
        while((count=await stream.ReadAsync(block,token))>0){total+=count;if(total>limit)throw new InvalidDataException("Extension response is too large.");await target.WriteAsync(block.AsMemory(0,count),token);}
    }
    public async Task<IReadOnlyList<EchoLinkChoice>> Read(string value,CancellationToken token=default)
    {
        var uri=Normalize(value);
        if(uri.AbsolutePath.EndsWith(".eapk",StringComparison.OrdinalIgnoreCase))return [new("Extension file",uri.AbsoluteUri)];
        using var response=await Open(uri,token);using var memory=new MemoryStream();await Copy(response.Content,memory,2*1024*1024,token);
        using var document=JsonDocument.Parse(memory.ToArray(),new JsonDocumentOptions{MaxDepth=16});var root=document.RootElement;var choices=new List<EchoLinkChoice>();
        if(root.ValueKind==JsonValueKind.Array)
        {
            if(root.GetArrayLength()>256)throw new InvalidDataException("Music repository is too large.");
            foreach(var row in root.EnumerateArray())
            {
                var type=Text(row,"type");if(type.Length>0&&!type.Equals("music",StringComparison.OrdinalIgnoreCase))continue;
                string name=Text(row,"name"),link=Text(row,"updateUrl");
                if(name.Length is 0 or >512||link.Length==0)throw new InvalidDataException("Unsupported Echo repository.");
                choices.Add(new(name,Normalize(link).AbsoluteUri));
            }
        }
        else if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("assets",out var assets)&&assets.ValueKind==JsonValueKind.Array)
        {
            if(assets.GetArrayLength()>256)throw new InvalidDataException("Release has too many assets.");
            foreach(var asset in assets.EnumerateArray())
            {
                var name=Text(asset,"name");if(!name.EndsWith(".eapk",StringComparison.OrdinalIgnoreCase))continue;
                var digest=Text(asset,"digest");string? hash=digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)?digest[7..]:null;
                choices.Add(new(name,Normalize(Text(asset,"browser_download_url")).AbsoluteUri,hash));
            }
        }
        else throw new InvalidDataException("Use an Echo repository, GitHub release, or direct .eapk link.");
        if(choices.Count==0)throw new EchoInstallException("No supported music extension files were found in this release or repository.");return choices;
    }
    private static string Text(JsonElement row,string key)=>row.ValueKind==JsonValueKind.Object&&row.TryGetProperty(key,out var item)&&item.ValueKind==JsonValueKind.String?item.GetString()??"":"";
    public async Task<EchoPackage> Install(EchoLinkChoice choice,string dataDirectory,CancellationToken token=default)
    {
        var uri=Normalize(choice.Url);
        if(!uri.AbsolutePath.EndsWith(".eapk",StringComparison.OrdinalIgnoreCase))
        {
            var assets=await Read(uri.AbsoluteUri,token);
            if(assets.Count!=1||!Normalize(assets[0].Url).AbsolutePath.EndsWith(".eapk",StringComparison.OrdinalIgnoreCase))throw new EchoInstallException("This release has multiple files or an unsupported format. Paste the direct .eapk download link instead.");
            choice=assets[0];uri=Normalize(choice.Url);
        }
        var directory=Path.Combine(dataDirectory,"echo","downloads");Directory.CreateDirectory(directory);var temporary=Path.Combine(directory,Guid.NewGuid()+".download");
        try
        {
            using(var response=await Open(uri,token))await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))await Copy(response.Content,output,32*1024*1024,token);
            if(choice.Sha256 is {} expected)
            {
                if(expected.Length!=64||!expected.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid release fingerprint.");
                await using var input=File.OpenRead(temporary);if(!Convert.ToHexString(await SHA256.HashDataAsync(input,token)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new EchoInstallException("The download did not match its published checksum. Try downloading again.");
            }
            token.ThrowIfCancellationRequested();try{return await Task.Run(()=>new EchoPackages(dataDirectory).Import(temporary),token);}catch(InvalidDataException){throw new EchoInstallException("The downloaded file is not a music extension format supported by BingeX.");}
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void Dispose()=>http.Dispose();
}
