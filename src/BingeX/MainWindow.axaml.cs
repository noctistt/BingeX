using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System.Text.Json;

namespace BingeX;

public partial class MainWindow : Window
{
    private readonly LibraryStore store = new(Environment.GetEnvironmentVariable("BINGEX_DATA_DIRECTORY"));
    private readonly AddonClient addons = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private LibVLC? engine;
    private MediaPlayer? player;
    private string? currentPath;
    private string currentTitle = "";
    private long resumeAt;
    private int saveTicks;
    private bool updatingSeek;
    private bool seekDragging;
    private bool suppressCurrentHistory;
    private string playerReturnPage = "Library";

    private void NavigateBack_Click(object? sender, RoutedEventArgs e)
    {
        if (activePage == "Player")
        {
            SaveProgress();
            Page(playerReturnPage);
        }
        else if (activePage == "Details") Page("Library");
    }

    public MainWindow()
    {
        InitializeComponent();
        VideoSurface.SizeChanged += (_, _) => SynchronizePlayerOverlay();
        library = new MediaLibrary(addons);
        SetupPlayerControls();
        RefreshHistory();
        RefreshAddons();
        SetupLocalLibrary();
        SetupSources();
        Opened += (_, _) =>
        {
            _ = ScanMediaFolders();
            try
            {
                Core.Initialize();
                engine = new LibVLC("--no-video-title-show");
                player = new MediaPlayer(engine) { Volume = preferences.Volume, EnableKeyInput = false, EnableMouseInput = false };
                player.EndReached += PlayerEnded;
                VideoSurface.MediaPlayer = player;
                player.EncounteredError += (_, _) => Dispatcher.UIThread.Post(() =>
                {
                    if (localClosing || NativePlaybackBusy || player?.State != VLCState.Error) return;
                    var failure = torrentSession?.Failure ?? "Playback failed. The source may be unavailable, damaged, or unsupported.";
                    if (TryScheduleNextSource(failure)) return;
                    if (torrentSession != null) StopTorrentPlayback();
                    Status(failure); PlayerNotice(failure);
                });
                timer.Tick += Tick;
                timer.Start();
                _ = RefreshCatalogs();
                if (Program.StartupFile is { } path) Play(path, Path.GetFileName(path));
                if (Program.StartupSubtitle is { } subtitlePath) _ = LoadSubtitle(subtitlePath);
            }
            catch (Exception) { Status("Playback engine could not start. Check the bundled LibVLC installation."); }
        };
        VolumeBar.PropertyChanged += VolumeChanged;
        SeekBar.AddHandler(PointerPressedEvent, (_, _) => seekDragging = true, RoutingStrategies.Tunnel);
        SeekBar.AddHandler(PointerReleasedEvent, (_, _) => { seekDragging = false; Seek(); }, RoutingStrategies.Tunnel);
        SeekBar.PropertyChanged += (_, e) => { if (e.Property.Name == "Value" && !updatingSeek && !seekDragging) Seek(); };
        SpeedBox.SelectionChanged += (_, _) => { if (!NativePlaybackBusy && player != null) player.SetRate(new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)]); };
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Closing += async (_, e) =>
        {
            sourceTimer.Stop(); ResetSourceAttempts();
            if (torrentCloseReady) return;
            e.Cancel = true;
            if (torrentCloseInProgress) return;
            torrentCloseInProgress = true;
            await FinishWindowCloseAsync();
            torrentCloseReady = true;
            Close();
        };
        Closed += (_, _) =>
        {
            browseWork.Cancel();
            detailWork.Cancel();
            streamWork.Cancel();
            posterWork.Cancel();
            localScanWork.Dispose();
            artwork.Dispose();
            subtitleDownloads.Dispose();
            subtitleWork.Dispose();
            overlayTimer.Stop();
            store.SavePreferences(preferences);
            timer.Stop();
            SaveProgress();
            VideoSurface.MediaPlayer = null;
            player?.Dispose();
            engine?.Dispose();
            store.Dispose();
            addonCheckLifetime.Cancel(); addonCheckLifetime.Dispose();
            addons.Dispose();
        };
    }

    private void Status(string message) => StatusLabel.Text = message;
    private static string Clock(long milliseconds) => TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(milliseconds >= 3600000 ? @"h\:mm\:ss" : @"mm\:ss");
    private void Page(string page)
    {
        if (page == "Player" && activePage != "Player") playerReturnPage = activePage;
        if (page != "Player" && pendingSource != null) ResetSourceAttempts();
        videoClickTimer.Stop();
        videoPressPoint = null;
        if (page != "Player" && torrentStarting) StopTorrentPlayback();
        if (page != "Player") CancelOnlineSubtitles();
        if (page != "Player" && WindowState == WindowState.FullScreen) SetFullscreen(false);
        if (page != "Library") { browseWork.Cancel(); posterWork.Cancel(); }
        if (page != "Details") { detailWork.Cancel(); streamWork.Cancel(); }
        activePage = page;
        NavigateBackButton.IsVisible = page is "Player" or "Details";
        LibraryNav.Classes.Set("active", page is "Library" or "Details");
        LocalLibraryNav.Classes.Set("active", page == "My Library");
        PlayerNav.Classes.Set("active", page == "Player");
        AddonsNav.Classes.Set("active", page == "Add-ons");
        DetailsPage.IsVisible = page == "Details";
        LibraryPage.IsVisible = page == "Library";
        LocalLibraryPage.IsVisible = page == "My Library";
        UpdatePlayerLayout();
        AddonsPage.IsVisible = page == "Add-ons";
        if (page == "Player") RevealPlayerControls();
        if (page == "My Library") RefreshLocalLibrary();
        PageTitle.Text = page == "Library" ? "Home" : page == "Details" ? "Discover" : page;
        if (page == "Library") { RefreshHistory(); ResumePosterArtwork(); }
    }
    private async void Library_Click(object? sender, RoutedEventArgs e)
    {
        Page("Library");
        if (BrowseBusy.IsVisible || catalogs.Count == 0) await RefreshCatalogs();
    }
    private void Player_Click(object? sender, RoutedEventArgs e) => Page("Player");
    private void Addons_Click(object? sender, RoutedEventArgs e) => Page("Add-ons");

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open media", AllowMultiple = false });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) Play(path, Path.GetFileName(path));
    }

    private async void Play(string path, string title, MediaContext? context = null, StreamMediaInput? input = null, string? playbackUrl = null)
    {
        if (localClosing) return;
        if (input == null && Path.GetExtension(path).Equals(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) { StartTorrentFile(path); return; }
        if (player == null || engine == null) { Status("Playback engine is unavailable."); return; }
        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri)) { Status("Invalid media location."); return; }
        if (uri.IsFile && !File.Exists(path)) { Status("This file has moved or is no longer available."); return; }
        if (input == null && playbackUrl == null && (activeSource == null || activeSource.Url != path)) ResetSourceAttempts();
        SaveProgress(); thumbnailWork.Cancel();
        var request = ++playbackRequest;
        var preserve = string.Equals(path, currentPath, StringComparison.Ordinal);
        playbackStarting = true;
        videoClickTimer.Stop(); videoPressPoint = null;
        Status("Opening " + title + "…");
        try
        {
            if (input == null && playbackUrl == null) { StopTorrentPlayback(); lastTorrentChoice = null; lastTorrentContext = null; }
            await StopNativePlayerAsync();
            if (localClosing || request != playbackRequest) return;
            suppressCurrentHistory = false;
            currentContext = context;
            currentHistoryKey = context?.HistoryKey ?? path;
            if (context != null) store.SaveContext(context);
            resumeAt = store.FindHistory(currentHistoryKey)?.Position ?? 0;
            ResetMediaControls(preserve);
            pendingAudioSelection = store.AudioSelection(currentHistoryKey);
            thumbnailAttempted = false;
            currentPath = path; currentTitle = title;
            PlayerEmpty.IsVisible = false;
            NowPlaying.Text = title; MiniTitle.Text = title;
            // Realize and arrange the native video surface before the decoder starts.
            Page("Player");
            if (!await WaitForPlayerLayoutAsync(request))
            {
                if (!localClosing && request == playbackRequest)
                    PlayerNotice("The player could not finish opening. Restore the window and try again.");
                return;
            }
            if (localClosing || request != playbackRequest) return;
            var native = player;
            if (OperatingSystem.IsWindows() && native.Hwnd == IntPtr.Zero)
            {
                VideoSurface.MediaPlayer = null;
                VideoSurface.MediaPlayer = native;
                if (native.Hwnd == IntPtr.Zero) throw new InvalidOperationException("The video surface is not ready.");
            }
            var playbackEngine = engine;
            var speed = new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)];
            var extraAudio = externalAudioPath;
            var started = false;
            await nativePlayback.Enqueue(() =>
            {
                if (request != Volatile.Read(ref playbackRequest)) return;
                using var media = input == null ? new Media(playbackEngine, playbackUrl == null ? uri : new Uri(playbackUrl)) : new Media(playbackEngine, input);
                if (extraAudio != null && File.Exists(extraAudio)) media.AddSlave(MediaSlaveType.Audio, 4, new Uri(extraAudio));
                started = native.Play(media);
                if (started) { native.SetRate(speed); native.SetAudioDelay(0); }
            });
            if (localClosing || request != playbackRequest) return;
            playbackStarting = false;
            if (!started || native.State == VLCState.Error)
            {
                if (TryScheduleNextSource("This source could not start.")) return;
                if (input != null || playbackUrl != null) StopTorrentPlayback();
                Status("Unable to start this media."); return;
            }
            native.Volume = preferences.Volume;
            Status("Loading " + title);
        }
        catch (Exception)
        {
            if (!localClosing && request == playbackRequest) { playbackStarting = false; PlayerNotice("Playback could not start. Try another source."); }
        }
        finally { if (request == playbackRequest) playbackStarting = false; }
    }

    private void Tick(object? sender, EventArgs e)
    {
        while (Program.Instance?.TryDequeue(out var request) == true) OpenLaunchRequest(request);
        if (NativePlaybackBusy || player == null || currentPath == null) return;
        if (playbackEnded) { UpdatePlayIcon("Replay"); return; }
        if (player.State is VLCState.Opening or VLCState.Buffering)
        {
            UpdatePlayIcon("Pause");
            Status("Buffering · " + currentTitle);
            return;
        }
        if (player.Length > 0) lastDuration = player.Length;
        if (lastDuration > 0) torrentSession?.UpdateDuration(lastDuration);
        if (player.Time > 0) lastPosition = player.Time;
        if (resumeAt > 0 && player.IsSeekable && player.Length > 0)
        {
            var target = PlaybackPolicy.ResumeTarget(resumeAt, player.Length);
            player.Time = target;
            resumeAt = 0;
            if (target > 0) PlayerNotice("Resumed at " + Clock(target));
        }
        else if (resumeAt > 0 && player.IsPlaying && player.Time > 2000 && !player.IsSeekable)
        {
            resumeAt = 0;
            PlayerNotice("This source cannot seek. Playing from the start.");
        }
        if (++trackTicks % 2 == 0) RefreshTracks();
        if (!seekDragging)
        {
            updatingSeek = true;
            SeekBar.Value = player.Length > 0 ? player.Time * 1000d / player.Length : 0;
            updatingSeek = false;
        }
        TimeLabel.Text = Clock(player.Time) + " / " + (player.Length > 0 ? Clock(player.Length) : "--:--");
        if (player.IsPlaying && activePage == "Player") Status("Playing · " + currentTitle);
        if (currentContext == null && !thumbnailAttempted && player.IsPlaying && player.Time > 1000 && File.Exists(currentPath))
        {
            thumbnailAttempted = true;
            var thumbnail = store.ThumbnailPath(currentPath);
            if (!File.Exists(thumbnail))
            {
                var native = player;
                _ = nativePlayback.Enqueue(() => native.TakeSnapshot(0, thumbnail, 480, 0));
                return;
            }
        }
        UpdatePlayIcon(player.IsPlaying ? "Pause" : "Play");
        if (++saveTicks >= 10) { saveTicks = 0; SaveProgress(); }
    }
    private void Seek()
    {
        if (NativePlaybackBusy || player?.IsSeekable != true || player.Length <= 0) return;
        resumeAt = 0;
        player.Time = (long)(SeekBar.Value / 1000 * player.Length);
        RevealPlayerControls();
    }
    private void SaveProgress()
    {
        // Stable title/episode IDs keep expiring playback URLs out of history.
        if (suppressCurrentHistory || player == null || currentPath == null || currentHistoryKey == null || lastDuration <= 0 || resumeAt > 0) return;
        if (currentContext == null && !File.Exists(currentPath)) return;
        if (playbackEnded) store.Save(new(currentHistoryKey, currentTitle, lastDuration, lastDuration));
        else if (NativePlaybackBusy) store.Save(new(currentHistoryKey, currentTitle, Math.Max(0, lastPosition), lastDuration));
        else if (player.State is VLCState.Playing or VLCState.Paused && lastDuration > 0)
            store.Save(new(currentHistoryKey, currentTitle, Math.Max(0, player.Time), lastDuration));
    }
    private async void Play_Click(object? sender, RoutedEventArgs e)
    {
        videoClickTimer.Stop();
        if (NativePlaybackBusy || player == null || currentPath == null) return;
        if (playbackEnded || player.State is VLCState.Ended or VLCState.Stopped)
        {
            if (lastTorrentChoice != null && lastTorrentContext != null && currentPath.StartsWith("binge-torrent:")) StartTorrent(lastTorrentChoice, lastTorrentContext);
            else Play(currentPath, currentTitle, currentContext);
        }
        else { var native = player; await nativePlayback.Enqueue(native.Pause); }
        RevealPlayerControls();
    }
    private void Back_Click(object? sender, RoutedEventArgs e) => Skip(-10000);
    private void Forward_Click(object? sender, RoutedEventArgs e) => Skip(10000);
    private void Skip(long delta)
    {
        if (NativePlaybackBusy || player?.IsSeekable != true) return;
        resumeAt = 0;
        player.Time = PlaybackPolicy.SeekTarget(player.Time, delta, player.Length);
        RevealPlayerControls();
    }
    private void Fullscreen_Click(object? sender, RoutedEventArgs e) => SetFullscreen(WindowState != WindowState.FullScreen);
    private async void Subtitle_Click(object? sender, RoutedEventArgs e)
    {
        if (player == null || currentPath == null) { PlayerNotice("Open a video before adding subtitles."); return; }
        PlayerSettingsPanel.IsVisible = true;
        RevealPlayerControls();
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose subtitles", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Subtitle files") { Patterns = ["*.srt", "*.vtt", "*.ass", "*.ssa", "*.sub", "*.idx"] }] });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await LoadSubtitle(path);
    }
}
