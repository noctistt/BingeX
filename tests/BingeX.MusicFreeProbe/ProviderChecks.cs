using System.Security.Cryptography;
using System.Text.Json;
using BingeX;
using LibVLCSharp.Shared;

// Explicit research command: only reviewed author plugins, no user profile or saved URLs.
internal static class ProviderChecks
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
    public static int Run(string directory,string report)
    {
        var rows=new List<object>();
        foreach(var name in new[]{"youtube","udio","bilibili","kuaishou","maoerfm","yinyuetai"}){
            var source=File.ReadAllText(Path.Combine(directory,name,"index.js"));
            var stages=new List<object>();MusicFreeTrack[] tracks=[];
            string? Run(string operation,object arguments){
                int? status=null;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try{
                    var result=MusicFreeEngine.Run(source,operation,JsonSerializer.Serialize(arguments),timeout.Token,config=>{
                        try{var response=MusicFreeHttp.Send(config,timeout.Token);using var data=JsonDocument.Parse(response);status=data.RootElement.GetProperty("status").GetInt32();return response;}
                        catch(HttpRequestException e){status=(int?)e.StatusCode;throw;}
                    });
                    stages.Add(new{operation,result="pass",httpStatus=status});return result;
                }catch(Exception e){stages.Add(new{operation,result="failed",error=e.GetType().Name,httpStatus=status});return null;}
            }
            if(Run("inspect",new{})!=null){
                var catalogs=Run("catalogs",new{});
                var groups=catalogs==null?[]:JsonSerializer.Deserialize<MusicFreeCatalogGroup[]>(catalogs,Json)??[];
                if(groups.SelectMany(x=>x.Catalogs).FirstOrDefault() is {} catalog&&Run("browse",new{item=catalog.Item,page=1}) is {} browse)
                    tracks=JsonSerializer.Deserialize<MusicFreeResults>(browse,Json)?.Items??[];
                if(Run("search",new{query="piano",page=1}) is {} search){var found=JsonSerializer.Deserialize<MusicFreeResults>(search,Json)?.Items??[];stages.Add(new{operation="search-count",count=found.Length});if(found.Length>0)tracks=found;}
                foreach(var track in tracks.Take(2)){
                    if(Run("resolve",new{item=track.Item}) is not {} resolved)continue;
                    var media=JsonSerializer.Deserialize<MusicFreeSource>(resolved,Json)!;
                    try{MusicFreeHttp.PublicUri(media.Url);if(media.Headers?.Count>0)throw new NotSupportedException();}
                    catch(Exception e){stages.Add(new{operation="source-validation",result="failed",error=e.GetType().Name});continue;}
                    try{
                        Core.Initialize();using var engine=new LibVLC("--no-video","--aout=dummy","--quiet");using var player=new MediaPlayer(engine);using var native=new Media(engine,new Uri(media.Url));
                        player.Play(native);var until=DateTime.UtcNow.AddSeconds(20);while(DateTime.UtcNow<until&&player.State!=VLCState.Error&&!(player.Time>3000&&native.Statistics.DecodedAudio>0))Thread.Sleep(100);
                        var decoded=player.Time>3000&&native.Statistics.DecodedAudio>0;stages.Add(new{operation="native-audio",result=decoded?"pass":"failed",seekable=player.IsSeekable});player.Stop();
                    }catch(Exception e){stages.Add(new{operation="native-audio",result="failed",error=e.GetType().Name});}
                }
            }
            rows.Add(new{provider=name,sha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))).ToLowerInvariant(),stages});
            Console.WriteLine(name+": "+JsonSerializer.Serialize(stages));
            File.WriteAllText(report,JsonSerializer.Serialize(new{scope="Reviewed author plugins, embedded BingeX host, public requests, muted native decoding; no accounts or media saved",providers=rows},Json));
        }
        return 0;
    }
}
