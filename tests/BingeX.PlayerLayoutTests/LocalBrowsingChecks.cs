using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using BingeX;
internal static class LocalBrowsingChecks
{
    public static async Task Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object? Get(string n)=>typeof(MainWindow).GetField(n,flags)!.GetValue(window);
        void Set(string n,object? v)=>typeof(MainWindow).GetField(n,flags)!.SetValue(window,v);
        object? Call(string n,params object?[] args)=>typeof(MainWindow).GetMethod(n,flags)!.Invoke(window,args);
        T Control<T>(string n) where T:Control=>window.FindControl<T>(n)!;
        void Require(bool value,string message){if(!value)throw new Exception(message);}
        var store=(LibraryStore)Get("store")!; var saved=store.Preferences(); var thumbnails=Get("thumbnailRunning");
        var root=LocalLibraryScanner.NormalizeFolder(Path.Combine(store.DataDirectory,"browsing-fixture"));
        LocalVideo Video(string name,string relative)=>new(Path.Combine(root,relative),root,name,relative,100,0);
        var videos=new[]{Video("My.Show.S01E10",Path.Combine("My Show","Season 1","ten.mkv")),Video("My.Show.S01E02",Path.Combine("My Show","Season 1","two.mkv")),Video("A movie","movie.mkv")};
        store.AddFolder(root); store.ApplyFolderScan(new(root,videos,true,0)); Set("thumbnailRunning",true);
        try
        {
            Call("RefreshLocalFolders",root); Call("Page","My Library");
            Require(!(bool)Get("onlineEnabled")!,"Local browsing checks run with online mode disabled.");
            Control<ComboBox>("LocalGrouping").SelectedIndex=1; await Task.Delay(100);
            var sections=Control<StackPanel>("LocalVideoItems").Children.OfType<Expander>().ToArray();
            Require(sections.Length==2 && sections.Any(x=>x.Header!.ToString()!.Contains("My Show · Season 1")),"Local series are grouped separately from loose movies: "+string.Join(" / ",sections.Select(x=>x.Header))+" count "+Control<TextBlock>("LocalVideoCount").Text);
            sections[0].IsExpanded=false; Call("RefreshLocalLibrary");
            Require(!Control<StackPanel>("LocalVideoItems").Children.OfType<Expander>().First().IsExpanded,"Collapsed groups stay collapsed during a refresh.");
            Control<TextBox>("LocalVideoSearch").Text="my show s01e02"; await Task.Delay(300);
            Require(Control<TextBlock>("LocalVideoCount").Text!.StartsWith("1 video"),"Local search matches dotted filenames with separate words.");
            Call("ClearLocalSearch_Click",null,new RoutedEventArgs());
            Require(Control<TextBlock>("LocalVideoCount").Text!.StartsWith("3 videos"),"Clear search restores the indexed library.");
            Control<ComboBox>("LocalGrouping").SelectedIndex=2; Control<ComboBox>("LocalSort").SelectedIndex=2; await Task.Delay(100);
            Require(Control<StackPanel>("LocalVideoItems").Children.OfType<WrapPanel>().Single().Children.OfType<Button>().Count()==3,"All videos removes group boundaries without losing cards.");
            using(var reopened=new LibraryStore(store.DataDirectory))Require(reopened.Preferences().LocalLibrarySort==2 && reopened.Preferences().LocalLibraryGrouping==2,"Local view and sort preferences survive reopening.");
            Control<TextBox>("LocalVideoSearch").Text="no such video"; await Task.Delay(300);
            Require(Control<TextBlock>("LocalLibraryEmpty").IsVisible && store.LocalVideos(root).Count==3,"An empty search must not remove indexed files.");
        }
        finally
        {
            Control<TextBox>("LocalVideoSearch").Text=""; Control<ComboBox>("LocalSort").SelectedIndex=saved.LocalLibrarySort; Control<ComboBox>("LocalGrouping").SelectedIndex=saved.LocalLibraryGrouping;
            store.SavePreferences(saved); Set("preferences",saved); store.RemoveFolder(root);
            Call("RefreshLocalFolders",(object?)null); Call("Page","Library"); Set("thumbnailRunning",thumbnails);
        }
        log("PASS: offline local search, clear, folder/series/all views, collapsible groups and persisted sort/view settings.");
    }
}
