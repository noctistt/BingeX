using System.Net;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BingeX;

internal static class DiscoverChecks
{
    public static async Task Run(MainWindow window, Action<string> log)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        object? Call(string method, params object?[] args) => typeof(MainWindow).GetMethod(method, flags)!.Invoke(window, args);
        T Control<T>(string name) where T : Control => window.FindControl<T>(name)!;
        void Require(bool result, string message) { if (!result) throw new Exception(message); }
        void Click(string panel, string label) => Control<WrapPanel>(panel).Children.OfType<Button>()
            .Single(x => x.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var first = new InstalledAddon("one", "Catalog One", "https://one.example/manifest.json");
        var second = new InstalledAddon("two", "Catalog Two", "https://two.example/manifest.json");
        CatalogInfo[] catalogs = [new(first, "popular", "movie", "Popular", true, true), new(first, "new", "movie", "New", false, true),
            new(first, "shows", "series", "Shows", true, true), new(first, "search", "anime", "Anime search", true, false),
            new(second, "popular", "movie", "Popular", true, true)];
        var handler = new CatalogHandler();
        using var client = new AddonClient(handler);
        typeof(MainWindow).GetField("catalogs", flags)!.SetValue(window, catalogs);
        typeof(MainWindow).GetField("library", flags)!.SetValue(window, new MediaLibrary(client));
        Call("RenderDiscoverNavigation", "one", null);
        Require(Control<WrapPanel>("DiscoverAddons").Children.Count == 2, "Provider tabs must group catalogs by add-on.");
        Require(Control<WrapPanel>("DiscoverTypes").Children.Count == 3, "Types must come from the selected provider.");
        Require(Control<ComboBox>("CatalogPicker").Items.Count == 2, "Only selected provider/movie catalogs belong in the picker.");
        Click("DiscoverTypes", "Series");
        await Task.Delay(300);
        Require(Control<ComboBox>("CatalogPicker").SelectedItem is CatalogInfo { Type: "series", Id: "shows" }, "Series must filter catalogs.");
        Click("DiscoverTypes", "Anime");
        Require(!Control<ComboBox>("CatalogPicker").IsVisible, "Search-only catalogs must not be blindly browsed.");
        Control<TextBox>("SearchInput").Text = "a & b";
        await (Task)Call("SearchCatalogs")!;
        Require(handler.Requests.Last().AbsolutePath.Contains("/anime/search/"), "Search must respect the selected provider and type.");
        Call("ClearSearch_Click", null, new RoutedEventArgs());
        Require(Control<WrapPanel>("PosterGrid").Children.Count == 0, "Clearing search-only results must restore the search prompt.");
        Click("DiscoverAddons", "Catalog Two");
        await Task.Delay(300);
        Require(Control<WrapPanel>("DiscoverTypes").Children.Count == 1 && Control<ComboBox>("CatalogPicker").SelectedItem is CatalogInfo { Addon.Id: "two" }, "Switching provider must reset category and catalog.");
        Click("DiscoverAddons", "Catalog One");
        Click("DiscoverAddons", "Catalog Two");
        await Task.Delay(400);
        var card = Control<WrapPanel>("PosterGrid").Children.OfType<Button>().First();
        Require(((StackPanel)card.Content!).Children.OfType<TextBlock>().First().Text == "Catalog Two result", "A cancelled provider response must not overwrite the current tab.");
        log("PASS: Discover provider/type grouping, catalog filtering, scoped search, search-only categories and rapid-switch cancellation");
        if (Environment.GetEnvironmentVariable("BINGEX_DISCOVER_VISUAL_HOLD") == "1")
        {
            Control<Border>("WelcomeHero").IsVisible = false;
            Control<StackPanel>("HistorySection").IsVisible = false;
            Click("DiscoverAddons", "Catalog One");
            Click("DiscoverTypes", "Series");
            await Task.Delay(15000);
        }
    }

    private sealed class CatalogHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri);
            await Task.Delay(uri.Host == "one.example" ? 150 : 20, token);
            var json = JsonSerializer.Serialize(new { metas = new[] { new { id = "fixture", name = uri.Host == "one.example" ? "Catalog One result" : "Catalog Two result", type = "movie" } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
