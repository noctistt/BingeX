using Avalonia.Interactivity;
using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private TorrentPlayback? torrentSession;
    private CancellationTokenSource torrentRequest = new();
    private Task torrentCleanup = Task.CompletedTask;
    private int torrentRevision;
    private bool torrentStarting;
    private bool torrentCloseReady;
    private bool torrentCloseInProgress;
    private StreamChoice? lastTorrentChoice;
    private MediaContext? lastTorrentContext;

    private void StartTorrentFile(string path)
    {
        try
        {
            ResetSourceAttempts();
            var source = TorrentSource.FromFile(path);
            var title = Path.GetFileNameWithoutExtension(path);
            StartTorrent(new("Local torrent", title, "", "", null, source), new(Path.GetFullPath(path), "torrent-file", title, "", "local-torrent"));
        }
        catch (Exception error) { Status(error is IOException ? error.Message : "This torrent file could not be read."); }
    }
    private async void StartTorrent(StreamChoice choice, MediaContext context)
    {
        if (choice.Torrent == null || player == null || engine == null) return;
        SaveProgress();
        playbackRequest++; playbackStarting = false;
        StopTorrentPlayback();
        var stop = StopNativePlayerAsync();
        currentPath = null;
        thumbnailWork.Cancel();
        lastTorrentChoice = choice; lastTorrentContext = context;
        torrentRequest.Dispose(); torrentRequest = new();
        var token = torrentRequest.Token;
        var revision = torrentRevision;
        torrentStarting = true;
        Page("Player");
        PlayerEmpty.IsVisible = false;
        NowPlaying.Text = context.DisplayName;
        TorrentPanel.IsVisible = true;
        TorrentProgress.IsVisible = true;
        TorrentProgress.IsIndeterminate = true;
        TorrentBufferDetails.Text = "";
        TorrentState.Text = "Finding peers and torrent information…";
        TorrentDetails.Text = "Saved video cache: up to 32 GB · upload limit: 256 KB/s";
        Status("Preparing torrent playback…");
        try
        {
            await stop;
            await torrentCleanup;
            token.ThrowIfCancellationRequested();
            var dataDirectory = store.DataDirectory;
            var session = await Task.Run(() =>
            {
                TorrentPlayback.CleanOldSessions(dataDirectory);
                return new TorrentPlayback(dataDirectory, infoHash: choice.Torrent.InfoHash);
            }, token);
            if (token.IsCancellationRequested || revision != torrentRevision) { await session.DisposeAsync(); return; }
            torrentSession = session;
            var progress = new Progress<TorrentStatus>(update =>
            {
                if (revision != torrentRevision || localClosing) return;
                ReportSourceProgress(update);
                if (pendingSource != null) return;
                TorrentState.Text = update.Stage;
                TorrentDetails.Text = update.HasMetadata
                    ? $"{update.DownloadedPercent:0.0}% downloaded · {update.DownloadRate / 1024d:0} KB/s · {update.Peers} peer(s) · cache limit 32 GB"
                    : $"{update.ElapsedSeconds}s elapsed · {update.Peers} peer(s) · {update.Discovery}";
                TorrentBufferDetails.Text = update.HasMetadata
                    ? update.StartupTarget > 0 && torrentStarting
                        ? $"Startup buffer: {update.StartupBuffered / 1048576d:0.0} / {update.StartupTarget / 1048576d:0.0} MB"
                        : update.DownloadedPercent >= 100 ? "Video fully cached · seeking reads from disk" : (update.BufferedSeconds is { } seconds ? $"≈ {seconds:0}s of data ahead (estimate)" : $"{update.BufferedBytes / 1048576d:0.0} MB verified ahead") +
                          (update.RequiredRate is { } required ? $" · average needed ≈ {required / 1048576d:0.00} MB/s" : "") +
                          $" · measured {update.MeasuredRate / 1048576d:0.00} MB/s"
                    : "Download progress begins after torrent information arrives.";
                TorrentProgress.IsIndeterminate = update.StartupTarget <= 0 || !torrentStarting;
                if (!TorrentProgress.IsIndeterminate) { TorrentProgress.Maximum = update.StartupTarget; TorrentProgress.Value = update.StartupBuffered; }
                TorrentProgress.IsVisible = torrentStarting || update.Waiting;
            });
            await Task.Run(() => session.OpenAsync(choice.Torrent, progress, token, durationSeconds: context.DurationSeconds), token);
            token.ThrowIfCancellationRequested();
            if (revision != torrentRevision) return;
            torrentStarting = false;
            Play("binge-torrent://" + choice.Torrent.InfoHash + "/" + (choice.Torrent.FileIndex?.ToString() ?? "largest"), context.DisplayName, context, playbackUrl: session.StartHttpServer());
            TorrentPanel.IsVisible = torrentSession == session;
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (revision != torrentRevision || localClosing) return;
            if (TryScheduleNextSource(error is IOException ? error.Message : "This source could not start.")) return;
            StopTorrentPlayback();
            PlayerNotice(error is IOException ? error.Message : "This torrent could not start. Try another source or check your connection.");
            Status(error is IOException ? error.Message : "Torrent playback could not start. Try another source or check your connection.");
        }
        finally { if (revision == torrentRevision) torrentStarting = false; }
    }
    private void CancelTorrent_Click(object? sender, RoutedEventArgs e)
    {
        ResetSourceAttempts();
        SaveProgress();
        StopTorrentPlayback();
        currentPath = null;
        Status("Torrent stopped. Downloaded data is kept for next time.");
        PlayerNotice("Torrent stopped. Choose another source to continue.");
        RefreshHistory();
    }
    private async void ClearVideoCache_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var root = Path.GetFullPath(Path.Combine(store.DataDirectory, "torrent-cache-v1"));
            var removed = await Task.Run(() => !Directory.Exists(root) ? 0 : Directory.EnumerateDirectories(root)
                .Where(x => TorrentCache.IsOwned(Path.GetFileName(x))).Count(x => TorrentCache.Delete(root, x)));
            if (!localClosing) Status($"Cleared {removed} unused video cache(s). The active video is kept.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { if (!localClosing) Status("The video cache could not be cleared. Check that its folder is accessible."); }
    }
    private void StopTorrentPlayback()
    {
        torrentRevision++;
        torrentStarting = false;
        torrentRequest.Cancel();
        var session = torrentSession;
        torrentSession = null;
        if (session != null)
        {
            session.Cancel(); // Release blocked HTTP reads before stopping the player.
            mediaGeneration++;
            var stop = StopNativePlayerAsync();
            var previous = torrentCleanup;
            torrentCleanup = Task.Run(async () =>
            {
                try { await previous; await stop; await session.DisposeAsync(); }
                catch { /* Locked cache files are retried at the next torrent launch. */ }
            });
        }
        TorrentPanel.IsVisible = false;
    }
}
