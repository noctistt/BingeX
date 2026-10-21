using System.Reflection;
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
    private static object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
    private static object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, args);
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
        Call("BeginSource", new StreamChoice("Fixture", "Layout fixture", "", media, null),
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

    private static void Step()
    {
        heartbeat = DateTime.UtcNow;
        window ??= (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;
        if (window == null || Field("player") == null) return;
        switch (phase)
        {
            case 0:
                window.Title = "BingeX layout regression";
                window.Closed += (_, _) => done = true;
                Call("Page", "Details"); Start(); phase = 1; break;
            case 1:
                if (!Playing()) return;
                Geometry(false); previousTime = Player.Time; deadline = DateTime.UtcNow.AddSeconds(4); phase = 2; break;
            case 2:
                if (DateTime.UtcNow < deadline) return;
                Require(!Busy && Player.IsPlaying && Player.Time > previousTime + 2000, "Playback stopped after startup");
                Call("Page", "Details"); deadline = DateTime.UtcNow.AddMilliseconds(400); phase = 3; break;
            case 3:
                if (DateTime.UtcNow < deadline) return;
                Geometry(true);
                window.WindowState = cycle % 2 == 0 ? WindowState.Maximized : WindowState.Normal;
                Start(); phase = 4; break;
            case 4:
                if (!Playing()) return;
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
                phase = 99; window.Close(); break;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr windowHandle, out NativeRect rect);
}
