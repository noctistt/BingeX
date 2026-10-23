using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace BingeX;
public record MusicFreeResponse(int status,string body);
public static class MusicFreeHttp
{
    public static Uri PublicUri(string value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||!uri.IsDefaultPort||uri.HostNameType==UriHostNameType.Unknown)throw new InvalidDataException();
        if(uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)||(IPAddress.TryParse(uri.Host,out var literal)&&!PublicAddress(literal)))throw new InvalidDataException();
        return uri;
    }
    public static bool PublicAddress(IPAddress address)
    {
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        if(IPAddress.IsLoopback(address))return false;
        var b=address.GetAddressBytes();
        if(b.Length==16)return (b[0]&0xe0)==0x20; // Global unicast only.
        return b[0] is not(0 or 10 or 127)&&b[0]<224&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&b[1]==168)&&!(b[0]==100&&b[1]>=64&&b[1]<=127);
    }
    private static HttpClient Client()=>new(new SocketsHttpHandler{
        AllowAutoRedirect=false,UseCookies=false,UseProxy=false,
        ConnectCallback=async(context,token)=>{
            var addresses=await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host,token);
            if(addresses.Length==0||addresses.Any(x=>!PublicAddress(x)))throw new InvalidDataException();
            foreach(var address in addresses){var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp);try{await socket.ConnectAsync(address,context.DnsEndPoint.Port,token);return new NetworkStream(socket,true);}catch{socket.Dispose();}}
            throw new HttpRequestException();
        }}){Timeout=TimeSpan.FromSeconds(12)};
    public static string Send(string config,CancellationToken token,bool ensureSuccess=true,int maximumBytes=2*1024*1024)
        =>JsonSerializer.Serialize(SendResponse(config,token,ensureSuccess,maximumBytes));
    public static MusicFreeResponse SendResponse(string config,CancellationToken token,bool ensureSuccess=true,int maximumBytes=2*1024*1024)
    {
        using var parsed=JsonDocument.Parse(config);var c=parsed.RootElement;var uri=PublicUri(c.GetProperty("url").GetString()!);
        if(c.TryGetProperty("params",out var parameters)&&parameters.ValueKind==JsonValueKind.Object){var pairs=parameters.EnumerateObject().Where(x=>x.Value.ValueKind is not(JsonValueKind.Null or JsonValueKind.Undefined)).Select(x=>Uri.EscapeDataString(x.Name)+"="+Uri.EscapeDataString(x.Value.ValueKind==JsonValueKind.String?x.Value.GetString()!:x.Value.GetRawText()));uri=new UriBuilder(uri){Query=(uri.Query.Length>0?uri.Query[1..]+"&":"")+string.Join("&",pairs)}.Uri;}
        var method=c.TryGetProperty("method",out var m)?m.GetString()?.ToUpperInvariant():"GET";
        if(method is not("GET" or "POST"))throw new InvalidDataException();
        using var client=Client();
        for(var hop=0;hop<5;hop++){
            using var request=new HttpRequestMessage(new HttpMethod(method),uri);
            if(c.TryGetProperty("headers",out var headers)&&headers.ValueKind==JsonValueKind.Object)foreach(var h in headers.EnumerateObject()){
                if(h.Name.Equals("User-Agent",StringComparison.OrdinalIgnoreCase)||h.Name.Equals("Origin",StringComparison.OrdinalIgnoreCase)||h.Name.Equals("Referer",StringComparison.OrdinalIgnoreCase)||h.Name.Equals("Accept",StringComparison.OrdinalIgnoreCase))request.Headers.TryAddWithoutValidation(h.Name,h.Value.ToString());
            }
            if(method=="POST"&&c.TryGetProperty("data",out var data))request.Content=new StringContent(data.ValueKind==JsonValueKind.String?data.GetString()!:data.GetRawText(),Encoding.UTF8,"application/json");
            using var response=client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).GetAwaiter().GetResult();
            if((int)response.StatusCode is >=300 and <400&&response.Headers.Location is {} next){uri=PublicUri(new Uri(uri,next).AbsoluteUri);continue;}
            if(ensureSuccess)response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>maximumBytes)throw new InvalidDataException();
            using var stream=response.Content.ReadAsStream(token);using var body=new MemoryStream();var buffer=new byte[8192];int read;
            while((read=stream.Read(buffer))>0){token.ThrowIfCancellationRequested();if(body.Length+read>maximumBytes)throw new InvalidDataException();body.Write(buffer,0,read);}
            return new((int)response.StatusCode,Encoding.UTF8.GetString(body.GetBuffer(),0,(int)body.Length));
        }
        throw new HttpRequestException();
    }
    public static string Download(string url,CancellationToken token)
    {
        var result=Send(JsonSerializer.Serialize(new{url,method="GET"}),token);using var json=JsonDocument.Parse(result);return json.RootElement.GetProperty("body").GetString()!;
    }
    public static byte[] DownloadArchive(string value,CancellationToken token)
    {
        var uri=PublicUri(value);using var client=Client();
        for(var hop=0;hop<5;hop++){
            using var response=client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token).GetAwaiter().GetResult();
            if((int)response.StatusCode is >=300 and <400&&response.Headers.Location is {} next){uri=PublicUri(new Uri(uri,next).AbsoluteUri);continue;}
            response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>512*1024)throw new InvalidDataException();
            using var input=response.Content.ReadAsStream(token);using var output=new MemoryStream();var bytes=new byte[8192];int read;
            while((read=input.Read(bytes))>0){token.ThrowIfCancellationRequested();if(output.Length+read>512*1024)throw new InvalidDataException();output.Write(bytes,0,read);}return output.ToArray();
        }
        throw new HttpRequestException();
    }
}
