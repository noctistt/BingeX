using Avalonia;

namespace BingeX;

internal static class Program
{
    internal static SingleInstance? Instance { get; private set; }
    public static string? StartupFile { get; private set; }
    public static string? StartupSubtitle { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        var dataDirectory = Environment.GetEnvironmentVariable("BINGEX_DATA_DIRECTORY") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BingeX");
        using var instance = new SingleInstance(dataDirectory);
        Instance = instance;
        // Resolve paths in the launching process, before handing them to the existing window.
        args = args.Select(x => File.Exists(x) ? Path.GetFullPath(x) : x).ToArray();
        if (!instance.IsPrimary) return instance.Forward(args).GetAwaiter().GetResult() ? 0 : 1;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--subtitle" && i + 1 < args.Length) StartupSubtitle = File.Exists(args[++i]) ? Path.GetFullPath(args[i]) : null;
            else if (StartupFile == null && File.Exists(args[i])) StartupFile = Path.GetFullPath(args[i]);
        }
        CreateAppBuilder()
            .StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder CreateAppBuilder() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        // Keep the Avalonia owner and transparent controls on the same Windows
        // framebuffer path around VLC's native child window. This changes UI
        // rendering only; VLC retains its own hardware decoding/video output.
        .With(new Win32PlatformOptions
        {
            RenderingMode = [Win32RenderingMode.Software],
            CompositionMode = [Win32CompositionMode.RedirectionSurface]
        })
        .LogToTrace();
}
