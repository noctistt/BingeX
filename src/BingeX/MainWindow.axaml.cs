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
    private readonly LibraryStore store = new();
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
        RefreshHistory();
        RefreshAddons();
        Opened += (_, _) =>
        {
            try
            {
                Core.Initialize();
                engine = new LibVLC("--no-video-title-show");
                player = new MediaPlayer(engine) { Volume = 80 };
                VideoSurface.MediaPlayer = player;
                player.EncounteredError += (_, _) => Dispatcher.UIThread.Post(() => Status("Playback failed. The source may be unavailable, damaged, or unsupported."));
                timer.Tick += Tick;
                timer.Start();
                _ = RefreshCatalogs();
                if (Program.StartupFile is { } path) Play(path, Path.GetFileName(path));
            }
            catch (Exception) { Status("Playback engine could not start. Check the bundled LibVLC installation."); }
        };
        VolumeBar.PropertyChanged += (_, e) => { if (e.Property.Name == "Value" && player != null) player.Volume = (int)VolumeBar.Value; };
        SeekBar.AddHandler(PointerPressedEvent, (_, _) => seekDragging = true, RoutingStrategies.Tunnel);
        SeekBar.AddHandler(PointerReleasedEvent, (_, _) => { seekDragging = false; Seek(); }, RoutingStrategies.Tunnel);
        SeekBar.PropertyChanged += (_, e) => { if (e.Property.Name == "Value" && !updatingSeek && !seekDragging) Seek(); };
        SpeedBox.SelectionChanged += (_, _) => { if (player != null) player.SetRate(new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)]); };
        KeyDown += OnKey;
        Closed += (_, _) =>
        {
            browseWork.Cancel();
            detailWork.Cancel();
            streamWork.Cancel();
            posterWork.Cancel();
            artwork.Dispose();
            timer.Stop();
            SaveProgress();
            VideoSurface.MediaPlayer = null;
            player?.Stop();
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
        if (page != "Library") { browseWork.Cancel(); posterWork.Cancel(); }
        if (page != "Details") { detailWork.Cancel(); streamWork.Cancel(); }
        activePage = page;
        LibraryNav.Classes.Set("active", page is "Library" or "Details");
        PlayerNav.Classes.Set("active", page == "Player");
        AddonsNav.Classes.Set("active", page == "Add-ons");
        DetailsPage.IsVisible = page == "Details";
        LibraryPage.IsVisible = page == "Library";
        PlayerPage.IsVisible = page == "Player";
        AddonsPage.IsVisible = page == "Add-ons";
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
        resumeAt = store.History().FirstOrDefault(x => x.Path == currentHistoryKey)?.Position ?? 0;
        thumbnailAttempted = false;
        currentPath = path;
        currentTitle = title;
        using var media = new Media(engine, uri);
        if (!player.Play(media)) { Status("Unable to start this media."); return; }
        player.SetRate(new[] { .5f, .75f, 1f, 1.25f, 1.5f, 2f }[Math.Max(0, SpeedBox.SelectedIndex)]);
        NowPlaying.Text = title;
        Page("Player");
        Status("Loading " + title);
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (player == null || currentPath == null) return;
        if (resumeAt > 0 && player.IsSeekable && player.Length > 0)
        {
            if (resumeAt < player.Length - 10000) player.Time = resumeAt;
            resumeAt = 0;
        }
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
        PlayButton.Content = player.IsPlaying ? "Pause" : "Play";
        if (++saveTicks >= 10) { saveTicks = 0; SaveProgress(); }
    }
    private void Seek() { if (player?.IsSeekable == true && player.Length > 0) player.Time = (long)(SeekBar.Value / 1000 * player.Length); }
    private void SaveProgress()
    {
        // Stable title/episode IDs keep expiring playback URLs out of history.
        if (player == null || currentPath == null || currentHistoryKey == null || player.Length <= 0 || resumeAt > 0) return;
        if (currentContext == null && !File.Exists(currentPath)) return;
        if (player.State is not (VLCState.Playing or VLCState.Paused or VLCState.Ended)) return;
        store.Save(new(currentHistoryKey, currentTitle, Math.Max(0, player.Time), player.Length));
    }
    private void Play_Click(object? sender, RoutedEventArgs e) { if (player?.Media != null) player.Pause(); }
    private void Back_Click(object? sender, RoutedEventArgs e) => Skip(-10000);
    private void Forward_Click(object? sender, RoutedEventArgs e) => Skip(10000);
    private void Skip(long delta) { if (player?.IsSeekable == true) player.Time = Math.Clamp(player.Time + delta, 0, Math.Max(0, player.Length)); }
    private void Fullscreen_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    private async void Subtitle_Click(object? sender, RoutedEventArgs e)
    {
        if (player?.Media == null) { Status("Open a video before adding subtitles."); return; }
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose subtitles", AllowMultiple = false });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            Status(player.AddSlave(MediaSlaveType.Subtitle, new Uri(path).AbsoluteUri, true) ? "Subtitle file added." : "Could not add subtitles.");
    }
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox || !PlayerPage.IsVisible) return;
        switch (e.Key)
        {
            case Key.Space: Play_Click(null, new()); e.Handled = true; break;
            case Key.Left: Skip(-10000); e.Handled = true; break;
            case Key.Right: Skip(10000); e.Handled = true; break;
            case Key.F: Fullscreen_Click(null, new()); e.Handled = true; break;
            case Key.Escape: WindowState = WindowState.Normal; e.Handled = true; break;
        }
    }

}
