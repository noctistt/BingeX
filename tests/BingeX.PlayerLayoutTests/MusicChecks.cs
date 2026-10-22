using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BingeX;

internal static class MusicChecks
{
    public static async Task Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object? Get(string name)=>typeof(MainWindow).GetField(name,flags)!.GetValue(window);
        void Set(string name,object? value)=>typeof(MainWindow).GetField(name,flags)!.SetValue(window,value);
        object? Call(string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,args);
        T Control<T>(string name)where T:Control=>window.FindControl<T>(name)!;
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        async Task Wait(Func<bool> condition,string message){var end=DateTime.UtcNow.AddSeconds(18);while(!condition()){if(DateTime.UtcNow>end)throw new Exception(message);await Task.Delay(100);}}
        var store=(LibraryStore)Get("store")!;var queue=(LocalPlaybackQueue)Get("localQueue")!;var root=LocalLibraryScanner.NormalizeFolder(Path.Combine(store.DataDirectory,"music-fixture"));Directory.CreateDirectory(root);
        var first=Path.Combine(root,"z-first.wav");var second=Path.Combine(root,"a-second.wav");
        foreach(var (path,number,title) in new[]{(first,1,"First track"),(second,2,"Second track")})
        {
            File.WriteAllBytes(path,Wave());using var file=TagLib.File.Create(path);file.Tag.Title=title;file.Tag.Performers=["Fixture artist"];file.Tag.AlbumArtists=["Fixture artist"];file.Tag.Album="Fixture album";file.Tag.Track=(uint)number;file.Save();
        }
        var originalState=new[]{"lastPosition","lastDuration","resumeAt","currentTitle","currentHistoryKey","playbackEnded","playbackSnapshot","suppressCurrentHistory","currentContext","playingEpisodeTitle"}.ToDictionary(x=>x,Get);
        var original=SHA256.HashData(File.ReadAllBytes(first));var music=(MusicLibrary)Get("musicLibrary")!;var saved=store.Preferences();
        await using var server=new Server(Wave());
        try
        {
            foreach(var invalid in new[]{"https://user:password@music.example/","http://music.example/","https://music.example/?token=secret","file:///music"}){var rejected=false;try{using var connection=new SubsonicMusic(invalid,"fixture","password");}catch(ArgumentException){rejected=true;}Check(rejected,"Unsafe or credential-bearing server address was accepted.");}
            using(var connection=new SubsonicMusic(server.Address,"wrong","wrong")){var rejected=false;try{await connection.Albums("",0,CancellationToken.None);}catch(IOException error){rejected=!error.Message.Contains(server.Address)&&!error.Message.Contains("wrong");}Check(rejected,"Login failure must not expose private request details.");}
            var scan=await Task.Run(()=>music.Scan(root,CancellationToken.None));Check(scan.Complete&&scan.Tracks.Count==2,"Music scanner reads the two tagged audio files.");music.Apply(scan);
            Check(scan.Tracks.All(x=>x.Album=="Fixture album"&&x.Artist=="Fixture artist"&&x.SampleRate==16000&&x.Bits==16),"Tags and measured audio properties survive indexing.");
            Check(SHA256.HashData(File.ReadAllBytes(first)).SequenceEqual(original),"Music indexing must not write source media.");
            Check(new MusicLibrary(store.DataDirectory).Tracks.Count(x=>x.Folder==root)==2,"Music index survives reopening.");
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();var rejected=false;try{music.Scan(root,cancelled.Token);}catch(OperationCanceledException){rejected=true;}Check(rejected,"Music scan cancellation is honored.");}
            Call("LibraryMusic_Click",null,new RoutedEventArgs());
            Check(Control<ScrollViewer>("MusicPage").IsVisible&&!Control<ScrollViewer>("LocalLibraryPage").IsVisible&&!Control<Expander>("MusicServerPanel").IsVisible,"Music has its own offline sidebar page and hides server entry points.");
            await Task.Delay(100);
            using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(store.DataDirectory,"music-library.png"));}
            Check(Control<WrapPanel>("MusicAlbums").Children.Count==1,"Tracks form one album rather than separate filename cards.");
            Control<TextBox>("MusicSearch").Text="no matching album";await Task.Delay(300);Check(Control<WrapPanel>("MusicAlbums").Children.Count==0&&music.Tracks.Length==2,"Music search does not mutate the index.");Control<TextBox>("MusicSearch").Text="";await Task.Delay(300);
            Control<WrapPanel>("MusicAlbums").Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Control<StackPanel>("MusicTracks").Children.Count==2,"Album opens its complete ordered track list.");
            Control<CheckBox>("QueueAutoplayCheck").IsChecked=false;
            Call("MusicPlayAlbum_Click",null,new RoutedEventArgs());
            await Wait(()=>Get("currentPath") as string==first && !(bool)Get("playbackStarting")! && ((LibVLCSharp.Shared.MediaPlayer)Get("player")!).Time>500,"Local music did not decode.");
            Check((string)Get("currentTitle")! =="Fixture artist — First track","Local music playback should use tagged display names.");
            Check(queue.Items.SequenceEqual(new[]{first,second}),"Play album uses disc/track order rather than filename order.");
            Call("QueueNext_Click",null,new RoutedEventArgs());await Wait(()=>Get("currentPath") as string==second&&!(bool)Get("playbackStarting")!,"Next local music track did not open.");
            Call("CloseMiniPlayer_Click",null,new RoutedEventArgs());await Wait(()=>Get("currentPath")==null,"Music stop did not complete.");queue.Clear();Call("RenderLocalQueue");
            await (Task)Call("SetOnlineMode",true)!;
            Control<TextBox>("MusicServerAddress").Text=server.Address;Control<TextBox>("MusicServerUser").Text="fixture";Control<TextBox>("MusicServerPassword").Text="fixture-password";
            Call("ConnectMusicServer_Click",null,new RoutedEventArgs());await Wait(()=>Get("musicServer")!=null,"Music server fixture did not connect.");
            Check(Control<TextBox>("MusicServerPassword").Text==""&&Control<WrapPanel>("ServerAlbumItems").Children.Count==1,"Server login clears the password control and displays albums.");
            Call("ManageMusicConnections_Click",null,new RoutedEventArgs());
            Check(Control<ScrollViewer>("AddonsPage").IsVisible && Control<Expander>("MusicServerPanel").IsEffectivelyVisible && !Control<ScrollViewer>("MusicPage").IsVisible,"Music setup belongs to Add-ons.");
            Call("LibraryMusic_Click",null,new RoutedEventArgs());await Task.Delay(100);
            Check(Control<ScrollViewer>("MusicPage").IsVisible && Control<StackPanel>("MusicServerBrowser").IsEffectivelyVisible && !Control<TextBox>("MusicServerPassword").IsEffectivelyVisible,"Music browses connected albums without exposing setup fields.");
            var owner=(SubsonicMusic)Get("musicServer")!;
            await (Task)Call("LoadServerTracks",owner,new MusicAlbum("album","Server album","Server artist",2))!;
            Control<StackPanel>("ServerTrackItems").Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(()=>Get("currentHistoryKey")==null && Get("currentPath") as string==Get("musicCurrentUrl") as string && !(bool)Get("playbackStarting")! && ((LibVLCSharp.Shared.MediaPlayer)Get("player")!).Time>500,"Server audio did not decode through the relay.");
            var remote=(string)Get("musicCurrentUrl")!;Check(remote.Contains("format=raw")&&!remote.Contains("fixture-password")&&server.Authenticated>0,"Server uses salted token auth and requests original audio.");
            Check(store.FindHistory(remote)==null&&store.AudioSelection(remote)==null,"Authenticated playback addresses are not persisted as history or audio-choice keys.");
            Set("playbackEnded",true);Call("Play_Click",null,new RoutedEventArgs());await Wait(()=>!(bool)Get("playbackStarting")!&&((LibVLCSharp.Shared.MediaPlayer)Get("player")!).Time>500,"Server replay did not decode.");Check(Get("currentHistoryKey")==null,"Replaying server audio preserves transient history handling.");
            Control<CheckBox>("QueueAutoplayCheck").IsChecked=true;Check((bool)Call("ContinueLocalQueue")!,"Music autoplay should select the next server track.");await Wait(()=>((int)Get("serverTrackIndex")!)==1&&!(bool)Get("playbackStarting")!,"Server next-track action did not advance.");
            Call("CloseMiniPlayer_Click",null,new RoutedEventArgs());await Wait(()=>Get("currentPath")==null,"Server audio did not stop.");
            await (Task)Call("SetOnlineMode",false)!;Check(Get("musicServer")==null&&!Control<Expander>("MusicServerPanel").IsVisible,"Disabling online mode cancels and disconnects the music browser.");
            music.Apply(new MusicScan(root,[],1,false));Check(music.Tracks.Count(x=>x.Folder==root)==2,"Incomplete scans preserve previous tracks.");
            music.Remove(root);Check(File.Exists(first)&&File.Exists(second),"Removing music folders keeps media files.");
        }
        finally
        {
            if(Get("currentPath")!=null){Call("CloseMiniPlayer_Click",null,new RoutedEventArgs());await Wait(()=>Get("currentPath")==null,"Music test cleanup failed.");}
            Call("DisconnectMusicServer");await (Task)Call("SetOnlineMode",false)!;music.Remove(root);queue.Clear();store.RemoveHistory(first);store.RemoveHistory(second);
            Control<CheckBox>("QueueAutoplayCheck").IsChecked=saved.AutoplayLocalQueue;store.SavePreferences(saved);Set("preferences",saved);Call("RenderLocalQueue");foreach(var entry in originalState)Set(entry.Key,entry.Value);Call("Page","Library");
        }
        log("PASS: music tags/quality, read-only scan, persistence/cancellation, album tabs/search/order, real local/server audio decoding, next/replay, token-auth relay, credential-free history and online-mode cleanup.");
    }
    private static byte[] Wave()
    {
        using var data=new MemoryStream();using var writer=new BinaryWriter(data);var samples=16000*20;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(16000);writer.Write(32000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
        for(var i=0;i<samples;i++)writer.Write((short)(Math.Sin(i*2*Math.PI*440/16000)*1000));return data.ToArray();
    }
    private sealed class Server:IAsyncDisposable
    {
        private readonly HttpListener listener=new();private readonly CancellationTokenSource work=new();private readonly Task running;private readonly byte[] wave;
        public string Address{get;}public int Authenticated;
        public Server(byte[] wave)
        {
            this.wave=wave;var port=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);port.Start();var number=((IPEndPoint)port.LocalEndpoint).Port;port.Stop();Address=$"http://127.0.0.1:{number}/";listener.Prefixes.Add(Address);listener.Start();running=Run();
        }
        private async Task Run()
        {
            try{while(!work.IsCancellationRequested){var context=await listener.GetContextAsync().WaitAsync(work.Token);_ = Serve(context);}}catch(Exception e) when(e is OperationCanceledException or HttpListenerException or ObjectDisposedException){}
        }
        private async Task Serve(HttpListenerContext context)
        {
            try
            {
                var query=context.Request.QueryString;var expected=Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("fixture-password"+query["s"]))).ToLowerInvariant();
                if(query["u"]!="fixture"||query["t"]!=expected){context.Response.StatusCode=403;return;}Interlocked.Increment(ref Authenticated);
                if(context.Request.Url!.AbsolutePath.EndsWith("stream.view"))
                {
                    if(query["format"]!="raw"){context.Response.StatusCode=400;return;}context.Response.ContentType="audio/wav";context.Response.Headers["Accept-Ranges"]="bytes";var offset=0;
                    if(context.Request.Headers["Range"] is {} range&&int.TryParse(range.Split('=')[1].Split('-')[0],out var parsed))offset=Math.Clamp(parsed,0,wave.Length-1);
                    if(offset>0){context.Response.StatusCode=206;context.Response.Headers["Content-Range"]=$"bytes {offset}-{wave.Length-1}/{wave.Length}";}
                    context.Response.ContentLength64=wave.Length-offset;if(context.Request.HttpMethod!="HEAD")await context.Response.OutputStream.WriteAsync(wave.AsMemory(offset));return;
                }
                var payload=context.Request.Url.AbsolutePath.EndsWith("getAlbum.view")?"\"album\":{\"song\":[{\"id\":\"one\",\"title\":\"First\",\"artist\":\"Fixture\",\"track\":1,\"duration\":20,\"suffix\":\"wav\"},{\"id\":\"two\",\"title\":\"Second\",\"artist\":\"Fixture\",\"track\":2,\"duration\":20,\"suffix\":\"wav\"}]}":"\"albumList2\":{\"album\":[{\"id\":\"album\",\"name\":\"Server album\",\"artist\":\"Fixture\",\"songCount\":2}]}";
                var bytes=Encoding.UTF8.GetBytes("{\"subsonic-response\":{\"status\":\"ok\","+payload+"}}");context.Response.ContentType="application/json";context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);
            }
            catch(Exception e) when(e is IOException or HttpListenerException or ObjectDisposedException){}
            finally{context.Response.Close();}
        }
        public async ValueTask DisposeAsync(){work.Cancel();listener.Close();await running;work.Dispose();}
    }
}
