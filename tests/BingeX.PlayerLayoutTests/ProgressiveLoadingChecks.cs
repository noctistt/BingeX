using System.Net;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using BingeX;
internal static class ProgressiveLoadingChecks
{
    public static async Task Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object? Get(string name)=>typeof(MainWindow).GetField(name,flags)!.GetValue(window);
        void Set(string name,object? value)=>typeof(MainWindow).GetField(name,flags)!.SetValue(window,value);
        object? Call(string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,args);
        T Control<T>(string name) where T:Control=>window.FindControl<T>(name)!;
        void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        var store=(LibraryStore)Get("store")!; var prior=Get("library");
        using var handler=new Handler(); using var client=new AddonClient(handler); var library=new MediaLibrary(client);
        var fast=new InstalledAddon("progress-fast","Fast fixture","https://fast.fixture/manifest.json");
        var slow=new InstalledAddon("progress-slow","Slow fixture","https://slow.fixture/manifest.json");
        store.Install(slow); store.Install(fast); Set("library",library);
        try
        {
            Call("Page","Library");
            var loading=(Task)Call("RefreshCatalogs",(object?)null)!;
            var deadline=DateTime.UtcNow.AddSeconds(5);
            while(Control<WrapPanel>("PosterGrid").Children.Count==0 && DateTime.UtcNow<deadline) await Task.Delay(25);
            Require(!loading.IsCompleted && Control<WrapPanel>("PosterGrid").Children.Count==1,"Fast catalog posters must appear before slow provider discovery completes.");
            Require(Control<TextBlock>("CatalogProviderStatus").Text!.Contains("Slow fixture"),"Catalog progress names the waiting provider.");
            handler.ReleaseCatalog.TrySetResult(); await loading;
            Require(Control<WrapPanel>("DiscoverAddons").Children.Count==2,"Late catalog provider joins without hiding early results.");
            var catalog=new CatalogInfo(fast,"popular","movie","Popular",true,true);
            var count=handler.CatalogRequests;
            await library.Catalog(catalog,null,default);
            Require(handler.CatalogRequests==count,"Revisiting a catalog reuses its short-lived cache.");
            library.Invalidate(); await library.Catalog(catalog,null,default);
            Require(handler.CatalogRequests==count+1,"Explicit refresh invalidates catalog cache.");
            Set("selectedContext",new MediaContext("fixture-movie","movie","Fixture","","progress-fast"));
            Set("selectedTitle",null); Call("Page","Details");
            var streams=(Task)Call("FindStreams")!;
            deadline=DateTime.UtcNow.AddSeconds(5);
            while(Control<StackPanel>("StreamItems").Children.Count==0 && DateTime.UtcNow<deadline) await Task.Delay(25);
            Require(!streams.IsCompleted && Control<StackPanel>("StreamItems").Children.OfType<Button>().Single().IsEnabled,"Fast source must be playable while slow sources are pending.");
            var first=Control<StackPanel>("StreamItems").Children[0];
            handler.ReleaseStreams.TrySetResult(); await streams;
            Require(Control<StackPanel>("StreamItems").Children.Count==2 && ReferenceEquals(first,Control<StackPanel>("StreamItems").Children[0]),"Late streams append without replacing the source under the pointer.");
            var filterContext=new MediaContext("anime-filter-fixture","series","Anime fixture","","fixture","episode1");
            StreamChoice[] audioSources=[new("Fixture","[SUB] 1080p","","https://fixture/sub",null),new("Fixture","English Dubbed","","https://fixture/dub",null),new("Fixture","Dual Audio","","https://fixture/dual",null),new("Fixture","1080p HEVC","","https://fixture/unknown",null)];
            Call("RenderSourceChoices",audioSources,filterContext);
            var dubButton=Control<WrapPanel>("SourceAudioFilters").Children.OfType<Button>().Single(x=>x.Content!.ToString()!.StartsWith("Dubbed"));
            dubButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Require(Control<StackPanel>("StreamItems").Children.Count==1 && store.ShowAudioPreference(filterContext).SourceFilter=="Dubbed","Dub filter shows only labelled dub streams and saves the selection for this show.");
            Call("RenderSourceChoices",audioSources,filterContext with{EpisodeId="episode2"});
            Require(Control<StackPanel>("StreamItems").Children.Count==1,"Next episode retains the source filter.");
            var unknown=Control<WrapPanel>("SourceAudioFilters").Children.OfType<Button>().Single(x=>x.Content!.ToString()!.StartsWith("Unknown"));
            unknown.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Require(Control<StackPanel>("StreamItems").Children.Count==1,"Unlabelled streams remain available under Unknown.");
            store.SaveShowAudioPreference(filterContext,new("english","English","All"));
            Set("sourceAudioFilter","All");
            Call("RenderSourceChoices",new StreamChoice[]{audioSources[1],audioSources[1] with{Name="Hindi Dubbed",Url="https://fixture/hi"}},filterContext);
            var playback=(IEnumerable<StreamChoice>)typeof(MainWindow).GetProperty("PlaybackSources",flags)!.GetValue(window)!;
            Require(playback.Single().Name=="English Dubbed","Autoplay must not select a known wrong audio language when the show prefers English.");
            Call("RememberSourceAudioChoice",audioSources[1] with{Name="Hindi Dubbed"},filterContext);
            Require(store.ShowAudioPreference(filterContext).Language=="hindi","Choosing a clearly labelled single-language source remembers its audio language.");
            var priorContext=Get("currentContext"); var priorTitle=Get("playingEpisodeTitle");
            var sequence=new TitleInfo("sequence-fixture","series","Sequence fixture","","","Synopsis","","","","progress-fast",[new("first","Episode one",1,1,""),new("next","Episode two",1,2,"")]);
            Set("playingEpisodeTitle",sequence); Set("currentContext",new MediaContext(sequence.Id,sequence.Type,sequence.Name,"","progress-fast","first"));
            handler.ReleaseStreams=new(TaskCreationOptions.RunContinuationsAsynchronously); handler.HoldAllStreams=true;
            var preparing=(Task)Call("StartNextEpisode")!;
            deadline=DateTime.UtcNow.AddSeconds(5);
            while(!Control<Border>("EpisodePreparingPanel").IsVisible && DateTime.UtcNow<deadline) await Task.Delay(25);
            Require(Control<Border>("EpisodePreparingPanel").IsVisible && Control<TextBlock>("EpisodePreparingTitle").Text!.Contains("Episode two"),"Next-episode preparation names the target episode.");
            Call("CancelEpisodePreparation_Click",null,new Avalonia.Interactivity.RoutedEventArgs());
            await preparing.WaitAsync(TimeSpan.FromSeconds(5)); handler.ReleaseStreams.TrySetResult();
            Require(!Control<Border>("EpisodePreparingPanel").IsVisible && Get("currentPath")==null && Control<TextBlock>("DetailMessage").Text!.Contains("cancelled"),"Cancel must prevent a late source from starting playback.");
            Set("currentContext",priorContext); Set("playingEpisodeTitle",priorTitle);

        }
        finally
        {
            handler.ReleaseCatalog.TrySetResult(); handler.ReleaseStreams.TrySetResult();
            store.RemoveAddon(fast.Id);store.RemoveAddon(slow.Id); Set("library",prior);
            Set("catalogs",Array.Empty<CatalogInfo>()); Call("Page","Library");
        }
        log("PASS: early catalog posters and playable streams before slow providers, pending names, stable appended rows and catalog cache invalidation.");
    }
    private sealed class Handler:HttpMessageHandler
    {
        public TaskCompletionSource ReleaseCatalog=new(TaskCreationOptions.RunContinuationsAsynchronously),ReleaseStreams=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CatalogRequests;
        public bool HoldAllStreams;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var uri=request.RequestUri!; var slow=uri.Host=="slow.fixture";
            object value;
            if(uri.AbsolutePath.EndsWith("manifest.json"))
            {
                if(slow) await ReleaseCatalog.Task.WaitAsync(token);
                value=new{resources=new[]{"catalog","stream"},catalogs=new[]{new{id="popular",type="movie",name="Popular"}}};
            }
            else if(uri.AbsolutePath.Contains("/stream/"))
            {
                if(slow || HoldAllStreams)await ReleaseStreams.Task.WaitAsync(token);
                value=new{streams=new[]{new{name=slow?"Slow source":"Fast source",url="https://"+uri.Host+"/video"}}};
            }
            else { CatalogRequests++; value=new{metas=new[]{new{id="fixture-movie",type="movie",name="Fixture"}}}; }
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value))};
        }
    }
}
