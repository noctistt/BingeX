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
        var contextField=typeof(MainWindow).GetField("currentContext",flags)!;
        var prior=contextField.GetValue(window);
        var context=new MediaContext("show-audio-fixture","series","Fixture","","fixture","episode-2");
        contextField.SetValue(window,context);
        var store=(LibraryStore)typeof(MainWindow).GetField("store",flags)!.GetValue(window)!;
        store.SaveShowAudioPreference(context with{EpisodeId="episode-1"},new("hindi","Hindi"));
        pending.SetValue(window,null);
        StreamAudioOption[] next=[new("https://example.com/next-ja","Japanese","ja",true),new("https://example.com/next-hi","Hindi new label","hi",false)];
        selected=method.Invoke(window,[next,next[0].Url]);
        if((string?)selected!=next[1].Url)throw new Exception("The next episode must choose its own Hindi audio URL from the show's saved language.");
        contextField.SetValue(window,prior);
        method.Invoke(window, [null, null]);
        if (window.FindControl<StackPanel>("StreamAudioSection")!.IsVisible || picker.ItemCount != 0)
            throw new Exception("Changing to local media must clear stream languages.");
        log("PASS: stream audio selector, saved language restoration and local-media clearing");
    }
}
