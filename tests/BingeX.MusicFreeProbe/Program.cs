using System.Text.Json;
using LibVLCSharp.Shared;
// Research-only stdin transport: do not persist source URLs or native diagnostic logs.
internal static class Program
{
    private sealed record Source(string Url,Dictionary<string,string>? Headers);
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.FirstOrDefault()=="--lyrics-live")return MusicLyricsChecks.Live();
        if(args.FirstOrDefault()=="--lyrics-checks")return MusicLyricsChecks.Run();
        if(args.FirstOrDefault()=="--host-checks")return HostChecks.Run();
        if(args.FirstOrDefault()=="--ui-live")return MusicFreeUiChecks.Run();
        if(args.Length>0)return EmbeddedChecks.Run(args);
        var stage="input";var passed=new List<string>();
        try
        {
            var sources=JsonSerializer.Deserialize<Source[]>(Console.In.ReadToEnd(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
            if(sources.Length!=2||sources.Any(x=>!Uri.TryCreate(x.Url,UriKind.Absolute,out var u)||u.Scheme!="https"))throw new InvalidDataException();
            stage="native_initialization";Core.Initialize();
            using var engine=new LibVLC("--no-video","--aout=dummy","--quiet");
            engine.Log+=(_,_)=>{};
            using var player=new MediaPlayer(engine);
            Media Open(Source s)
            {
                var m=new Media(engine,new Uri(s.Url));
                if(s.Headers?.Count>0)throw new NotSupportedException("Header relay must be verified before testing header-dependent sources.");
                return m;
            }
            void Wait(Func<bool> ready,int seconds=20)
            {
                var end=DateTime.UtcNow.AddSeconds(seconds);
                while(!ready()){if(DateTime.UtcNow>end||player.State==VLCState.Error)throw new TimeoutException();Thread.Sleep(100);}
            }
            void Play(Media m)
            {
                if(!player.Play(m))throw new InvalidOperationException();
                Wait(()=>player.Time>3000&&m.Statistics.DecodedAudio>0);
            }
            stage="first_track";using var first=Open(sources[0]);Play(first);passed.Add(stage);
            var track=first.Tracks.FirstOrDefault(x=>x.TrackType==TrackType.Audio);
            var codec=track.Codec;var rate=track.Data.Audio.Rate;var channels=track.Data.Audio.Channels;
            stage="seek_forward";Wait(()=>player.IsSeekable);player.Time=15000;Wait(()=>player.Time>=15000&&player.Time<23000);var t=player.Time;Wait(()=>player.Time>t+500);passed.Add(stage);
            stage="seek_backward";player.Time=2000;Wait(()=>player.Time>=2000&&player.Time<7000);t=player.Time;Wait(()=>player.Time>t+500);passed.Add(stage);
            stage="pause_resume";player.SetPause(true);Wait(()=>player.State==VLCState.Paused,5);t=player.Time;Thread.Sleep(700);if(Math.Abs(player.Time-t)>300)throw new InvalidOperationException();player.SetPause(false);Wait(()=>player.Time>t+500);passed.Add(stage);
            stage="next_track";player.Stop();using var second=Open(sources[1]);Play(second);passed.Add(stage);
            stage="failed_source";player.Stop();using var broken=new Media(engine,new Uri("http://127.0.0.1:1/bingex-test-unavailable"));player.Play(broken);
            var end=DateTime.UtcNow.AddSeconds(8);while(DateTime.UtcNow<end&&player.State is not(VLCState.Error or VLCState.Ended)){Thread.Sleep(100);}
            if(player.State is not(VLCState.Error or VLCState.Ended))throw new TimeoutException();passed.Add(stage);
            stage="recovery";player.Stop();using var recovery=Open(sources[0]);Play(recovery);passed.Add(stage);player.Stop();
            Console.WriteLine(JsonSerializer.Serialize(new{result="pass",passed,codecFourCC=codec,sampleRate=rate,channels,output="muted dummy output",scope="native engine; no BingeX UI integration"}));return 0;
        }
        catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{result="failed",stage,passed,error=e.GetType().Name}));return 1;}
    }
}
