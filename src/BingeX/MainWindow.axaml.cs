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
    private IReadOnlyDictionary<string, string>? currentRequestHeaders;
    private string? currentStreamAudio;
    private HeaderStreamRelay? headerStreamRelay;
    private long resumeAt;
    private int saveTicks;
    private bool updatingSeek;
    private bool seekDragging;
    private bool suppressCurrentHistory;
    private string playerReturnPage = "Library";
    private readonly PlaybackDiagnostics diagnostics;
    private long lastUiTick;

    private void NavigateBack_Click(object? sender, RoutedEventArgs e)
    {
        if (activePage == "Player")
        {
            SaveProgress();
            Page(playerReturnPage);
        }
        else if (activePage == "Details") Page("Library");
        else if(activePage=="Settings")Page(settingsReturnPage);
    }

    public MainWindow()
    {
        diagnostics = new(store.DataDirectory);
        InitializeComponent();
        VideoSurface.SizeChanged += (_, _) => SynchronizePlayerOverlay();
        library = new MediaLibrary(addons, Path.Combine(store.DataDirectory,"title-links.json"));
        SetupCloudstream();
        SetupPlayerControls();
        RefreshHistory();
        RefreshAddons();
        SetupLocalLibrary();
        SetupSources();
        SetupOnlineMode();
        SetupEpisodeNavigation();
        SetupStreamScope();
        SetupAppSettings();
        UpdateSeekLabels();
        RefreshHistory();
        Opened += (_, _) =>
        {
            _ = AnimateStartupBrand();
            _ = ScanMediaFolders();
            try
            {
                Core.Initialize();
                engine = new LibVLC("--no-video-title-show");
                player = new MediaPlayer(engine) { Volume = preferences.Volume, EnableKeyInput = false, EnableMouseInput = false };
                player.EndReached += PlayerEnded;
                VideoSurface.MediaPlayer = player;
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
        SpeedBox.SelectionChanged += (_, _) => { if (!NativePlaybackBusy && player != null) { var rate = new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)]; _ = QueuePlayerCommand(native => native.SetRate(rate)); } };
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
            catalogWork.Cancel();
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
        if (!onlineEnabled && page is "Details" or "Add-ons") page = "Library";
        if(page!="Settings")CancelSettingsPreview();
        if (page != "Player") CancelEpisodeAutoplay();
        if (page != "Details") EpisodePreparingPanel.IsVisible=false;
        using var timing = diagnostics.Measure("page-layout");
        if (page == "Player" && activePage != "Player") playerReturnPage = activePage;
        if (page != "Player" && pendingSource != null) ResetSourceAttempts();
        videoClickTimer.Stop();
        videoPressPoint = null;
        if (page != "Player" && torrentStarting) StopTorrentPlayback();
        if (page != "Player") CancelOnlineSubtitles();
        if (page != "Player" && WindowState == WindowState.FullScreen) SetFullscreen(false);
        if (page != "Library") { catalogWork.Cancel(); browseWork.Cancel(); posterWork.Cancel(); }
        if (page != "Details") { detailWork.Cancel(); streamWork.Cancel(); }
        activePage = page;
        NavigateBackButton.IsVisible = page is "Player" or "Details" or "Settings";
        LibraryNav.Classes.Set("active", page is "Library" or "Details");
        LocalLibraryNav.Classes.Set("active", page == "My Library");
        PlayerNav.Classes.Set("active", page == "Player");
        AddonsNav.Classes.Set("active", page == "Add-ons");
        DetailsPage.IsVisible = page == "Details";
        LibraryPage.IsVisible = page == "Library";
        LocalLibraryPage.IsVisible = page == "My Library";
        UpdatePlayerLayout();
        AddonsPage.IsVisible = page == "Add-ons";
        AppSettingsPage.IsVisible=page=="Settings"; SettingsNav.Classes.Set("active",page=="Settings");
        if(page=="Settings"){PopulateAppSettings();_ = UpdateStorage(null);}
        if (page == "Player") RevealPlayerControls();
        if (page == "My Library") RefreshLocalLibrary();
        PageTitle.Text = page == "Library" ? "Home" : page == "Details" ? "Discover" : page;
        if (page == "Library") { RefreshHistory(); if (onlineEnabled) ResumePosterArtwork(); }
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

    private async void Play(string path, string title, MediaContext? context = null, StreamMediaInput? input = null, string? playbackUrl = null, IReadOnlyDictionary<string, string>? requestHeaders = null, string? streamAudio = null, IReadOnlyList<StreamAudioOption>? streamAudioOptions = null, IReadOnlyList<SubtitleChoice>? streamSubtitles = null)
    {
        if (localClosing) return;
        if (input == null && Path.GetExtension(path).Equals(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) { StartTorrentFile(path); return; }
        if (player == null || engine == null) { Status("Playback engine is unavailable."); return; }
        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri)) { Status("Invalid media location."); return; }
        if (uri.IsFile && !File.Exists(path)) { Status("This file has moved or is no longer available."); return; }
        if (input == null && playbackUrl == null && (activeSource == null || activeSource.Url != path)) ResetSourceAttempts();
        var switchingPosition=PlaybackPolicy.SourceSwitchPosition(currentContext?.HistoryKey,context?.HistoryKey,lastPosition,resumeAt,playbackEnded);
        using var timing = diagnostics.Measure("stream-start-total");
        SaveProgress(); thumbnailWork.Cancel();
        var request = ++playbackRequest;
        var preserve = string.Equals(path, currentPath, StringComparison.Ordinal) && currentContext?.HistoryKey==context?.HistoryKey;
        playbackStarting = true;
        videoClickTimer.Stop(); videoPressPoint = null;
        Status("Opening " + title + "…");
        // Commit navigation before waiting for the old decoder to release its
        // stream. A slow native stop must not leave the source page on screen.
        PlayerLoadingTitle.Text = nextEpisodeBusy ? "Loading next episode…" : "Opening video…";
        PlayerLoadingDetail.Text = title;
        PlayerLoadingHint.Text = uri.IsFile ? "Reading your local video" : activeSource != null ? $"Connecting to {activeSource.Provider}" : "Connecting to the video source";
        PlayerTrackSummary.Text = "Preparing audio and subtitles…";
        PlayerLoading.IsVisible = true;
        PlayerEmpty.IsVisible = false;
        NowPlaying.Text = title; MiniTitle.Text = title;
        Page("Player");
        try
        {
            if (input == null && playbackUrl == null) { StopTorrentPlayback(); lastTorrentChoice = null; lastTorrentContext = null; }
            await StopNativePlayerAsync();
            if (localClosing || request != playbackRequest) return;
            suppressCurrentHistory = false;
            currentContext = context;
            playingEpisodeTitle = context != null && selectedTitle?.Id == context.Id && selectedTitle.Type == context.Type
                ? selectedTitle : context != null && playingEpisodeTitle?.Id == context.Id ? playingEpisodeTitle : null;
            UpdateEpisodeActions();
            currentHistoryKey = context?.HistoryKey ?? path;
            if (context != null) store.SaveContext(context);
            resumeAt = preferences.ResumePlayback ? store.FindHistory(currentHistoryKey)?.Position ?? 0 : 0;
            ResetMediaControls(preserve);
            if(preserve && useTextSubtitles)restoredSubtitleGeneration=mediaGeneration;
            if(switchingPosition>0){resumeAt=switchingPosition;sourceResumePending=true;}
            currentStreamSubtitles = streamSubtitles ?? [];
            pendingAudioSelection = store.AudioSelection(currentHistoryKey);
            if(pendingAudioSelection==null && EffectiveAudioLanguage(context) is {Length:>0} audioLanguage)
                pendingAudioSelection=new(-3,"",audioLanguage);
            streamAudio = SetupStreamAudio(streamAudioOptions, streamAudio);
            thumbnailAttempted = false;
            currentPath = path; currentTitle = title;
            currentRequestHeaders = requestHeaders;
            currentStreamAudio = streamAudio;
            PlayerEmpty.IsVisible = false;
            NowPlaying.Text = title; MiniTitle.Text = title;
            // Preserve navigation if the user went back while native stop ran.
            UpdatePlayerLayout();
            if (!await WaitForPlayerLayoutAsync(request))
            {
                if (!localClosing && request == playbackRequest)
                {
                    PlayerLoading.IsVisible = false;
                    playbackSnapshot = PlaybackSnapshot.Empty;
                    PlayerNotice("The player could not finish opening. Restore the window and try again.");
                }
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
            if (input == null && playbackUrl == null && requestHeaders is { Count: > 0 })
            {
                headerStreamRelay ??= new HeaderStreamRelay();
                playbackUrl = headerStreamRelay.Register(path, requestHeaders);
            }
            var speed = new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)];
            var extraAudio = externalAudioPath;
            var volume = preferences.Volume;
            var started = false;
            var failed = false;
            await nativePlayback.Enqueue(() =>
            {
                using var nativeTiming = diagnostics.Measure("native-start");
                if (request != Volatile.Read(ref playbackRequest)) return;
                using var media = input == null ? new Media(playbackEngine, playbackUrl == null ? uri : new Uri(playbackUrl)) : new Media(playbackEngine, input);
                if (requestHeaders != null)
                    foreach (var header in requestHeaders)
                    {
                        if (header.Key.Equals("Referer", StringComparison.OrdinalIgnoreCase)) media.AddOption(":http-referrer=" + header.Value);
                        if (header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) media.AddOption(":http-user-agent=" + header.Value);
                    }
                if (extraAudio != null && File.Exists(extraAudio)) media.AddSlave(MediaSlaveType.Audio, 4, new Uri(extraAudio));
                else if (streamAudio != null) media.AddSlave(MediaSlaveType.Audio, 4, new Uri(streamAudio));
                started = native.Play(media);
                if (started) { native.SetRate(speed); native.SetAudioDelay(0); native.Volume = volume; }
                failed = native.State == VLCState.Error;
            });
            if (localClosing || request != playbackRequest) return;
            playbackStarting = false;
            if (!started || failed)
            {
                PlayerLoading.IsVisible = false;
                playbackSnapshot = PlaybackSnapshot.Empty;
                if (TryScheduleNextSource("This source could not start.")) return;
                if (input != null || playbackUrl != null) StopTorrentPlayback();
                ShowSourceRecovery("Source unavailable","This source could not start. Choose another source for this title."); return;
            }
            Status("Loading " + title);
        }
        catch (Exception)
        {
            if (!localClosing && request == playbackRequest) { playbackStarting = false; PlayerLoading.IsVisible = false; playbackSnapshot = PlaybackSnapshot.Empty; PlayerNotice("Playback could not start. Try another source."); ShowSourceRecovery("Source unavailable","Playback could not start. Choose another source to continue."); }
        }
        finally { if (request == playbackRequest) playbackStarting = false; }
    }

    private async void Tick(object? sender, EventArgs e)
    {
        if (lastUiTick != 0)
            diagnostics.Record("ui-dispatch-delay", System.Diagnostics.Stopwatch.GetElapsedTime(lastUiTick) - timer.Interval);
        lastUiTick = System.Diagnostics.Stopwatch.GetTimestamp();
        while (Program.Instance?.TryDequeue(out var request) == true) OpenLaunchRequest(request);
        if (pollingPlayer || NativePlaybackBusy || player == null || currentPath == null || localClosing) return;
        var generation = mediaGeneration;
        var nativePlayer = player;
        pollingPlayer = true;
        try
        {
            PlaybackSnapshot? observed = null;
            var previous = playbackSnapshot;
            await nativePlayback.Enqueue(() =>
            {
                using var pollTiming = diagnostics.Measure("native-state-and-tracks");
                observed = ReadPlaybackSnapshot(nativePlayer, previous);
            });
            if (localClosing || generation != mediaGeneration || NativePlaybackBusy || observed == null) return;
            playbackSnapshot = observed;
            UpdatePlaybackUi();
        }
        catch (Exception)
        {
            if (!localClosing && generation == mediaGeneration) HandlePlaybackFailure();
        }
        finally { pollingPlayer = false; }
    }

    private void UpdatePlaybackUi()
    {
        var snapshot = playbackSnapshot;
        if (snapshot.State == VLCState.Error) { HandlePlaybackFailure(); return; }
        if (snapshot.State == VLCState.Ended && !playbackEnded) { PlayerEnded(null, EventArgs.Empty); return; }
        if (playbackEnded) { UpdatePlayIcon("Replay"); return; }
        if (snapshot.State is VLCState.Opening or VLCState.Buffering)
        {
            UpdatePlayIcon("Pause");
            Status("Buffering · " + currentTitle);
            return;
        }
        if (snapshot.Time > 0 || snapshot.State is VLCState.Error or VLCState.Ended) PlayerLoading.IsVisible = false;
        if (snapshot.Length > 0) lastDuration = snapshot.Length;
        if (lastDuration > 0) torrentSession?.UpdateDuration(lastDuration);
        if (snapshot.Time > 0) lastPosition = snapshot.Time;
        if (resumeAt > 0 && snapshot.IsSeekable && snapshot.Length > 0)
        {
            var target = sourceResumePending ? Math.Clamp(resumeAt,0,Math.Max(0,snapshot.Length-1000)) : PlaybackPolicy.ResumeTarget(resumeAt, snapshot.Length);
            _ = QueuePlayerCommand(native => native.Time = target);
            resumeAt = 0;
            if (target > 0) PlayerNotice("Resumed at " + Clock(target));
        }
        else if (resumeAt > 0 && snapshot.IsPlaying && snapshot.Time > 2000 && !snapshot.IsSeekable)
        {
            resumeAt = 0;
            PlayerNotice("This source cannot seek. Playing from the start.");
        }
        if (++trackTicks % 2 == 0) RefreshTracks();
        if (!seekDragging)
        {
            updatingSeek = true;
            SeekBar.Value = snapshot.Length > 0 ? snapshot.Time * 1000d / snapshot.Length : 0;
            updatingSeek = false;
        }
        TimeLabel.Text = Clock(snapshot.Time) + " / " + (snapshot.Length > 0 ? Clock(snapshot.Length) : "--:--");
        if (snapshot.IsPlaying && activePage == "Player" && !PlaybackWaiting.IsVisible) Status("Playing · " + currentTitle);
        if (currentContext == null && !thumbnailAttempted && snapshot.IsPlaying && snapshot.Time > 1000 && File.Exists(currentPath))
        {
            thumbnailAttempted = true;
            var thumbnail = store.ThumbnailPath(currentPath);
            if (!File.Exists(thumbnail))
            {
                _ = QueuePlayerCommand(native => native.TakeSnapshot(0, thumbnail, 480, 0));
                return;
            }
        }
        UpdatePlayIcon(snapshot.IsPlaying ? "Pause" : "Play");
        if (++saveTicks >= 10) { saveTicks = 0; SaveProgress(); }
    }
    private void Seek()
    {
        if (NativePlaybackBusy || player == null || !playbackSnapshot.IsSeekable || playbackSnapshot.Length <= 0) return;
        resumeAt = 0;
        captionClock.Reset();
        ResetPlaybackWaiting();
        var target = (long)(SeekBar.Value / 1000 * playbackSnapshot.Length);
        _ = QueuePlayerCommand(native => native.Time = target);
        RevealPlayerControls();
    }
    private void SaveProgress()
    {
        // Stable title/episode IDs keep expiring playback URLs out of history.
        if (suppressCurrentHistory || player == null || currentPath == null || currentHistoryKey == null || lastDuration <= 0 || resumeAt > 0) return;
        if (currentContext == null && !File.Exists(currentPath)) return;
        if (playbackEnded) store.Save(new(currentHistoryKey, currentTitle, lastDuration, lastDuration));
        else if (NativePlaybackBusy) store.Save(new(currentHistoryKey, currentTitle, Math.Max(0, lastPosition), lastDuration));
        else if (lastPosition > 0 && lastDuration > 0)
            store.Save(new(currentHistoryKey, currentTitle, Math.Max(0, lastPosition), lastDuration));
    }
    private async void Play_Click(object? sender, RoutedEventArgs e)
    {
        videoClickTimer.Stop();
        CancelEpisodeAutoplay();
        if (NativePlaybackBusy || player == null || currentPath == null) return;
        if (playbackEnded || playbackSnapshot.State is VLCState.Ended or VLCState.Stopped or VLCState.Error)
        {
            if (lastTorrentChoice != null && lastTorrentContext != null && currentPath.StartsWith("binge-torrent:")) StartTorrent(lastTorrentChoice, lastTorrentContext);
            else Play(currentPath, currentTitle, currentContext, requestHeaders: currentRequestHeaders, streamAudio: currentStreamAudio, streamAudioOptions: currentStreamAudioOptions, streamSubtitles: currentStreamSubtitles);
        }
        else { var native = player; await nativePlayback.Enqueue(native.Pause); }
        RevealPlayerControls();
    }
    private void Back_Click(object? sender, RoutedEventArgs e) => Skip(-preferences.SeekSeconds*1000L);
    private void Forward_Click(object? sender, RoutedEventArgs e) => Skip(preferences.SeekSeconds*1000L);
    private void Skip(long delta)
    {
        if (NativePlaybackBusy || player == null || !playbackSnapshot.IsSeekable) return;
        resumeAt = 0;
        captionClock.Reset();
        ResetPlaybackWaiting();
        var target = PlaybackPolicy.SeekTarget(playbackSnapshot.Time, delta, playbackSnapshot.Length);
        _ = QueuePlayerCommand(native => native.Time = target);
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
