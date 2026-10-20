using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace BingeX;

public partial class MainWindow
{
    private bool miniPlayer;
    private void UpdatePlayerLayout()
    {
        miniPlayer = activePage != "Player" && currentPath != null;
        PlayerPage.IsVisible = activePage == "Player" || miniPlayer;
        VideoSurface.IsVisible = PlayerPage.IsVisible;
        PlayerPage.Width = miniPlayer ? 360 : double.NaN;
        PlayerPage.Height = miniPlayer ? 244 : double.NaN;
        PlayerPage.HorizontalAlignment = miniPlayer ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Stretch;
        PlayerPage.VerticalAlignment = miniPlayer ? Avalonia.Layout.VerticalAlignment.Bottom : Avalonia.Layout.VerticalAlignment.Stretch;
        PlayerPage.Margin = miniPlayer ? new Thickness(12) : new Thickness(0);
        PlayerPage.ZIndex = miniPlayer ? 10 : 0;
        VideoFrame.BorderThickness = new Thickness(miniPlayer ? 1 : 0);
        VideoFrame.BorderBrush = Brush.Parse("#615078");
        MiniControls.IsVisible = miniPlayer;
        PlayerControls.IsVisible = !miniPlayer;
        if (miniPlayer)
        {
            PlayerSettingsPanel.IsVisible = false;
            TorrentPanel.IsVisible = false;
            PlaybackNotice.IsVisible = false;
            PlayerOverlay.Cursor = Avalonia.Input.Cursor.Default;
        }
        MiniTitle.Text = currentTitle;
        ApplySubtitleAppearance();
        UpdatePlayerOverlay();
    }
    private void MinimizePlayer_Click(object? sender, RoutedEventArgs e) => Page(playerReturnPage == "Player" ? "Library" : playerReturnPage);
    private void RestorePlayer_Click(object? sender, RoutedEventArgs e) => Page("Player");
    private async void CloseMiniPlayer_Click(object? sender, RoutedEventArgs e)
    {
        videoClickTimer.Stop(); SaveProgress(); ResetSourceAttempts();
        var request = ++playbackRequest; playbackStarting = false;
        StopTorrentPlayback();
        var stopped = StopNativePlayerAsync();
        await stopped;
        if (localClosing || request != playbackRequest) return;
        currentPath = null; currentContext = null;
        PlayerEmpty.IsVisible = true;
        CaptionPanel.IsVisible = false;
        UpdatePlayerLayout();
        if (!localClosing) Status("Playback stopped. Your progress is saved.");
    }
}
