using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BingeX;
using LibVLCSharp.Shared;

// Interactive Windows regression: runs the real app, renderer and native video.
// Requires a playable local file or HTTP fixture and an isolated data directory.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MainWindow? window;
    private static string media = "", log = "";
    private static int phase, cycle, failures;
    private static bool done;
    private static DateTime deadline, heartbeat = DateTime.UtcNow;
    private static long previousTime;
    private static Task? blockedNative;
    private static Task? discoverChecks;
    private static int savedAudio = -1;
    private static IReadOnlyDictionary<string, string>? FixtureHeaders => Environment.GetEnvironmentVariable("BINGEX_LAYOUT_HEADER_FIXTURE") == "1"
        ? new Dictionary<string,string> { ["Origin"] = "https://provider.example", ["Cookie"] = "session=fixture" } : null;
    private static int emptySource;
    private static readonly string[] EmptySources = ["Library", "My Library", "Details", "Add-ons"];
    private static object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
    private static object? Call(string name, params object?[] args) { var clock=Stopwatch.StartNew(); try{return typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, args);} finally{Log($"TIMING: {name} {clock.Elapsed.TotalMilliseconds:0}ms");} }
    private static MediaPlayer Player => (MediaPlayer)Field("player")!;
    private static bool Busy => (bool)typeof(MainWindow).GetProperty("NativePlaybackBusy", Private)!.GetValue(window)!;
    private static void Log(string message) { lock (typeof(Program)) File.AppendAllText(log, message + Environment.NewLine); }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    [STAThread]
    private static int Main(string[] args)
    {
        var data = Environment.GetEnvironmentVariable("BINGEX_DATA_DIRECTORY");
        if (!OperatingSystem.IsWindows() || args.Length != 1 || string.IsNullOrWhiteSpace(data)) return 2;
        Directory.CreateDirectory(data);
        log = Path.Combine(data, "player-layout-test.log");
        media = File.Exists(args[0]) ? Path.GetFullPath(args[0]) : args[0];
        Log("START");
        // Use the application's actual platform configuration, not a test renderer.
        var builder = (AppBuilder)typeof(MainWindow).Assembly.GetType("BingeX.Program")!
            .GetMethod("CreateAppBuilder")!.Invoke(null, null)!;
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            while (!Volatile.Read(ref done))
            {
                try { await Dispatcher.UIThread.InvokeAsync(Step); }
                catch (Exception error)
                {
                    Log("FAIL: " + error); failures++;
                    await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
                    break;
                }
                await Task.Delay(100);
            }
        });
        _ = Task.Run(async () =>
        {
            while (!Volatile.Read(ref done))
            {
                await Task.Delay(1000);
                if (phase > 0 && DateTime.UtcNow - heartbeat > TimeSpan.FromSeconds(20))
                { Log("FAIL: UI unresponsive"); Environment.Exit(3); }
            }
        });
        builder.StartWithClassicDesktopLifetime([]);
        done = true;
        Log(failures == 0 && phase == 99 ? "PASS: completed" : "FAIL: incomplete");
        return failures == 0 && phase == 99 ? 0 : 1;
    }

    private static void Start()
    {
        Call("BeginSource", new StreamChoice("Fixture", "Layout fixture", "", media, null, RequestHeaders: FixtureHeaders),
            new MediaContext("layout-fixture", "movie", "Layout fixture", "", "fixture"), true);
        deadline = DateTime.UtcNow.AddSeconds(25);
    }

    private static bool Playing()
    {
        Require(DateTime.UtcNow < deadline, "Playback did not become ready");
        return !Busy && Player.IsPlaying && Player.Time > 500;
    }

    private static T Control<T>(string name) where T : Control => window!.FindControl<T>(name)!;
    private static void Geometry(bool mini)
    {
        var surface = Control<LibVLCSharp.Avalonia.VideoView>("VideoSurface");
        var overlay = Control<Grid>("PlayerOverlay");
        Require(surface.IsEffectivelyVisible && surface.Bounds.Width > 0 && surface.Bounds.Height > 0, "Video is hidden or empty");
        Require(Math.Abs(surface.Bounds.Width - overlay.Bounds.Width) < 2 && Math.Abs(surface.Bounds.Height - overlay.Bounds.Height) < 2,
            "Video and controls have different sizes");
        Require(GetClientRect(Player.Hwnd, out var native), "Missing native video window");
        Require(Math.Abs(native.Right - surface.Bounds.Width * window!.RenderScaling) < 3 &&
            Math.Abs(native.Bottom - surface.Bounds.Height * window.RenderScaling) < 3, "Native video retained stale dimensions");
        Require(Control<ScrollViewer>("DetailsPage").IsVisible == mini, "Wrong details-page visibility");
        Require(Control<Border>("PlayerControls").IsVisible != mini && Control<Border>("MiniControls").IsVisible == mini,
            "Full and mini controls conflict");
        Log($"PASS: mini={mini}, video={surface.Bounds}, controls={overlay.Bounds}, native={native.Right}x{native.Bottom}, scale={window.RenderScaling}");
    }

    private static void CheckEmptyPlayer()
    {
        Require(Field("currentPath") == null, "Empty-page check unexpectedly loaded media");
        Require(!Control<ScrollViewer>("LibraryPage").IsVisible && !Control<ScrollViewer>("DetailsPage").IsVisible &&
            !Control<ScrollViewer>("LocalLibraryPage").IsVisible && !Control<ScrollViewer>("AddonsPage").IsVisible,
            "A previous page is still visible behind Player");
        Require(GetClassLongPtr(Player.Hwnd, -10) == GetStockObject(4),
            "Empty native video window has no black background and can retain previous-page pixels");
        var surface = Control<LibVLCSharp.Avalonia.VideoView>("VideoSurface");
        Require(GetClientRect(Player.Hwnd, out var native) && Math.Abs(native.Right - surface.Bounds.Width * window!.RenderScaling) < 3 &&
            Math.Abs(native.Bottom - surface.Bounds.Height * window.RenderScaling) < 3, "Empty native video has stale dimensions");
        Log("PASS: empty Player clears native background after " + EmptySources[emptySource]);
    }

    private static void Step()
    {
        var gap=(DateTime.UtcNow-heartbeat).TotalMilliseconds;if(phase>0&&gap>250)Log($"UI GAP: {gap:0}ms phase={phase}");if(phase==8)Require(gap<500,"Startup native wait blocked the UI dispatcher");heartbeat = DateTime.UtcNow;
        window ??= (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;
        if (window == null || Field("player") == null) return;
        switch (phase)
        {
            case 0:
                StreamAudioChecks.Run(window, Log);
                discoverChecks = DiscoverChecks.Run(window, Log); phase = 30; break;
            case 30:
                if (discoverChecks?.IsCompleted != true) return;
                discoverChecks.GetAwaiter().GetResult(); phase = 31; break;
            case 31:
                Control<Border>("WelcomeHero").Background = Avalonia.Media.Brushes.Magenta;
                for(var i=0;i<40;i++)Control<WrapPanel>("PosterGrid").Children.Add(new Border{ Width=150, Height=160, Margin=new Thickness(8), Background=i%2==0?Avalonia.Media.Brushes.Red:Avalonia.Media.Brushes.Lime });
                Call("Page","Library");deadline=DateTime.UtcNow.AddSeconds(2);phase=20;break;
            case 20:
                if (DateTime.UtcNow < deadline) return;
                Call("Page", "Player");
                var hold = int.TryParse(Environment.GetEnvironmentVariable("BINGEX_LAYOUT_EMPTY_HOLD_MS"), out var configuredHold) ? Math.Clamp(configuredHold, 200, 30000) : 300;
                deadline = DateTime.UtcNow.AddMilliseconds(hold); phase = 21; break;
            case 21:
                if (DateTime.UtcNow < deadline) return;
                CheckEmptyPlayer();
                emptySource = 1;
                Call("Page", EmptySources[emptySource]); deadline = DateTime.UtcNow.AddMilliseconds(300); phase = 22; break;
            case 22:
                if (DateTime.UtcNow < deadline) return;
                window.WindowState = emptySource % 2 == 0 ? WindowState.Normal : WindowState.Maximized;
                Call("Page", "Player"); deadline = DateTime.UtcNow.AddMilliseconds(300); phase = 23; break;
            case 23:
                if (DateTime.UtcNow < deadline) return;
                CheckEmptyPlayer();
                if (++emptySource < EmptySources.Length)
                { Call("Page", EmptySources[emptySource]); deadline = DateTime.UtcNow.AddMilliseconds(300); phase = 22; break; }
                window.Title = "BingeX layout regression";
                window.Closed += (_, _) => done = true;
                Call("RenderSourceChoices", Enumerable.Range(0,100).Select(i=>new StreamChoice("Fixture", "1080p source "+i,"Series episode · HEVC · Multi-audio\n1080p · 2.5 GiB · Layout fixture",media,null, RequestHeaders: FixtureHeaders)).ToArray(),new MediaContext("layout-fixture","movie","Layout fixture","","fixture"));
                Call("Page", "Details"); deadline=DateTime.UtcNow.AddSeconds(2); phase=7; break;
            case 7:
                if(DateTime.UtcNow<deadline)return;
                var queue=(NativePlaybackQueue)Field("nativePlayback")!;
                var delay = int.TryParse(Environment.GetEnvironmentVariable("BINGEX_LAYOUT_NATIVE_DELAY_MS"), out var configured) ? Math.Clamp(configured, 1000, 10000) : 2000;
                blockedNative=queue.Enqueue(()=>Thread.Sleep(delay));
                Start();
                Require((string)Field("activePage")! == "Player" && !Control<ScrollViewer>("DetailsPage").IsVisible,"Source page remained visible while waiting for native stop");
                Require(Control<Border>("PlayerLoading").IsVisible,"No clean opening state while native decoder is busy");
                phase=8;break;
            case 8:
                Require((string)Field("activePage")! == "Player","Navigation changed during native startup");
                if(blockedNative?.IsCompleted!=true)return;
                Log("PASS: immediate navigation and responsive dispatcher during injected native delay");phase=1;break;
            case 1:
                if (!Playing() || Control<Border>("PlayerLoading").IsVisible) return;
                BufferingChecks.Run(window, Log);
                Geometry(false); previousTime = Player.Time; deadline = DateTime.UtcNow.AddSeconds(4); phase = 2; break;
            case 2:
                if (DateTime.UtcNow < deadline) return;
                Require(!Busy && Player.IsPlaying && Player.Time > previousTime + 2000, "Playback stopped after startup");
                Require(!Control<Border>("PlayerLoading").IsVisible, "Opening cover remained over playing video");
                var audio = Player.AudioTrackDescription.Where(x => x.Id >= 0).ToArray();
                if (audio.Length > 1) { savedAudio = audio[1].Id; Control<ComboBox>("AudioTrackPicker").SelectedIndex = 1; }
                Call("Page", "Details"); deadline = DateTime.UtcNow.AddMilliseconds(400); phase = 3; break;
            case 3:
                if (DateTime.UtcNow < deadline) return;
                Geometry(true);
                window.WindowState = cycle % 2 == 0 ? WindowState.Maximized : WindowState.Normal;
                Start(); phase = 4; break;
            case 4:
                if (!Playing()) return;
                if (savedAudio >= 0 && Player.AudioTrack != savedAudio) return;
                if (savedAudio >= 0) Log("PASS: saved alternate audio track restored after source change");
                Geometry(false);
                if (++cycle < 4) { Call("Page", "Details"); deadline = DateTime.UtcNow.AddMilliseconds(400); phase = 3; }
                else { Call("Page", "Details"); deadline = DateTime.UtcNow.AddMilliseconds(400); phase = 5; }
                break;
            case 5:
                if (DateTime.UtcNow < deadline) return;
                Geometry(true); Call("Page", "Player"); deadline = DateTime.UtcNow.AddSeconds(3); phase = 6; break;
            case 6:
                if (DateTime.UtcNow < deadline) return;
                Geometry(false); Require(Player.IsPlaying, "Restoring paused playback");
                Call("Play_Click",null,new Avalonia.Interactivity.RoutedEventArgs());deadline=DateTime.UtcNow.AddSeconds(8);phase=9;break;
            case 9:
                Require(DateTime.UtcNow<deadline,"Pause did not complete");
                if(Busy||Player.State!=VLCState.Paused)return;
                Call("Play_Click",null,new Avalonia.Interactivity.RoutedEventArgs());deadline=DateTime.UtcNow.AddSeconds(8);phase=10;break;
            case 10:
                Require(DateTime.UtcNow<deadline,"Resume did not complete");
                if(Busy||!Player.IsPlaying)return;
                Log("PASS: pause/resume with background polling");
                Control<Slider>("SeekBar").Value=999;Call("Seek");deadline=DateTime.UtcNow.AddSeconds(12);phase=11;break;
            case 11:
                Require(DateTime.UtcNow<deadline,"End-of-media UI did not update");
                if(!(bool)Field("playbackEnded")!)return;
                Log("PASS: end-of-media update from background snapshot");
                Call("Play_Click",null,new Avalonia.Interactivity.RoutedEventArgs());deadline=DateTime.UtcNow.AddSeconds(15);phase=12;break;
            case 12:
                if(!Playing())return;
                Log("PASS: replay after end of media");
                if (media.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var unavailable = new Uri(new Uri(media), "unavailable-fixture.mp4").AbsoluteUri;
                    Call("BeginSource", new StreamChoice("Fixture", "Unavailable fixture", "", unavailable, null, RequestHeaders: FixtureHeaders), new MediaContext("layout-fixture", "movie", "Layout fixture", "", "fixture"), true);
                    deadline=DateTime.UtcNow.AddSeconds(25);phase=13;
                }
                else { phase=99;window.Close(); }
                break;
            case 13:
                if (!Playing() || (string?)Field("currentPath") != media) return;
                Log("PASS: failed source offers fallback and resumes a working stream");phase=99;window.Close();break;
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr GetClassLongPtr(IntPtr windowHandle, int index);
    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr windowHandle, out NativeRect rect);
}
