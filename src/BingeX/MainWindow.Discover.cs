using Avalonia;
using Avalonia.Controls;

namespace BingeX;

public partial class MainWindow
{
    private string? discoverAddon;
    private string? discoverType;
    private IReadOnlyList<CatalogInfo> DiscoverCatalogs => catalogs.Where(x => x.Addon.Id == discoverAddon && x.Type == discoverType).ToArray();

    private void RenderDiscoverNavigation(string? preferredAddon = null, CatalogInfo? previous = null)
    {
        var providers = catalogs.Select(x => x.Addon).DistinctBy(x => x.Id).ToArray();
        discoverAddon = providers.FirstOrDefault(x => x.Id == (preferredAddon ?? discoverAddon ?? previous?.Addon.Id))?.Id ?? providers.FirstOrDefault()?.Id;
        DiscoverAddons.Children.Clear();
        foreach (var addon in providers)
        {
            var button = new Button { Content = addon.Name, Margin = new Thickness(0, 0, 8, 8) };
            button.Classes.Set("primary", addon.Id == discoverAddon);
            button.Click += async (_, _) =>
            {
                if (discoverAddon == addon.Id) return;
                discoverAddon = addon.Id; discoverType = null; SearchInput.Text = "";
                RenderDiscoverNavigation();
                await LoadDiscoverSelection();
            };
            DiscoverAddons.Children.Add(button);
        }
        var types = catalogs.Where(x => x.Addon.Id == discoverAddon).Select(x => x.Type).Distinct().ToArray();
        discoverType = types.Contains(discoverType) ? discoverType : types.FirstOrDefault();
        DiscoverTypes.Children.Clear();
        foreach (var type in types)
        {
            var label = type switch { "movie" => "Movies", "series" => "Series", "anime" => "Anime", _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(type) };
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 4) };
            button.Classes.Set("primary", type == discoverType);
            button.Click += async (_, _) =>
            {
                if (discoverType == type) return;
                discoverType = type; SearchInput.Text = "";
                RenderDiscoverNavigation();
                await LoadDiscoverSelection();
            };
            DiscoverTypes.Children.Add(button);
        }
        changingCatalog = true;
        try
        {
            var selected = previous ?? CatalogPicker.SelectedItem as CatalogInfo;
            var choices = DiscoverCatalogs.Where(x => x.Browsable).ToArray();
            CatalogPicker.ItemsSource = choices;
            CatalogPicker.SelectedItem = choices.FirstOrDefault(x => x.Addon.Id == selected?.Addon.Id && x.Type == selected.Type && x.Id == selected.Id) ?? choices.FirstOrDefault();
            CatalogPicker.IsVisible = choices.Length > 0;
        }
        finally { changingCatalog = false; }
        DiscoverAddons.IsVisible = providers.Length > 0;
        DiscoverTypes.IsVisible = types.Length > 0;
        SearchInput.Watermark = "Search this add-on…";
        DiscoverHint.Text = providers.Length > 0 ? "Choose an add-on, then Movies, Series or another available category." : "Add a catalog provider to browse here. Stream-only add-ons appear when you choose a title.";
    }

    private async Task LoadDiscoverSelection()
    {
        var token = BeginBrowse();
        if (CatalogPicker.SelectedItem is CatalogInfo catalog) await ShowCatalog(catalog, token);
        else
        {
            BrowseLoading("Loading catalogs…");
            BrowseNotice(DiscoverCatalogs.Any(x => x.Searchable) ? "This category supports search. Enter a title above." : "This add-on has no browsing catalogs in this category.");
        }
    }
}
