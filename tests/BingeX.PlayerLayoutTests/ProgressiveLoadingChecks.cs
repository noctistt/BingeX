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
            var torrentContext=new MediaContext("torrent-filter-fixture","movie","Torrent filter","","fixture");
            StreamChoice[] mixed=[new("Fixture","Direct","","https://fixture/direct",null),new("Fixture","Torrent","","",null,new TorrentSource(new string('a',40),null,[],[]))];
            Control<CheckBox>("IncludeTorrentSourcesCheck").IsChecked=false;
            Call("RenderSourceChoices",mixed,torrentContext);
            Require(Control<StackPanel>("StreamItems").Children.Count==1 && !Control<Button>("SuggestedSource").IsVisible,"Torrent opt-out keeps direct streams and hides torrent suggestions.");
            Control<CheckBox>("IncludeTorrentSourcesCheck").IsChecked=true;
            Require(Control<StackPanel>("StreamItems").Children.Count==2 && Control<Button>("SuggestedSource").IsVisible && store.Preferences().IncludeTorrentSources,"Enabling torrents immediately reveals existing results and persists the choice.");
            var retiredTorrent=Control<StackPanel>("StreamItems").Children.OfType<Button>().Single(x=>Equals(x.Tag,SourceSelection.Key(mixed[1])));
            Control<CheckBox>("IncludeTorrentSourcesCheck").IsChecked=false;
            retiredTorrent.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Call("SuggestedSource_Click",null,new Avalonia.Interactivity.RoutedEventArgs());
            Require(Control<StackPanel>("StreamItems").Children.Count==1 && Get("currentPath")==null && !store.Preferences().IncludeTorrentSources,"Disabling torrents blocks stale torrent actions and suggested selection cannot pick a direct stream.");
            var filterContext=new MediaContext("anime-filter-fixture","series","Anime fixture","","fixture","episode1");
            StreamChoice[] audioSources=[new("Fixture","[SUB] 1080p","","https://fixture/sub",null),new("Fixture","English Dubbed","","https://fixture/dub",null),new("Fixture","Dual Audio","","https://fixture/dual",null),new("Fixture","1080p HEVC","","https://fixture/unknown",null)];
            store.SaveShowAudioPreference(filterContext,new("","","DualAudio"));
            Call("RenderSourceChoices",audioSources,filterContext);
            Require(Control<StackPanel>("StreamItems").Children.Count==4 && (string)Get("sourceAudioFilter")! == "All","A previous saved Dual audio filter must not hide sources when opening a title.");
            var dubButton=Control<WrapPanel>("SourceAudioFilters").Children.OfType<Button>().Single(x=>x.Content!.ToString()!.StartsWith("Dubbed"));
            dubButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Require(Control<StackPanel>("StreamItems").Children.Count==1,"Manual Dub filter shows only labelled dub streams during the current search.");
            Call("ClearSourceChoices");
            Call("RenderSourceChoices",audioSources,filterContext with{EpisodeId="episode2"});
            Require(Control<StackPanel>("StreamItems").Children.Count==4 && (string)Get("sourceAudioFilter")! == "All","A new episode search starts with All audio types.");
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
            Call("ClearSourceChoices");
            StreamChoice[] filterSources=[
                new("Provider A","English Dubbed","Full source description","https://fixture/a1080",null,Evidence:new(null,null,1080)),
                new("Provider A","English Dubbed","","https://fixture/a720",null,Evidence:new(null,null,720)),
                new("Provider B","Hindi Dubbed","","https://fixture/b1080",null,Evidence:new(null,null,1080)),
                new("Provider B","Unlabelled","","https://fixture/unknown",null)];
            Call("RenderSourceChoices",filterSources,filterContext);
            void Pick(string picker,string label)
            {
                var control=Control<ComboBox>(picker);
                control.SelectedItem=control.Items.Cast<object>().Single(x=>x.ToString()==label);
            }
            Pick("SourceProviderPicker","Provider A"); Pick("SourceResolutionPicker","1080p"); Pick("SourceLanguagePicker","English");
            Require(Control<StackPanel>("StreamItems").Children.Count==1 && ((IEnumerable<StreamChoice>)typeof(MainWindow).GetProperty("PlaybackSources",flags)!.GetValue(window)!).Single().Url.EndsWith("a1080"),"Combined source filters match provider, resolution and language; explicit language overrides the remembered preference.");
            Call("RenderSourceChoicesCore",filterSources.Append(filterSources[0] with{Url="https://fixture/late"}).ToArray(),filterContext,true);
            Require(Control<StackPanel>("StreamItems").Children.Count==2 && Control<ComboBox>("SourceProviderPicker").SelectedItem!.ToString()=="Provider A","Late results respect and preserve active filters.");
            Pick("SourceLanguagePicker","Hindi");
            Require(Control<StackPanel>("StreamItems").Children.Count==0 && Control<TextBlock>("SourceFilterEmpty").IsVisible,"Conflicting filters show a clear empty state with a reset action.");
            Call("ResetSourceFilters_Click",null,new Avalonia.Interactivity.RoutedEventArgs());
            Require(Control<StackPanel>("StreamItems").Children.Count==5 && !Control<TextBlock>("SourceFilterEmpty").IsVisible,"Reset filters restores every eligible source.");
            Pick("SourceResolutionPicker","Unknown"); Pick("SourceLanguagePicker","Unknown");
            Require(Control<StackPanel>("StreamItems").Children.Count==1,"Unknown quality and audio remain explicitly selectable.");
            Call("ClearSourceChoices"); Call("RenderSourceChoices",filterSources,filterContext with{EpisodeId="episode3"});
            Require(Control<StackPanel>("StreamItems").Children.Count==4 && Control<ComboBox>("SourceProviderPicker").SelectedItem!.ToString()=="All providers" && Control<ComboBox>("SourceResolutionPicker").SelectedItem!.ToString()=="All resolutions" && Control<ComboBox>("SourceLanguagePicker").SelectedItem!.ToString()=="All audio languages","New searches reset all source filters.");
            var staleFilter=Control<WrapPanel>("SourceAudioFilters").Children.OfType<Button>().First();
            var staleSource=Control<StackPanel>("StreamItems").Children.OfType<Button>().First();
            var movie=new TitleInfo("new-movie-fixture","movie","New movie","","","Synopsis","","","","progress-fast",[]);
            await (Task)Call("ShowTitle",movie,null)!;
            Require(!Control<StackPanel>("SourceAudioFilterPanel").IsVisible && Control<WrapPanel>("SourceAudioFilters").Children.Count==0 && !Control<Button>("SuggestedSource").IsVisible && Control<StackPanel>("StreamItems").Children.Count==0 && Get("availableSourceContext")==null,"Opening a title immediately clears the previous source pool and audio controls.");
            staleFilter.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            staleSource.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Require(Control<StackPanel>("StreamItems").Children.Count==0 && Get("currentPath")==null,"Retired filter and source callbacks cannot revive or play the old title.");
            handler.ReleaseStreams=new(TaskCreationOptions.RunContinuationsAsynchronously); handler.HoldAllStreams=true;
            var retiredLookup=(Task)Call("FindStreams")!;
            await (Task)Call("ShowTitle",movie with{Id="another-movie-fixture"},null)!;
            handler.ReleaseStreams.TrySetResult(); await retiredLookup;
            Require(Control<StackPanel>("StreamItems").Children.Count==0 && Get("availableSourceContext")==null,"Cancelled title lookups cannot restore sources after navigation.");
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
            var priorSource=Get("activeSource"); var priorSourceContext=Get("activeSourceContext");
            Set("activeSource",new StreamChoice("Slow fixture","Previous episode","","https://slow.fixture/previous",null,Evidence:new(null,null,1080)));
            Set("activeSourceContext",Get("currentContext"));
            handler.ReleaseStreams=new(TaskCreationOptions.RunContinuationsAsynchronously); handler.HoldAllStreams=false;
            var choosingNext=(Task)Call("StartNextEpisode")!;
            deadline=DateTime.UtcNow.AddSeconds(5);
            while(Control<StackPanel>("StreamItems").Children.Count==0 && DateTime.UtcNow<deadline)await Task.Delay(25);
            Require(!choosingNext.IsCompleted && Get("currentPath")==null,"An early result from another provider cannot win next-episode playback while the preferred provider is pending.");
            handler.ReleaseStreams.TrySetResult(); await choosingNext.WaitAsync(TimeSpan.FromSeconds(5));
            Require(Get("currentPath")==null && Control<StackPanel>("StreamItems").Children.Count==2 && Control<TextBlock>("DetailMessage").Text!.Contains("Choose an alternative"),"When preferred quality is unavailable, next-episode alternatives stay visible for manual choice.");
            var recoveryContext=(MediaContext)Get("currentContext")!;
            var priorPosition=Get("lastPosition"); var priorDuration=Get("lastDuration"); var priorKey=Get("currentHistoryKey"); var priorName=Get("currentTitle"); var priorResume=Get("resumeAt");
            Set("currentPath","https://fixture/failed"); Set("currentHistoryKey",recoveryContext.HistoryKey); Set("currentTitle","Recovery fixture");
            Set("lastPosition",45000L); Set("lastDuration",120000L); Set("resumeAt",0L);
            Call("SaveProgress");
            Require(store.FindHistory(recoveryContext.HistoryKey)?.Position==45000,"Stopped/error recovery retains the last known episode position.");
            Call("ShowSourceRecovery","Source unavailable","Fixture failure");
            Require(Control<Border>("PlayerRecovery").IsVisible && Control<TextBlock>("PlayerRecoveryTitle").Text=="Source unavailable","Failure acknowledgement stays visible with an actionable source choice.");
            Set("currentPath",null);
            await (Task)Call("OpenCurrentSources")!;
            Require(((MediaContext)Get("selectedContext")!).HistoryKey==recoveryContext.HistoryKey && Control<StackPanel>("StreamItems").Children.Count==2,"Choose another source returns to the playing episode, not a browsed title or next episode.");
            Call("ResetSourceRecovery");
            Require(!Control<Border>("PlayerRecovery").IsVisible,"New media clears old recovery notices.");
            Set("lastPosition",priorPosition); Set("lastDuration",priorDuration); Set("currentHistoryKey",priorKey); Set("currentTitle",priorName); Set("resumeAt",priorResume);
            Set("activeSource",priorSource); Set("activeSourceContext",priorSourceContext);
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
