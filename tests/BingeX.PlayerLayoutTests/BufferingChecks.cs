using System.Reflection;
using Avalonia.Controls;
using BingeX;
using LibVLCSharp.Shared;

internal static class BufferingChecks
{
    public static void Run(MainWindow window, Action<string> log)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var field = typeof(MainWindow).GetField("playbackSnapshot", flags)!;
        var original = field.GetValue(window)!;
        var simulated = original.GetType().GetMethod("<Clone>$")!.Invoke(original, null)!;
        void Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, args);
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        try
        {
            simulated.GetType().GetProperty("State")!.SetValue(simulated, VLCState.Buffering);
            field.SetValue(window, simulated);
            Call("UpdatePlaybackWaiting");
            Require(window.FindControl<Border>("PlaybackWaiting")!.IsVisible, "Buffering must be acknowledged in full player");
            Call("Page", "Details");
            Require(window.FindControl<Border>("PlaybackWaiting")!.IsVisible, "Mini-player must retain buffering acknowledgement");
            simulated.GetType().GetProperty("State")!.SetValue(simulated, VLCState.Paused);
            Call("UpdatePlaybackWaiting");
            Require(!window.FindControl<Border>("PlaybackWaiting")!.IsVisible, "User pause must clear the waiting indicator");
        }
        finally
        {
            field.SetValue(window, original);
            Call("Page", "Player");
            Call("UpdatePlaybackWaiting");
        }
        log("PASS: buffering indicator in full/mini player and paused-state clearing");
    }
}
