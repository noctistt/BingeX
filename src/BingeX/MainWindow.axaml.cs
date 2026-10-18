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

    public MainWindow()
    {
        InitializeComponent();
        library = new MediaLibrary(addons);
        SetupPlayerControls();
        RefreshHistory();
        RefreshAddons();
        Opened += (_, _) =>
        {
            try
            {
                Core.Initialize();
                engine = new LibVLC("--no-video-title-show");
                player = new MediaPlayer(engine) { Volume = preferences.Volume, EnableKeyInput = false, EnableMouseInput = false };
                player.EndReached += PlayerEnded;
                VideoSurface.MediaPlayer = player;
                player.EncounteredError += (_, _) => Dispatcher.UIThread.Post(() => Status("Playback failed. The source may be unavailable, damaged, or unsupported."));
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
        SpeedBox.SelectionChanged += (_, _) => { if (player != null) player.SetRate(new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)]); };
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Closing += (_, _) =>
        {
            timer.Stop(); overlayTimer.Stop();
            SaveProgress();
            mediaGeneration++;
            // Stop native output while the window's graphics resources are still alive.
            player?.Stop();
        };
        Closed += (_, _) =>
        {
            browseWork.Cancel();
            detailWork.Cancel();
            streamWork.Cancel();
            posterWork.Cancel();
            artwork.Dispose();
            overlayTimer.Stop();
            store.SavePreferences(preferences);
            timer.Stop();
            SaveProgress();
            VideoSurface.MediaPlayer = null;
            player?.Dispose();
            engine?.Dispose();
            store.Dispose();
            addons.Dispose();
        };
    }

    private void Status(string message) => StatusLabel.Text = message;
    private static string Clock(long milliseconds) => TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(milliseconds >= 3600000 ? @"h\:mm\:ss" : @"mm\:ss");
    private void Page(string page)
    {
        if (page != "Player" && WindowState == WindowState.FullScreen) SetFullscreen(false);
        if (page != "Library") { browseWork.Cancel(); posterWork.Cancel(); }
        if (page != "Details") { detailWork.Cancel(); streamWork.Cancel(); }
        activePage = page;
        LibraryNav.Classes.Set("active", page is "Library" or "Details");
        PlayerNav.Classes.Set("active", page == "Player");
        AddonsNav.Classes.Set("active", page == "Add-ons");
        DetailsPage.IsVisible = page == "Details";
        LibraryPage.IsVisible = page == "Library";
        PlayerPage.IsVisible = page == "Player";
        VideoSurface.IsVisible = page == "Player";
        AddonsPage.IsVisible = page == "Add-ons";
        if (page == "Player") RevealPlayerControls();
        PageTitle.Text = page == "Library" ? "Your library" : page == "Details" ? "Discover" : page;
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

    private void Play(string path, string title, MediaContext? context = null)
    {
        if (player == null || engine == null) { Status("Playback engine is unavailable."); return; }
        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri)) { Status("Invalid media location."); return; }
        if (uri.IsFile && !File.Exists(path)) { Status("This file has moved or is no longer available."); return; }
        SaveProgress();
        player.Stop();
        currentContext = context;
        currentHistoryKey = context?.HistoryKey ?? path;
        if (context != null) store.SaveContext(context);
        resumeAt = store.FindHistory(currentHistoryKey)?.Position ?? 0;
        ResetMediaControls(string.Equals(path, currentPath, StringComparison.Ordinal));
        thumbnailAttempted = false;
        currentPath = path;
        currentTitle = title;
        using var media = new Media(engine, uri);
        if (!player.Play(media)) { Status("Unable to start this media."); return; }
        player.SetRate(new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)]);
        PlayerEmpty.IsVisible = false;
        NowPlaying.Text = title;
        Page("Player");
        Status("Loading " + title);
    }

    private void Tick(object? sender, EventArgs e)
    {
        while (Program.Instance?.TryDequeue(out var request) == true) OpenLaunchRequest(request);
        if (player == null || currentPath == null) return;
        if (playbackEnded) { UpdatePlayIcon("Replay"); return; }
        if (player.Length > 0) lastDuration = player.Length;
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
            if (!File.Exists(thumbnail)) player.TakeSnapshot(0, thumbnail, 480, 0);
        }
        UpdatePlayIcon(player.IsPlaying ? "Pause" : "Play");
        if (++saveTicks >= 10) { saveTicks = 0; SaveProgress(); }
    }
    private void Seek()
    {
        if (player?.IsSeekable != true || player.Length <= 0) return;
        resumeAt = 0;
        player.Time = (long)(SeekBar.Value / 1000 * player.Length);
        RevealPlayerControls();
    }
    private void SaveProgress()
    {
        // Stable title/episode IDs keep expiring playback URLs out of history.
        if (player == null || currentPath == null || currentHistoryKey == null || lastDuration <= 0 || resumeAt > 0) return;
        if (currentContext == null && !File.Exists(currentPath)) return;
        if (playbackEnded) store.Save(new(currentHistoryKey, currentTitle, lastDuration, lastDuration));
        else if (player.State is VLCState.Playing or VLCState.Paused && lastDuration > 0)
            store.Save(new(currentHistoryKey, currentTitle, Math.Max(0, player.Time), lastDuration));
    }
    private void Play_Click(object? sender, RoutedEventArgs e)
    {
        if (player == null || currentPath == null) return;
        if (playbackEnded || player.State is VLCState.Ended or VLCState.Stopped) Play(currentPath, currentTitle, currentContext);
        else player.Pause();
        RevealPlayerControls();
    }
    private void Back_Click(object? sender, RoutedEventArgs e) => Skip(-10000);
    private void Forward_Click(object? sender, RoutedEventArgs e) => Skip(10000);
    private void Skip(long delta)
    {
        if (player?.IsSeekable != true) return;
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
