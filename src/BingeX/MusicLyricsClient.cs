using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace BingeX;
public record MusicLyricMatch(long Id,string TrackName,string ArtistName,string AlbumName,double Duration,bool Instrumental,string? PlainLyrics,string? SyncedLyrics)
{
    public MusicLyrics Lyrics=>MusicLyrics.Parse(!string.IsNullOrWhiteSpace(SyncedLyrics)?SyncedLyrics:PlainLyrics??"");
}
public sealed class MusicLyricsClient:IDisposable
{
    private readonly HttpClient http;
    public MusicLyricsClient(HttpMessageHandler? handler=null)
    {
        http=handler==null?new(new HttpClientHandler{AllowAutoRedirect=false}):new(handler);
        http.Timeout=TimeSpan.FromSeconds(12);http.DefaultRequestHeaders.UserAgent.ParseAdd("BingeX/0.14.16");
    }
    public async Task<IReadOnlyList<MusicLyricMatch>> Search(string title,string artist,string album,double duration,CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(15));token=deadline.Token;
        title=title.Trim();artist=artist.Trim();album=album.Trim();if(title.Length is 0 or >200||artist.Length>200||album.Length>200)throw new InvalidDataException();
        var args=new List<string>{"track_name="+Uri.EscapeDataString(title)};if(artist.Length>0)args.Add("artist_name="+Uri.EscapeDataString(artist));
        var path="https://lrclib.net/api/search?"+string.Join("&",args);
        using var response=await http.GetAsync(path,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>2_000_000)throw new InvalidDataException();
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var buffer=new MemoryStream();var chunk=new byte[8192];int count;
        while((count=await stream.ReadAsync(chunk,token))>0){if(buffer.Length+count>2_000_000)throw new InvalidDataException();buffer.Write(chunk,0,count);}
        var results=JsonSerializer.Deserialize<MusicLyricMatch[]>(buffer.ToArray(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
        return results.Where(x=>x.TrackName!=null&&x.ArtistName!=null&&(x.Instrumental||!string.IsNullOrWhiteSpace(x.SyncedLyrics)||!string.IsNullOrWhiteSpace(x.PlainLyrics)))
            .OrderByDescending(x=>Exact(x,title,artist,album,duration)).ThenBy(x=>Math.Abs(x.Duration-duration)).Take(20).ToArray();
    }
    private static string Normalize(string text)=>Regex.Replace(text.ToLowerInvariant(),@"[^\p{L}\p{N}]","");
    public static bool Exact(MusicLyricMatch match,string title,string artist,string album,double duration)=>duration>0&&artist.Length>0&&Normalize(match.TrackName)==Normalize(title)&&Normalize(match.ArtistName)==Normalize(artist)&&Math.Abs(match.Duration-duration)<=2&&(album.Length==0||Normalize(match.AlbumName??"")==Normalize(album));
    public void Dispose()=>http.Dispose();
}
