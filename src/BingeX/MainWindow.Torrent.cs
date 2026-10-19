using Avalonia.Interactivity;
using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private TorrentPlayback? torrentSession;
    private StreamMediaInput? torrentInput;
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
        StopTorrentPlayback();
        player.Stop();
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
        TorrentState.Text = "Finding peers and torrent information…";
        TorrentDetails.Text = "Temporary cache: up to 32 GB · upload limit: 256 KB/s";
        Status("Preparing torrent playback…");
        try
        {
            await torrentCleanup;
            token.ThrowIfCancellationRequested();
            var dataDirectory = store.DataDirectory;
            var session = await Task.Run(() =>
            {
                TorrentPlayback.CleanOldSessions(dataDirectory);
                return new TorrentPlayback(dataDirectory);
            }, token);
            if (token.IsCancellationRequested || revision != torrentRevision) { await session.DisposeAsync(); return; }
            torrentSession = session;
            var progress = new Progress<TorrentStatus>(update =>
            {
                if (revision != torrentRevision || localClosing) return;
                TorrentState.Text = update.Stage;
                TorrentDetails.Text = update.HasMetadata
                    ? $"{update.DownloadedPercent:0.0}% downloaded · {update.DownloadRate / 1024d:0} KB/s · {update.Peers} peer(s) · cache limit 32 GB"
                    : $"{update.ElapsedSeconds}s elapsed · {update.Peers} peer(s) · {update.Discovery}";
                TorrentProgress.IsVisible = torrentStarting || update.Waiting;
            });
            var video = await Task.Run(() => session.OpenAsync(choice.Torrent, progress, token), token);
            token.ThrowIfCancellationRequested();
            if (revision != torrentRevision) return;
            torrentStarting = false;
            torrentInput = new StreamMediaInput(video);
            Play("binge-torrent://" + choice.Torrent.InfoHash + "/" + (choice.Torrent.FileIndex?.ToString() ?? "largest"), context.DisplayName, context, torrentInput);
            TorrentPanel.IsVisible = torrentSession == session;
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (revision != torrentRevision || localClosing) return;
            StopTorrentPlayback();
            PlayerNotice(error is IOException ? error.Message : "This torrent could not start. Try another source or check your connection.");
            Status(error is IOException ? error.Message : "Torrent playback could not start. Try another source or check your connection.");
        }
        finally { if (revision == torrentRevision) torrentStarting = false; }
    }
    private void CancelTorrent_Click(object? sender, RoutedEventArgs e)
    {
        SaveProgress();
        StopTorrentPlayback();
        currentPath = null;
        Status("Torrent stopped. Temporary cache is being cleared.");
        PlayerNotice("Torrent stopped. Choose another source to continue.");
        RefreshHistory();
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
            session.Cancel(); // Release a blocked native read before stopping the player.
            mediaGeneration++;
            player?.Stop();
            torrentInput?.Dispose(); torrentInput = null;
            var previous = torrentCleanup;
            torrentCleanup = Task.Run(async () =>
            {
                try { await previous; await session.DisposeAsync(); }
                catch { /* Locked cache files are retried at the next torrent launch. */ }
            });
        }
        TorrentPanel.IsVisible = false;
    }
}
