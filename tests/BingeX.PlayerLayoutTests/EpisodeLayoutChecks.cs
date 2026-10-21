using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using BingeX;

internal static class EpisodeLayoutChecks
{
    public static async Task Run(MainWindow window, Action<string> log)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        object? Call(string method, params object?[] args) => typeof(MainWindow).GetMethod(method,flags)!.Invoke(window,args);
        T Control<T>(string name) where T : Control => window.FindControl<T>(name)!;
        void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
        var episodes = Enumerable.Range(1,500).Select(i=>new EpisodeInfo("entry-"+i,"Video "+i,1,i,"Full episode overview with useful information that must remain readable in the episode card.")).ToArray();
        var title = new TitleInfo("channel-fixture","movie","Channel listed as a movie","","","Full title synopsis remains available.","2026","","","fixture",episodes);
        typeof(MainWindow).GetField("selectedTitle",flags)!.SetValue(window,title);
        Call("RenderTitle",title,CancellationToken.None);
        Call("SetEpisodeLayout",true);
        var season = Activator.CreateInstance(typeof(MainWindow).GetNestedType("SeasonOption",BindingFlags.NonPublic)!,new object[]{1})!;
        Control<ComboBox>("SeasonPicker").ItemsSource = new[]{season};
        Control<ComboBox>("SeasonPicker").SelectedItem = season;
        Call("Page","Details");
        Require(Control<WrapPanel>("EpisodeItems").Children.Count==500 && !Control<Button>("MovieSourcesButton").IsVisible,"Movie-labelled channels must retain their episode entries.");
        await (Task)Call("SelectEpisode",episodes[200])!;
        var context = new MediaContext(title.Id,title.Type,title.Name,"","fixture",episodes[200].Id,episodes[200].Name);
        var sources = Enumerable.Range(1,80).Select(i=>new StreamChoice("Fixture","Source "+i,"Full stream description with codec, language, file size and release details.","https://example.invalid/video"+i,null)).ToArray();
        Call("RenderSourceChoices",sources,context);
        var priorState=window.WindowState; var priorWidth=window.Width; var priorHeight=window.Height;
        try
        {
            window.WindowState=WindowState.Normal;
            foreach(var size in new[]{(Width:960,Height:680),(Width:1240,Height:820)})
            {
                window.Width=size.Width; window.Height=size.Height;
                await Task.Delay(300);
                var eps=Control<ScrollViewer>("EpisodeScroller"); var streams=Control<ScrollViewer>("StreamScroller");
                Require(eps.Viewport.Height>80 && streams.Viewport.Height>80,"Both panes need usable bounded viewports.");
                Require(eps.Extent.Height>eps.Viewport.Height && streams.Extent.Height>streams.Viewport.Height,"Long episode and stream lists must scroll independently.");
                var left=eps.TranslatePoint(default,window)!.Value; var right=streams.TranslatePoint(default,window)!.Value;
                Require(right.X>=left.X+eps.Bounds.Width && Math.Abs(left.Y-right.Y)<100,"Streams remain beside episodes.");
                eps.Offset=new Vector(0,eps.Extent.Height);
                await Task.Delay(100);
                Require(eps.Offset.Y>0 && streams.Offset.Y==0,"Scrolling to the last episode must not move the streams.");
                Require(((Button)Control<WrapPanel>("EpisodeItems").Children[200]).Classes.Contains("primary"),"The chosen episode remains highlighted.");
            }
            Call("SetEpisodeLayout",false); await Task.Delay(100);
            Require(Control<ScrollViewer>("StreamScroller").Bounds.Width>Control<Grid>("TitleContentGrid").Bounds.Width*.9,"Movies without entries give streams the full width.");
        }
        finally
        {
            window.Width=priorWidth; window.Height=priorHeight; window.WindowState=priorState;
            Call("SetEpisodeLayout",false); Call("Page","Library");
        }
        log("PASS: 500 episode entries, movie-labelled channels, independent episode/stream scrolling at minimum and normal size, selected highlight and full-width movie streams.");
    }
}
