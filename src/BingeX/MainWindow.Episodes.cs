using Avalonia.Interactivity;

namespace BingeX;

public partial class MainWindow
{
    private TitleInfo? playingEpisodeTitle;
    private bool nextEpisodeBusy;
    private bool episodePreparationCancelled;
    private DateTime? nextEpisodeAt;
    private TitleInfo? cachedEpisodeTitle;
    private MediaContext? cachedEpisodeContext;
    private EpisodeInfo? cachedNextEpisode;
    private EpisodeInfo? NextEpisode
    {
        get
        {
            if (!ReferenceEquals(cachedEpisodeTitle,playingEpisodeTitle) || !ReferenceEquals(cachedEpisodeContext,currentContext))
            {
                cachedEpisodeTitle = playingEpisodeTitle; cachedEpisodeContext = currentContext;
                cachedNextEpisode = EpisodeNavigation.Next(playingEpisodeTitle,currentContext);
            }
            return cachedNextEpisode;
        }
    }
    private void SetupEpisodeNavigation()
    {
        AutoplayEpisodesCheck.IsChecked = preferences.AutoplayEpisodes;
        AutoplayEpisodesCheck.PropertyChanged += (_,e) =>
        {
            if (e.Property.Name != nameof(AutoplayEpisodesCheck.IsChecked)) return;
            preferences = preferences with { AutoplayEpisodes = AutoplayEpisodesCheck.IsChecked == true };
            store.SavePreferences(preferences);
            if (!preferences.AutoplayEpisodes) CancelEpisodeAutoplay();
        };
        UpdateEpisodeActions();
    }
    private void UpdateEpisodeActions()
    {
        var next = onlineEnabled ? NextEpisode : null;
        NextEpisodeButton.IsVisible = next != null;
        NextEpisodeButton.IsEnabled = !nextEpisodeBusy && !NativePlaybackBusy && !playbackStarting;
        var tip = next == null ? "Next episode" : $"Next: {next.Name}";
        if (Avalonia.Controls.ToolTip.GetTip(NextEpisodeButton) as string != tip) Avalonia.Controls.ToolTip.SetTip(NextEpisodeButton,tip);
        if (nextEpisodeAt is not { } at) return;
        if (next == null || !preferences.AutoplayEpisodes || !playbackEnded || localClosing)
        { CancelEpisodeAutoplay(); return; }
        var remaining = Math.Max(0,(int)Math.Ceiling((at-DateTime.UtcNow).TotalSeconds));
        EpisodeAutoplayText.Text = $"Next: {next.Name} · {remaining}s";
        MiniEpisodeAutoplayText.Text = EpisodeAutoplayText.Text;
        if (remaining == 0) { CancelEpisodeAutoplay(); _ = StartNextEpisode(); }
    }
    private void CancelEpisodeAutoplay()
    {
        nextEpisodeAt = null;
        EpisodeAutoplayPanel.IsVisible = false;
        MiniEpisodeAutoplayPanel.IsVisible = false;
    }
    private void ScheduleEpisodeAutoplay()
    {
        if (!onlineEnabled || !preferences.AutoplayEpisodes || NextEpisode == null || nextEpisodeBusy) return;
        nextEpisodeAt = DateTime.UtcNow.AddSeconds(8);
        EpisodeAutoplayPanel.IsVisible = true;
        MiniEpisodeAutoplayPanel.IsVisible = true;
        UpdateEpisodeActions();
    }
    private void CancelEpisodeAutoplay_Click(object? sender,RoutedEventArgs e) => CancelEpisodeAutoplay();
    private async void NextEpisode_Click(object? sender,RoutedEventArgs e) => await StartNextEpisode();
    private void CancelEpisodePreparation_Click(object? sender,RoutedEventArgs e)
    {
        episodePreparationCancelled=true;
        streamWork.Cancel();
        EpisodePreparingPanel.IsVisible=false;
        DetailBusy.IsVisible=false;
        DetailMessage.Text="Episode preparation cancelled. Choose a source below or retry streams.";
        DetailRetry.IsVisible=true;
    }
    private async Task StartNextEpisode()
    {
        if (nextEpisodeBusy || NativePlaybackBusy || playbackStarting || !onlineEnabled || NextEpisode is not { } next || playingEpisodeTitle is not { } title) return;
        CancelEpisodeAutoplay();
        nextEpisodeBusy = true;
        episodePreparationCancelled=false;
        var generation = mediaGeneration;
        try
        {
            SaveProgress();
            selectedTitle = title;
            Page("Details");
            RenderTitle(title,CancellationToken.None);
            SetEpisodeLayout(true);
            SeasonPicker.ItemsSource = title.Episodes.Select(x=>x.Season).Distinct().Select(x=>new SeasonOption(x)).ToArray();
            SeasonPicker.SelectedItem = ((SeasonOption[])SeasonPicker.ItemsSource).First(x=>x.Number==next.Season);
            SetSelectedEpisode(next);
            EpisodePreparingTitle.Text=selectedContext!.DisplayName;
            EpisodePreparingPanel.IsVisible=true;
            await FindStreamsProgressive(context =>
            {
                if(episodePreparationCancelled || localClosing || !onlineEnabled || generation!=mediaGeneration || activePage!="Details" || context.Id!=title.Id || context.EpisodeId!=next.Id) return;
                var first=PlaybackSources.FirstOrDefault(x=>x.UnsupportedReason==null);
                if(first!=null) BeginSource(first,context);
            });
            if (episodePreparationCancelled || localClosing || !onlineEnabled || generation != mediaGeneration || activePage != "Details" || selectedContext?.Id != title.Id || selectedContext.EpisodeId != next.Id) return;
            var source = availableSourceContext?.HistoryKey == selectedContext.HistoryKey
                ? PlaybackSources.FirstOrDefault(x=>x.UnsupportedReason == null) : null;
            if (source != null) BeginSource(source,selectedContext);
            else Status("No playable source found for the next episode. Choose or retry a source on this page.");
        }
        catch { if (!localClosing) Status("Could not open the next episode. Choose an episode or retry its streams."); }
        finally { EpisodePreparingPanel.IsVisible=false; nextEpisodeBusy = false; UpdateEpisodeActions(); }
    }
}
