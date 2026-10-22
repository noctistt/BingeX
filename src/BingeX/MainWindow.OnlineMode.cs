using Avalonia.Controls;

namespace BingeX;

public partial class MainWindow
{
    private bool onlineEnabled;
    private void SetupOnlineMode()
    {
        onlineEnabled = store.OnlineModeEnabled();
        OnlineModeSwitch.IsChecked = onlineEnabled;
        ApplyOnlineMode();
        OnlineModeSwitch.PropertyChanged += async (_, e) =>
        {
            if (e.Property.Name == nameof(OnlineModeSwitch.IsChecked))
                await SetOnlineMode(OnlineModeSwitch.IsChecked == true);
        };
    }
    private async Task SetOnlineMode(bool enabled)
    {
        if (onlineEnabled == enabled) return;
        try { store.SaveOnlineMode(enabled); }
        catch
        {
            OnlineModeSwitch.IsChecked = onlineEnabled;
            Status("Could not save Online mode. Try again.");
            return;
        }
        onlineEnabled = enabled;
        OnlineModeSwitch.IsChecked = enabled;
        if (!enabled)
        {
            CancelEpisodeAutoplay();
            catalogWork.Cancel();
            browseWork.Cancel(); posterWork.Cancel(); detailWork.Cancel(); streamWork.Cancel();
            CancelOnlineSubtitles();
            onlineSubtitleView = false;
            OnlineSubtitleSection.IsVisible = false;
            SubtitleSearchBack.IsVisible = false;
            PlayerSettingsPanel.IsVisible = false;
            CaptionQuickPanel.IsVisible = false;
            BrowseBusy.IsVisible = false;
            if (activePage is "Details" or "Add-ons") Page("Library");
            if (playerReturnPage is "Details" or "Add-ons") playerReturnPage = "Library";
        }
        ApplyOnlineMode();
        RefreshHistory();
        UpdateEpisodeActions();
        Status(enabled ? "Online mode on." : "Online features hidden. Your add-ons and saved data are kept.");
        if (enabled) await RefreshCatalogs();
    }
    private void ApplyOnlineMode()
    {
        ApplyMusicOnlineMode();
        AddonsNav.IsVisible = onlineEnabled;
        DiscoverSection.IsVisible = onlineEnabled;
        ConnectCatalogButton.IsVisible = onlineEnabled;
        OnlineSubtitleButton.IsVisible = onlineEnabled;
        AutoplayEpisodesCheck.IsVisible = onlineEnabled;
        WelcomeHero.IsVisible = !onlineEnabled || ConnectedAddons().Count == 0;
        WelcomeTitle.Text = onlineEnabled ? "Find your next obsession." : "Your videos. Ready to play.";
    }
    private static bool IsLocalHistory(RecentMedia media, MediaContext? context) =>
        context == null && Uri.TryCreate(media.Path, UriKind.Absolute, out var uri) && uri.IsFile &&
        !Path.GetExtension(media.Path).Equals(".torrent", StringComparison.OrdinalIgnoreCase);
}
