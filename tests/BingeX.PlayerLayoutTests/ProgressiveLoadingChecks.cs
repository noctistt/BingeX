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
                if(slow)await ReleaseStreams.Task.WaitAsync(token);
                value=new{streams=new[]{new{name=slow?"Slow source":"Fast source",url="https://"+uri.Host+"/video"}}};
            }
            else { CatalogRequests++; value=new{metas=new[]{new{id="fixture-movie",type="movie",name="Fixture"}}}; }
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value))};
        }
    }
}
