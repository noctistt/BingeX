using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace BingeX;
public record MusicAlbum(string Id,string Name,string Artist,int Songs);
public record ServerMusicTrack(string Id,string Title,string Artist,int Disc,int Track,int Duration,string Format,int SampleRate,int Bits,string Album="");
// Credentials and authenticated addresses are session-only and never exposed in errors or ToString().
public sealed class SubsonicMusic:IDisposable
{
    private readonly Uri server;
    private readonly string username,password;
    private readonly HttpClient http;
    public SubsonicMusic(string address,string username,string password,bool allowHttp=false,HttpMessageHandler? handler=null)
    {
        if(!Uri.TryCreate(address.Trim().TrimEnd('/')+"/",UriKind.Absolute,out var uri) || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0 || (uri.Scheme=="http" && !uri.IsLoopback && !allowHttp))
            throw new ArgumentException("Use an HTTPS server address, or explicitly allow HTTP for your network.");
        if(string.IsNullOrWhiteSpace(username)||password.Length==0)throw new ArgumentException("Enter your server username and password.");
        server=uri;this.username=username;this.password=password;
        http=new(handler??new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(20),MaxResponseContentBufferSize=4194304};
    }
    internal Uri Address(string endpoint,params (string Key,string Value)[] parameters)
    {
        var salt=Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var token=Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password+salt))).ToLowerInvariant();
        var query=new[]{("u",username),("t",token),("s",salt),("v","1.16.1"),("c","BingeX"),("f","json")}.Concat(parameters);
        return new Uri(new Uri(server,"rest/"+endpoint+".view"),"?"+string.Join("&",query.Select(x=>Uri.EscapeDataString(x.Item1)+"="+Uri.EscapeDataString(x.Item2))));
    }
    private async Task<JsonElement> Get(string endpoint,CancellationToken token,params (string Key,string Value)[] parameters)
    {
        try
        {
            var text=await http.GetStringAsync(Address(endpoint,parameters),token);
            using var doc=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=32});
            var response=doc.RootElement.GetProperty("subsonic-response");
            if(Text(response,"status")!="ok")throw new IOException("The music server rejected the request. Check login and permissions.");
            return response.Clone();
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch(IOException){throw;}
        catch{throw new IOException("Could not read the music server. Check its address, login and connection.");}
    }
    public async Task<IReadOnlyList<MusicAlbum>> Albums(string query,int offset,CancellationToken token)
    {
        var response=string.IsNullOrWhiteSpace(query)?await Get("getAlbumList2",token,("type","alphabeticalByName"),("size","100"),("offset",offset.ToString())):await Get("search3",token,("query",query),("albumCount","100"),("albumOffset",offset.ToString()),("artistCount","0"),("songCount","0"));
        var key=string.IsNullOrWhiteSpace(query)?"albumList2":"searchResult3";
        if(!response.TryGetProperty(key,out var result)||!result.TryGetProperty("album",out var albums))return [];
        return albums.EnumerateArray().Take(100).Select(x=>new MusicAlbum(Text(x,"id"),Text(x,"name"),Text(x,"artist"),Number(x,"songCount"))).Where(x=>x.Id.Length>0).ToArray();
    }
    public async Task<IReadOnlyList<ServerMusicTrack>> Tracks(string album,CancellationToken token)
    {
        var response=await Get("getAlbum",token,("id",album));
        if(!response.GetProperty("album").TryGetProperty("song",out var songs))return [];
        return songs.EnumerateArray().Take(10000).Select(x=>new ServerMusicTrack(Text(x,"id"),Text(x,"title"),Text(x,"artist"),Number(x,"discNumber"),Number(x,"track"),Number(x,"duration"),Text(x,"suffix"),Number(x,"samplingRate"),Number(x,"bitDepth"),Text(x,"album"))).Where(x=>x.Id.Length>0).OrderBy(x=>x.Disc).ThenBy(x=>x.Track).ToArray();
    }
    public string Stream(string id)=>Address("stream",("id",id),("format","raw"),("maxBitRate","0")).AbsoluteUri;
    private static string Text(JsonElement item,string key)=>item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
    private static int Number(JsonElement item,string key)=>item.TryGetProperty(key,out var value)&&value.TryGetInt32(out var number)?number:0;
    public void Dispose()=>http.Dispose();
}
