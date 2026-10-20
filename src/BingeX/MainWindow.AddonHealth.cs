using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace BingeX;

public partial class MainWindow
{
    private readonly Dictionary<string, AddonCheck> addonChecks = new();
    private readonly CancellationTokenSource addonCheckLifetime = new();
    private int addonRowsRevision;
    private void RenderInstalledAddons()
    {
        addonRowsRevision++;
        var revision = addonRowsRevision;
        AddonItems.Children.Clear();
        foreach (var addon in store.Addons())
        {
            var column = new StackPanel { Spacing = 10 };
            column.Children.Add(new TextBlock { Text = addon.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brush.Parse("#BBB4CE") };
            void ShowCheck(AddonCheck? check) => status.Text = check == null ? "Not checked on this device." : $"{check.Message} · {check.CheckedAt.ToLocalTime():HH:mm:ss}";
            ShowCheck(addonChecks.GetValueOrDefault(addon.ManifestUrl));
            column.Children.Add(status);
            var actions = new WrapPanel { Orientation = Orientation.Horizontal };
            var browse = new Button { Content = "Browse", Margin = new(0, 0, 8, 0) };
            browse.Click += async (_, _) => { Page("Library"); SearchInput.Text = ""; await RefreshCatalogs(addon.Id); };
            var check = new Button { Content = "Check connection", Margin = new(0, 0, 8, 0) };
            check.Click += async (_, _) =>
            {
                if (addonChecks.TryGetValue(addon.ManifestUrl, out var previous) && DateTime.UtcNow - previous.CheckedAt < TimeSpan.FromSeconds(15))
                { Status("Please wait a few seconds before checking this provider again."); return; }
                check.IsEnabled = false; status.Text = "Checking this device's connection…";
                try
                {
                    var result = await AddonHealth.CheckManifest(addons, addon, addonCheckLifetime.Token);
                    if (localClosing || revision != addonRowsRevision) return;
                    addonChecks[addon.ManifestUrl] = result;
                    ShowCheck(result);
                }
                catch (OperationCanceledException) { }
                finally { if (!localClosing && revision == addonRowsRevision) check.IsEnabled = true; }
            };
            var communityUri = AddonHealth.CommunityPage(addon);
            var community = new Button { Content = communityUri.AbsolutePath == "/addons" ? "Find community reports ↗" : "Community reports ↗", Margin = new(0, 0, 8, 0) };
            community.Click += async (_, _) =>
            {
                try { if (!await Launcher.LaunchUriAsync(communityUri)) Status("The community page could not be opened in your browser."); }
                catch { Status("The community page could not be opened in your browser."); }
            };
            var remove = new Button { Content = "Remove" };
            remove.Click += (_, _) => { store.RemoveAddon(addon.Id); addonChecks.Remove(addon.ManifestUrl); library.Invalidate(); catalogs = []; RefreshAddons(); Status("Removed " + addon.Name); };
            actions.Children.Add(browse); actions.Children.Add(check); actions.Children.Add(community); actions.Children.Add(remove);
            column.Children.Add(actions);
            column.Children.Add(new TextBlock { Text = "Community reports: stremio-addons.net", Foreground = Brush.Parse("#968BA8"), FontSize = 11 });
            AddonItems.Children.Add(new Border { Background = Brush.Parse("#211D2C"), CornerRadius = new(12), Padding = new(16), Child = column });
        }
    }
}
