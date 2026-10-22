using LibVLCSharp.Shared;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)=>Run(args).GetAwaiter().GetResult();
    private static async Task<int> Run(string[] args)
    {
// Decode only the synthetic loopback source emitted by tools/echo-probe/EchoProbe.java.
if(args.Length!=1)return 2;
var url=File.ReadAllText(args[0]);
if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Host!="127.0.0.1"||uri.Scheme!="http")return 2;
Console.WriteLine("STAGE: native initialization");Core.Initialize();Console.WriteLine("STAGE: native libraries loaded");
using var engine=new LibVLC("--no-video","--aout=dummy");
Console.WriteLine("STAGE: native engine created");
using var media=new Media(engine,uri);
using var player=new MediaPlayer(engine);
Console.WriteLine("STAGE: opening resolved fixture");if(!player.Play(media))return 1;
Console.WriteLine("STAGE: waiting for audio decoding");
var until=DateTime.UtcNow.AddSeconds(20);var decoded=false;
while(DateTime.UtcNow<until){await Task.Delay(200);if(player.Time>3000&&media.Statistics.DecodedAudio>0){decoded=true;break;}}
Console.WriteLine("STAGE: decoding complete="+decoded);player.Stop();
Console.WriteLine(decoded?"PASS: extension-resolved source decoded audio and advanced beyond three seconds (silent test output).":"FAIL: audio decoding did not complete.");
return decoded?0:1;

    }
}
