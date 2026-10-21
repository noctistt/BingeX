using System.Reflection;
using Avalonia.Controls;
using BingeX;

internal static class OnlineModeChecks
{
    public static async Task Run(MainWindow window, Action<string> log)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var store = (LibraryStore)typeof(MainWindow).GetField("store", flags)!.GetValue(window)!;
        T Control<T>(string name) where T : Control => window.FindControl<T>(name)!;
        object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window,args);
        Task Set(bool enabled) => (Task)Call("SetOnlineMode",enabled)!;
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        Require(!Control<ToggleSwitch>("OnlineModeSwitch").IsChecked.GetValueOrDefault() && !store.OnlineModeEnabled(), "A fresh profile must start with Online mode off.");
        Require(!Control<StackPanel>("DiscoverSection").IsVisible && !Control<Button>("AddonsNav").IsVisible &&
            !Control<Button>("OnlineSubtitleButton").IsVisible, "Online entry points must be hidden by default.");
        Require(Control<Button>("LocalLibraryNav").IsVisible && Control<Button>("PlayerNav").IsVisible, "Local navigation remains available.");
        await Set(true);
        Require(Control<StackPanel>("DiscoverSection").IsVisible && Control<Button>("AddonsNav").IsVisible &&
            Control<Button>("OnlineSubtitleButton").IsVisible, "Enabling restores online features.");
        using(var reopened = new LibraryStore(store.DataDirectory)) Require(reopened.OnlineModeEnabled(), "Enabled preference survives reopening the database.");
        const string local = @"C:\fixture\local-mode.mkv";
        var context = new MediaContext("online-mode-fixture", "movie", "Online fixture", "", "mode-fixture");
        store.Save(new(local,"Local fixture",15000,90000));
        store.SaveContext(context); store.Save(new(context.HistoryKey,context.DisplayName,15000,90000));
        store.Install(new("mode-fixture","Fixture","https://example.invalid/manifest.json"));
        Call("RefreshHistory");
        Require(Control<StackPanel>("HistoryItems").Children.Count == 2, "Online mode includes both local and online history.");
        Call("Page","Add-ons");
        await Set(false);
        Require(Control<ScrollViewer>("LibraryPage").IsVisible && !Control<ScrollViewer>("AddonsPage").IsVisible, "Disabling leaves a hidden online page cleanly.");
        Require(Control<StackPanel>("HistoryItems").Children.Count == 1, "Local view hides online history.");
        Require(store.Addons().Any(x=>x.Id=="mode-fixture") && store.FindHistory(context.HistoryKey)!=null, "Disabling keeps installed add-ons and online history.");
        using(var reopened = new LibraryStore(store.DataDirectory)) Require(!reopened.OnlineModeEnabled(), "Disabled preference survives reopening the database.");
        Call("Page","Details");
        Require(!Control<ScrollViewer>("DetailsPage").IsVisible, "Hidden online pages cannot be reopened by navigation.");
        store.RemoveAddon("mode-fixture");
        await Set(true);
        Require(Control<StackPanel>("HistoryItems").Children.Count == 2, "Online history returns when enabled again.");
        store.RemoveHistory(local); store.RemoveHistory(context.HistoryKey); Call("RefreshHistory");
        log("PASS: Online mode defaults off, persists both states, hides/restores online features and history, keeps provider data and redirects hidden pages.");
    }
}
