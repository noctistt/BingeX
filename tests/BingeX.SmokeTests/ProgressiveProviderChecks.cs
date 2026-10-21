using BingeX;
internal static class ProgressiveProviderChecks
{
    public static async Task Run()
    {
        InstalledAddon[] providers=[new("slow","Slow","https://slow.invalid/manifest.json"),new("fast","Fast","https://fast.invalid/manifest.json")];
        var cancelled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using(var results=ProgressiveProviders.Load<string>(providers,async(addon,token)=>
        {
            if(addon.Id=="fast") return new(["ready"],0);
            try { await Task.Delay(Timeout.Infinite,token); return new([],0); }
            finally { cancelled.TrySetResult(); }
        },default).GetAsyncEnumerator())
        {
            if(!await results.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)) || results.Current.Name!="Fast" || results.Current.Pending.Single()!="Slow")
                throw new Exception("Fast provider results must appear before a blocked provider completes.");
        }
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var updates=new List<ProviderUpdate<string>>();
        await foreach(var update in ProgressiveProviders.Load<string>(providers,(addon,_)=>addon.Id=="slow" ? Task.FromException<ProviderResult<string>>(new Exception()) : Task.FromResult(new ProviderResult<string>(["ready"],0)),default)) updates.Add(update);
        if(updates.Count!=2 || updates.Sum(x=>x.Result.Failures)!=1 || updates.SelectMany(x=>x.Result.Items).Single()!="ready") throw new Exception("Provider failure must not discard successful results.");
        using(var client=new AddonClient(new ManifestHandler()))
        {
            client.CloudstreamRequest=async(_,ct)=>{ await Task.Delay(Timeout.Infinite,ct); return default; };
            var library=new MediaLibrary(client);
            var mixed=Enumerable.Range(0,6).Select(i=>new InstalledAddon("cloud"+i,"Cloud "+i,$"cloudstream://local/{i}/manifest.json"))
                .Append(new InstalledAddon("http","HTTP fast","https://fast.invalid/manifest.json")).ToArray();
            await using var result=ProgressiveProviders.Load<CatalogInfo>(mixed,(addon,ct)=>library.Catalogs([addon],ct),default).GetAsyncEnumerator();
            if(!await result.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)) || result.Current.Name!="HTTP fast") throw new Exception("Queued Cloudstream extensions must not consume Stremio HTTP request slots.");
        }
        if(!new SubtitlePreference("online",SubtitlePreference.LanguageKey("eng")).Matches(new("fixture","1","en","English","https://example.invalid/sub"))) throw new Exception("Subtitle language aliases must match across episodes.");
        if(SubtitlePreference.LanguageKey("en-US")!=SubtitlePreference.LanguageKey("eng")) throw new Exception("Regional language codes must match the same base language.");
        if(SubtitlePreference.EmbeddedLanguage("English [eng]")!="english") throw new Exception("Embedded subtitle labels retain language identity.");
        var directory=Path.Combine(Path.GetTempPath(),"BingeX-subtitle-pref-"+Guid.NewGuid());
        try
        {
            var first=new MediaContext("show","series","Show","","fixture","episode-1");
            using(var store=new LibraryStore(directory)) store.SaveSubtitlePreference(first,new("online","english","English","Provider"));
            using(var store=new LibraryStore(directory))
            {
                if(store.SubtitlePreference(first with{EpisodeId="episode-2"})?.Language!="english" || store.SubtitlePreference(first with{Id="other"})!=null) throw new Exception("Subtitle preference persists per title across episodes without affecting another title.");
                store.SaveSubtitlePreference(first,new("off"));
            }
            using(var store=new LibraryStore(directory)) if(store.SubtitlePreference(first)?.Mode!="off") throw new Exception("Subtitle Off must persist.");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory,true); }
        Console.WriteLine("PASS: progressive fast/slow results, cancellation and partial failure; subtitle language aliases and per-title preference persistence.");
    }
    private sealed class ManifestHandler:System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)=>
            Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent("{\"resources\":[\"catalog\"],\"catalogs\":[{\"id\":\"popular\",\"type\":\"movie\"}]}")});
    }
}
