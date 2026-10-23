using System.Text.Json;
using BingeX;
internal static class BridgeChecks
{
    public static async Task<int> Run(string[] args,Func<string,Task<int>> decode)
    {
        if(args.Length!=9)return 2;
        var runtime=new EchoRuntime(args[1],args[2],args[3],args[4],args[8]);
        var extension=new EchoExtension(args[5],Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[5]))),"dev.brahmkshatriya.echo.extension.OpenSubsonicExtension",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[3]))));
        var fixture=args[6];var tokenFile=args[7];
        if(!Uri.TryCreate(fixture,UriKind.Absolute,out var server)||server.Host!="127.0.0.1")return 2;
        void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        async Task Login(EchoMusicBridge bridge)
        {
            var forms=await bridge.CallAsync("loginForms",new{});Check(forms.EnumerateArray().Any(x=>x.GetProperty("key").GetString()=="UserPass"),"Custom login forms were not mapped.");
            var users=await bridge.CallAsync("login",new{key="UserPass",data=new{address=fixture,username="fixture",password="probe-password"}});
            Check(!users.GetRawText().Contains("probe-password")&&!users.GetRawText().Contains("serverUrl"),"Login output exposed private account data.");
            await bridge.CallAsync("selectUser",new{handle=users[0].GetProperty("handle").GetString()});
        }
        await using(var bridge=new EchoMusicBridge(runtime))
        {
            await bridge.InitializeAsync(extension);Check(bridge.Capabilities.Contains("SearchFeedClient"),"Helper capabilities were not exposed.");await Login(bridge);
            var settings=await bridge.CallAsync("settings",new{});Check(settings.ValueKind==JsonValueKind.Array,"Settings schema is not structured.");
            await bridge.CallAsync("configure",new{values=new{search_results=2,force_get_requests=true}});
            var limited=await bridge.SearchAsync("fixture");var matched=await bridge.PageAsync(limited.Feed,"Tracks");Check(matched.Items.Count==2,"Session settings should limit search results.");
            var feed=await bridge.SearchAsync("");var first=await bridge.PageAsync(feed.Feed,"Tracks");
            Check(first.Items.Count==50&&first.Next.Length>0,"First page should retain an opaque continuation.");
            var second=await bridge.PageAsync(feed.Feed,"Tracks",first.Next);Check(second.Items.Count==1&&second.Next.Length==0,"Continuation should retrieve the remaining track.");
            var variants=await bridge.VariantsAsync(first.Items[0].Handle!);Check(variants.Count>0,"No playable variants mapped.");
            var sources=await bridge.ResolveAsync(variants.OrderByDescending(x=>x.Quality).First().Handle);
            Check(sources.Count>0&&new Uri(sources[0].Url).Host=="127.0.0.1","Resolved source left the fixture.");
            // Feed and resolution responses may carry private source URLs; do not print them.
            File.WriteAllText(tokenFile,sources[0].Url);try{Check(await decode(tokenFile)==0,"Helper-resolved audio did not decode.");}finally{File.Delete(tokenFile);}
            var albums=await bridge.SearchAsync("fixture");var albumPage=await bridge.PageAsync(albums.Feed,"Albums");
            Check(albumPage.Items.Single().Kind=="album","Album search is not mapped.");
            var album=await bridge.CallAsync("album",new{handle=albumPage.Items[0].Handle});
            var tracks=await bridge.PageAsync(album.GetProperty("feed").GetString()!,"");Check(tracks.Items.Count==1,"Album track feed did not load.");
            // Updating account/settings invalidates opaque handles from the previous browsing context.
            await bridge.CallAsync("configure",new{values=new{search_results=1}});var stale=false;
            try{await bridge.VariantsAsync(first.Items[0].Handle!);}catch(NotSupportedException error){stale=!error.Message.Contains("probe-password");}
            Check(stale&&bridge.IsStopped,"Stale handles should fail safely and terminate the session.");
        }
        await using(var bridge=new EchoMusicBridge(runtime))
        {
            await bridge.InitializeAsync(extension);await Login(bridge);var slow=await bridge.SearchAsync("slow");
            var cancelled=false;using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            try{await bridge.PageAsync(slow.Feed,"Tracks",token:cancel.Token);}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled&&bridge.IsStopped,"Cancelling a request must terminate Java before another request can run.");
        }
        await using(var bridge=new EchoMusicBridge(runtime))
        {
            await bridge.InitializeAsync(extension);var hello=await bridge.CallAsync("hello",new{});Check(hello.GetProperty("protocol").GetInt32()==1,"Clean restart did not handshake.");
            var failed=false;try{await bridge.CallAsync("browser",new{});}catch(NotSupportedException){failed=true;}
            Check(failed&&bridge.IsStopped,"Unsupported host requests must fail explicitly and stop the helper.");
        }
        await using(var bridge=new EchoMusicBridge(runtime))
        {
            var failed=false;try{await bridge.InitializeAsync(extension with{Sha256=new string('0',64)});}catch(IOException){failed=true;}
            Check(failed&&bridge.IsStopped,"Package hash mismatches must prevent loading.");
        }
        await using(var bridge=new EchoMusicBridge(runtime))
        {
            var failed=false;try{await bridge.InitializeAsync(extension with{SdkSha256=new string('0',64)});}catch(NotSupportedException){failed=true;}
            Check(failed&&bridge.IsStopped,"SDK hash mismatches must prevent initialization.");
        }
        Console.WriteLine("PASS: persistent Echo protocol, custom login, session settings, paged search/albums, source mapping, native decoding, stale-handle invalidation, cancellation, restart, unsupported features and package integrity.");
        return 0;
    }
}
