using Avalonia.Interactivity;
using Avalonia.Threading;

namespace BingeX;

public partial class MainWindow
{
    private readonly Dictionary<string, SourcePerformance> sourcePerformance = new();
    private IReadOnlyList<StreamChoice> availableSources = [];
    private MediaContext? availableSourceContext;
    private SourceAttemptPlan? sourcePlan;
    private StreamChoice? activeSource;
    private MediaContext? activeSourceContext;
    private StreamChoice? pendingSource;
    private DateTime sourceLastProgress;
    private double sourceLastPercent;
    private bool sourceStarted;
    private DateTime switchAt;
    private readonly DispatcherTimer sourceTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private void SetupSources()
    {
        sourceTimer.Tick += (_, _) =>
        {
            if (localClosing || NativePlaybackBusy) return;
            if (pendingSource != null)
            {
                var remaining = Math.Max(0, (int)Math.Ceiling((switchAt - DateTime.UtcNow).TotalSeconds));
                SourceSwitchMessage.Text = $"Trying {pendingSource.Provider} · {pendingSource.Name} in {remaining}s";
                if (remaining == 0) TryNextSource_Click(null, new RoutedEventArgs());
                return;
            }
            if (activePage != "Player" || activeSource == null || playbackEnded) return;
            if (player?.Time > 1000 && !torrentStarting) sourceStarted = true;
            if (!sourceStarted && DateTime.UtcNow - sourceLastProgress > TimeSpan.FromSeconds(35))
                TryScheduleNextSource("This source has not delivered playable data.");
        };
        sourceTimer.Start();
    }
    private void BeginSource(StreamChoice choice, MediaContext context, bool fresh = true)
    {
        CancelSourceSwitch();
        if (fresh) sourcePlan = new(availableSourceContext?.HistoryKey == context.HistoryKey ? availableSources : [choice]);
        sourcePlan?.Mark(choice);
        activeSource = choice; activeSourceContext = context;
        sourceStarted = false; sourceLastPercent = 0; sourceLastProgress = DateTime.UtcNow;
        if (choice.Torrent != null) StartTorrent(choice, context);
        else Play(choice.Url, context.DisplayName, context);
    }
    private void SuggestedSource_Click(object? sender, RoutedEventArgs e)
    {
        var first = SourceSelection.Rank(availableSources, sourcePerformance).FirstOrDefault(x => x.UnsupportedReason == null);
        if (first != null && availableSourceContext != null) BeginSource(first, availableSourceContext);
    }
    private void ReportSourceProgress(TorrentStatus update)
    {
        if (activeSource == null) return;
        if (update.HasMetadata && (update.DownloadedPercent > sourceLastPercent || update.MeasuredRate > 0))
            sourceLastProgress = DateTime.UtcNow;
        sourceLastPercent = update.DownloadedPercent;
        if (sourceStarted && update.MeasuredRate > 0) sourcePerformance[SourceSelection.Key(activeSource)] = new(false, update.MeasuredRate);
        // A seek can legitimately wait for a different piece. The torrent service
        // reports a real delivery timeout; a short buffering pause is not failure.
    }
    private bool TryScheduleNextSource(string reason)
    {
        if (pendingSource != null) return true;
        if (localClosing || activeSource == null || activeSourceContext == null || AutoNextSource.IsChecked != true || sourcePlan?.Next is not { } next) return false;
        sourcePerformance[SourceSelection.Key(activeSource)] = new(true, 0);
        SaveProgress();
        StopTorrentPlayback();
        _ = StopNativePlayerAsync();
        Page("Player");
        pendingSource = next;
        switchAt = DateTime.UtcNow.AddSeconds(8);
        TorrentPanel.IsVisible = true; TorrentProgress.IsVisible = false;
        TorrentState.Text = reason;
        SourceSwitchMessage.Text = $"Trying {next.Provider} · {next.Name} in 8s";
        SourceSwitchActions.IsVisible = true;
        Status("Another source is ready. You can cancel the switch.");
        return true;
    }
    private void TryNextSource_Click(object? sender, RoutedEventArgs e)
    {
        var next = pendingSource; var context = activeSourceContext;
        CancelSourceSwitch();
        if (next != null && context != null) BeginSource(next, context, false);
    }
    private void CancelSwitch_Click(object? sender, RoutedEventArgs e)
    {
        ResetSourceAttempts();
        TorrentPanel.IsVisible = false;
        Status("Automatic switching cancelled. Choose a source to continue.");
    }
    private void CancelSourceSwitch() { pendingSource = null; SourceSwitchActions.IsVisible = false; }
    private void ResetSourceAttempts()
    {
        CancelSourceSwitch(); sourcePlan = null; activeSource = null; activeSourceContext = null;
    }
}
