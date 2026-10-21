using System.Reflection;
using Avalonia.Controls;
using BingeX;

internal static class StreamAudioChecks
{
    public static void Run(MainWindow window, Action<string> log)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = typeof(MainWindow).GetMethod("SetupStreamAudio", flags)!;
        var pending = typeof(MainWindow).GetField("pendingAudioSelection", flags)!;
        StreamAudioOption[] options = [new("https://example.com/en", "English · Original", "en", true), new("https://example.com/hi", "Hindi", "hi", false)];
        pending.SetValue(window, new AudioTrackSelection(-2, "Hindi"));
        var selected = method.Invoke(window, [options, options[0].Url]);
        var picker = window.FindControl<ComboBox>("StreamAudioPicker")!;
        if ((string?)selected != options[1].Url || picker.SelectedItem as StreamAudioOption != options[1] || picker.ItemCount != 2 || pending.GetValue(window) != null)
            throw new Exception("Saved stream language must restore separately from native track IDs.");
        method.Invoke(window, [null, null]);
        if (window.FindControl<StackPanel>("StreamAudioSection")!.IsVisible || picker.ItemCount != 0)
            throw new Exception("Changing to local media must clear stream languages.");
        log("PASS: stream audio selector, saved language restoration and local-media clearing");
    }
}
