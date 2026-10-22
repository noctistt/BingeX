using System.Reflection;
using Avalonia.Controls;
using BingeX;
internal static class StorageSettingsChecks
{
    public static async Task Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object? Get(string n)=>typeof(MainWindow).GetField(n,flags)!.GetValue(window);
        object? Call(string n,params object?[] a)=>typeof(MainWindow).GetMethod(n,flags)!.Invoke(window,a);
        T Control<T>(string n)where T:Control=>window.FindControl<T>(n)!;
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var store=(LibraryStore)Get("store")!;var path=Path.Combine(store.DataDirectory,"fixture-local.mkv");
        var thumbnail=store.ThumbnailPath(path);File.WriteAllBytes(thumbnail,new byte[120]);
        var other=store.ThumbnailPath(path+"-other");File.WriteAllBytes(other,new byte[80]);
        var current=Get("currentPath");var context=Get("currentContext");
        typeof(MainWindow).GetField("currentPath",flags)!.SetValue(window,path);typeof(MainWindow).GetField("currentContext",flags)!.SetValue(window,null);
        try
        {
            Call("Page","Settings");
            while((bool)Get("storageBusy")!)await Task.Delay(25);
            Check(Control<TextBlock>("ThumbnailStorageUsage").Text!.Contains("200 bytes"),"Storage page displays generated thumbnail usage.");
            await (Task)Call("UpdateStorage",(object?)true)!;
            Check(File.Exists(thumbnail) && !File.Exists(other) && Control<TextBlock>("StorageMessage").Text!.Contains("1 active"),"Storage cleanup preserves the current local thumbnail and explains retained items.");
            Check(Control<Button>("ClearThumbnailStorageButton").IsEnabled && !Control<ProgressBar>("StorageBusy").IsVisible,"Storage actions recover their enabled state after cleanup.");
        }
        finally
        {
            typeof(MainWindow).GetField("currentPath",flags)!.SetValue(window,current);typeof(MainWindow).GetField("currentContext",flags)!.SetValue(window,context);Call("Page","Library");
        }
        log("PASS: Settings storage totals, independent cleanup, active thumbnail preservation and responsive action states.");
    }
}
