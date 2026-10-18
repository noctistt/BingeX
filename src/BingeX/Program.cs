using Avalonia;

namespace BingeX;

internal static class Program
{
    public static string? StartupFile { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        StartupFile = args.FirstOrDefault(File.Exists);
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
