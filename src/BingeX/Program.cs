using Avalonia;

namespace BingeX;

internal static class Program
{
    public static string? StartupFile { get; private set; }
    public static string? StartupSubtitle { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--subtitle" && i + 1 < args.Length) StartupSubtitle = File.Exists(args[++i]) ? Path.GetFullPath(args[i]) : null;
            else if (StartupFile == null && File.Exists(args[i])) StartupFile = Path.GetFullPath(args[i]);
        }
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
