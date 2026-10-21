using System.Net;
using System.Reflection;
using Avalonia.Controls;
using BingeX;

internal static class ExtensionSubtitleChecks
{
    public static async Task Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        object? Get(string name) => typeof(MainWindow).GetField(name,flags)!.GetValue(window);
        void Set(string name,object? value) => typeof(MainWindow).GetField(name,flags)!.SetValue(window,value);
        void Call(string name,params object?[] args) => typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,args);
        var original = Get("subtitleDownloads");
        using var handler = new SubtitleHandler();
        using var downloader = new OnlineSubtitles(handler);
        try
        {
            Set("subtitleDownloads",downloader);
            Set("currentStreamSubtitles",new SubtitleChoice[] { new("Extension","fixture","en","English fixture","https://fixture.example/subtitle",new Dictionary<string,string>{{"Origin","https://provider.example"}}) });
            Call("RefreshTracks");
            var picker = window.FindControl<ComboBox>("SubtitleTrackPicker")!;
            var choice = picker.Items.Cast<object>().Single(x => x.ToString()!.Contains("Extension · English fixture"));
            picker.SelectedItem = choice;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow < deadline && !(bool)Get("useTextSubtitles")!) await Task.Delay(25);
            if (!(bool)Get("useTextSubtitles")! || ((SubtitleDocument?)Get("textSubtitles"))?.Cues.Count != 1 || !handler.HeaderSeen)
                throw new Exception("Extension subtitle selection did not load and activate the header-protected caption.");
            var previousContext=(MediaContext)Get("currentContext")!;
            var rememberedStore=(LibraryStore)Get("store")!;
            if(rememberedStore.SubtitlePreference(previousContext)?.Language!="english") throw new Exception("Manual extension caption selection did not save language preference.");
            Set("currentContext",previousContext with{EpisodeId="next-episode-fixture"});
            Set("currentStreamSubtitles",new SubtitleChoice[]{new("Extension","next-caption","eng","English next episode","https://fixture.example/subtitle-next",new Dictionary<string,string>{{"Origin","https://provider.example"}})});
            Set("useTextSubtitles",false); Set("textSubtitles",null); Set("restoredSubtitleGeneration",-1L);
            window.FindControl<Border>("PlayerSettingsPanel")!.IsVisible=false;
            Call("RestoreSubtitlePreference");
            deadline=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<deadline && !(bool)Get("useTextSubtitles")!) await Task.Delay(25);
            if(!(bool)Get("useTextSubtitles")! || handler.Requests<2 || handler.LastPath!="/subtitle-next" || window.FindControl<Border>("PlayerSettingsPanel")!.IsVisible) throw new Exception("Next episode must download matching captions automatically without opening settings.");
            Set("currentContext",previousContext);
            log("PASS: subtitle preference automatically selects and downloads the next episode caption without opening settings");
            Call("Captions_Click",null,new Avalonia.Interactivity.RoutedEventArgs());
            var quick=window.FindControl<ComboBox>("QuickSubtitleTrackPicker")!;
            var beforeQuickRequests=handler.Requests;
            Set("useTextSubtitles",false);
            quick.SelectedItem=quick.Items.Cast<object>().Single(x=>x.ToString()!.Contains("Extension · English next episode"));
            deadline=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<deadline && (!(bool)Get("useTextSubtitles")! || handler.Requests==beforeQuickRequests)) await Task.Delay(25);
            if(!(bool)Get("useTextSubtitles")! || handler.Requests==beforeQuickRequests || window.FindControl<Border>("PlayerSettingsPanel")!.IsVisible || !window.FindControl<Border>("CaptionQuickPanel")!.IsVisible)
                throw new Exception("Choosing online captions from CC must remain in the quick picker.");
            Call("CloseCaptionQuick_Click",null,new Avalonia.Interactivity.RoutedEventArgs());
            await PlayerPolishChecks.Run(window,log);
            log("PASS: extension captions appear in CC picker and selecting one loads header-protected text subtitles");
        }
        finally
        {
            Set("subtitleDownloads",original); Set("currentStreamSubtitles",Array.Empty<SubtitleChoice>());
            Set("useTextSubtitles",false); Set("textSubtitles",null); Set("trackSignature","");
            window.FindControl<Border>("PlayerSettingsPanel")!.IsVisible = false;
            Call("RefreshTracks");
        }
    }
    private sealed class SubtitleHandler : HttpMessageHandler
    {
        public bool HeaderSeen;
        public int Requests;
        public string LastPath="";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Requests++; LastPath=request.RequestUri!.AbsolutePath;
            HeaderSeen = request.Headers.TryGetValues("Origin",out var values) && values.Single() == "https://provider.example";
            return Task.FromResult(new HttpResponseMessage(HeaderSeen ? HttpStatusCode.OK : HttpStatusCode.Forbidden) { Content = new StringContent("1\n00:00:00,000 --> 00:00:04,000\nExtension caption\n") });
        }
    }
}
