using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BingeX;
internal static class LocalQueueUiChecks
{
    public static void Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object? Get(string n)=>typeof(MainWindow).GetField(n,flags)!.GetValue(window);
        void Set(string n,object? value)=>typeof(MainWindow).GetField(n,flags)!.SetValue(window,value);
        object? Call(string n,params object?[] args)=>typeof(MainWindow).GetMethod(n,flags)!.Invoke(window,args);
        T Control<T>(string n)where T:Control=>window.FindControl<T>(n)!;
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var store=(LibraryStore)Get("store")!;var queue=(LocalPlaybackQueue)Get("localQueue")!;var prefs=store.Preferences();
        var path=Path.Combine(store.DataDirectory,"queue-fixture.mkv");File.WriteAllText(path,"not decoded by this UI check");
        try
        {
            Call("AddQueueFiles",new[]{path,path},false);Call("QueuePage_Click",null,new RoutedEventArgs());
            Check(Control<ScrollViewer>("LocalLibraryPage").IsVisible && Control<Expander>("QueuePanel").IsExpanded && ((StackPanel)Control<ScrollViewer>("LocalLibraryPage").Content!).Children.Contains(Control<Expander>("QueuePanel")) && !Control<ScrollViewer>("AppSettingsPage").IsVisible && queue.Items.Count==1 && Get("currentPath")==null,"Queue opens inside My Library while adding files does not start playback.");
            Call("QueuePage_Click",Control<Button>("LibraryQueueButton"),new RoutedEventArgs());
            Check(!Control<Expander>("QueuePanel").IsExpanded && queue.Items.Count==1,"Library queue button collapses the panel without clearing it.");
            Call("Page","Player"); Call("QueuePage_Click",Control<Button>("QueueOpenButton"),new RoutedEventArgs());
            Check(Control<ScrollViewer>("LocalLibraryPage").IsVisible && Control<Expander>("QueuePanel").IsExpanded,"The player queue action opens the same My Library panel.");
            Control<CheckBox>("QueueAutoplayCheck").IsChecked=true;
            using(var reopened=new LibraryStore(store.DataDirectory))Check(reopened.Preferences().AutoplayLocalQueue,"Local autoplay preference persists independently.");
            var context=new MediaContext("remote-fixture","series","Remote","","fixture","ep1");Set("currentContext",context);
            Check(!(bool)Call("ContinueLocalQueue")! && Get("currentPath")==null,"Online playback cannot trigger local queue autoplay.");
            Set("currentContext",null);Call("ClearQueue_Click",null,new RoutedEventArgs());
            Check(queue.Items.Count==0 && File.Exists(path) && Control<TextBlock>("QueueEmpty").IsVisible,"Clear queue keeps media and updates the empty state.");
        }
        finally
        {
            Set("currentContext",null);queue.Clear();Control<CheckBox>("QueueAutoplayCheck").IsChecked=prefs.AutoplayLocalQueue;Set("preferences",prefs);store.SavePreferences(prefs);Call("Page","Library");
        }
        log("PASS: offline queue page, add without playback, non-destructive clear, persistent local autoplay and online-playback isolation.");
    }
}
