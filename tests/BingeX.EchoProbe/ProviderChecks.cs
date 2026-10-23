using BingeX;
using LibVLCSharp.Shared;
internal static class ProviderChecks
{
    // Explicit, opt-in public-provider test. No credentials or media URLs are persisted.
    public static async Task<int> Run(string[] args)
    {
        if(args.Length!=5||!Path.GetFileName(Path.GetFullPath(args[3])).StartsWith("music-provider-test-"))return 2;
        var stage="setup";
        try
        {
            var profile=EchoRuntimeProfile.Load(args[1],args[3],args[0]=="--provider-enhanced");var package=EchoPackages.Inspect(args[2]);
            await using var bridge=new EchoMusicBridge(profile.Runtime);
            stage="activation";await bridge.InitializeAsync(new(args[2],package.Sha256,package.EntryClass,profile.SdkHash));Console.WriteLine("PASS activation");
            stage="search";var feed=await bridge.SearchAsync(args[4]);EchoItem? track=null;
            static IEnumerable<EchoItem> Walk(IEnumerable<EchoItem> items){foreach(var item in items){yield return item;foreach(var child in Walk(item.Items??[]))yield return child;}}
            foreach(var tab in feed.Tabs.Count>0?feed.Tabs:new[]{new EchoTab("","")}){var page=await bridge.PageAsync(feed.Feed,tab.Id);track=Walk(page.Items).FirstOrDefault(x=>x.Kind=="track");if(track!=null)break;}
            if(track==null)throw new InvalidOperationException();Console.WriteLine("PASS search and track catalogue");
            stage="variants";var variants=await bridge.VariantsAsync(track.Handle!);
            stage="resolve";var sources=await bridge.ResolveAsync(variants.OrderByDescending(x=>x.Quality).First().Handle);Console.WriteLine("PASS source resolution: "+sources.Count);
            await using var relay=new HeaderStreamRelay();stage="decode";
            foreach(var source in sources.Take(3))
            {
                var local=relay.Register(source.Url,source.Headers);
                using(var http=new System.Net.Http.HttpClient{Timeout=TimeSpan.FromSeconds(15)}){using var req=new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,local);req.Headers.Range=new(0,4095);using var response=await http.SendAsync(req,System.Net.Http.HttpCompletionOption.ResponseHeadersRead);Console.WriteLine("STAGE relay HTTP "+(int)response.StatusCode);response.EnsureSuccessStatusCode();}
                var done=new TaskCompletionSource<bool>();
                var thread=new Thread(()=>
                {
                    try{Core.Initialize();Console.WriteLine("STAGE native initialized");using var engine=new LibVLC("--no-video","--aout=dummy");using var media=new Media(engine,new Uri(local));using var player=new MediaPlayer(engine);
                        var ok=false;Console.WriteLine("STAGE native play");if(player.Play(media)){var end=DateTime.UtcNow.AddSeconds(18);while(DateTime.UtcNow<end){Thread.Sleep(200);if(player.Time>3000&&media.Statistics.DecodedAudio>0){ok=true;break;}if(player.State==VLCState.Error)break;}}Console.WriteLine("STAGE decode result "+ok);done.SetResult(ok);player.Stop();
                    }catch{done.TrySetResult(false);}
                }){IsBackground=true};if(OperatingSystem.IsWindows())thread.SetApartmentState(ApartmentState.STA);thread.Start();
                if(await done.Task.WaitAsync(TimeSpan.FromSeconds(25))){Console.WriteLine("PASS real provider audio decoded beyond three seconds (silent output)");return 0;}
            }
            Console.WriteLine("FAIL decode: resolved sources did not deliver playable audio");return 1;
        }
        catch(EchoCompatibilityException error){Console.WriteLine("FAIL "+stage+": "+error.Stage+" / "+error.FailureType);return 1;}
        catch(Exception error){Console.WriteLine("FAIL "+stage+": "+error.GetType().Name);return 1;}
    }
}
