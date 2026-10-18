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
        LibraryPage.IsVisible = page == "Library";
        PlayerPage.IsVisible = page == "Player";
        AddonsPage.IsVisible = page == "Add-ons";
        PageTitle.Text = page == "Library" ? "Your library" : page;
        if (page == "Library") RefreshHistory();
    }
    private void Library_Click(object? sender, RoutedEventArgs e) => Page("Library");
    private void Player_Click(object? sender, RoutedEventArgs e) => Page("Player");
    private void Addons_Click(object? sender, RoutedEventArgs e) => Page("Add-ons");

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open media", AllowMultiple = false });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) Play(path, Path.GetFileName(path));
    }

    private void Play(string path, string title)
    {
        if (player == null || engine == null) { Status("Playback engine is unavailable."); return; }
        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri)) { Status("Invalid media location."); return; }
        if (uri.IsFile && !File.Exists(path)) { Status("This file has moved or is no longer available."); return; }
        SaveProgress();
        resumeAt = store.History().FirstOrDefault(x => x.Path == path)?.Position ?? 0;
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
        if (player.IsPlaying) Status("Playing · " + currentTitle);
        PlayButton.Content = player.IsPlaying ? "Pause" : "Play";
        if (++saveTicks >= 10) { saveTicks = 0; SaveProgress(); }
    }
    private void Seek() { if (player?.IsSeekable == true && player.Length > 0) player.Time = (long)(SeekBar.Value / 1000 * player.Length); }
    private void SaveProgress()
    {
        // Remote add-on URLs can contain expiring tokens; only local paths enter watch history.
        if (player == null || currentPath == null || !File.Exists(currentPath) || player.Length <= 0) return;
        store.Save(new(currentPath, currentTitle, Math.Max(0, player.Time), player.Length));
    }
    private void RefreshHistory()
    {
        HistoryItems.Children.Clear();
        var history = store.History();
        EmptyHistory.IsVisible = history.Count == 0;
        foreach (var media in history)
        {
            var button = new Button { Content = $"▶  {media.Title}     {Clock(media.Position)} / {Clock(media.Duration)}", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            button.Click += (_, _) => Play(media.Path, media.Title);
            HistoryItems.Children.Add(button);
        }
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

    private async void Install_Click(object? sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        try
        {
            var url = ManifestInput.Text?.Trim() ?? "";
            var manifest = await addons.Get(url);
            var id = AddonClient.Text(manifest, "id");
            var name = AddonClient.Text(manifest, "name");
            if (id.Length == 0 || name.Length == 0 || !manifest.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Not a valid Stremio manifest.");
            store.Install(new(id, name, url));
            ManifestInput.Text = "";
            RefreshAddons();
            Status("Installed " + name);
        }
        catch (Exception) { Status("Add-on installation failed. Check the HTTPS manifest URL and provider configuration."); }
        finally { InstallButton.IsEnabled = true; }
    }

    private void RefreshAddons()
    {
        AddonItems.Children.Clear();
        foreach (var addon in store.Addons())
        {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
            var browse = new Button { Content = "Browse " + addon.Name };
            browse.Click += async (_, _) => await Browse(addon);
            var remove = new Button { Content = "Remove" };
            remove.Click += (_, _) => { store.RemoveAddon(addon.Id); RefreshAddons(); CatalogItems.Children.Clear(); CatalogHeading.Text = ""; };
            row.Children.Add(browse); row.Children.Add(remove); AddonItems.Children.Add(row);
        }
    }
    private async Task Browse(InstalledAddon addon)
    {
        try
        {
            CatalogItems.Children.Clear();
            CatalogHeading.Text = addon.Name;
            var manifest = await addons.Get(addon.ManifestUrl);
            if (!manifest.TryGetProperty("catalogs", out var catalogs) || catalogs.ValueKind != JsonValueKind.Array) { Status("This add-on has no catalogs."); return; }
            foreach (var catalog in catalogs.EnumerateArray())
            {
                var type = AddonClient.Text(catalog, "type"); var id = AddonClient.Text(catalog, "id");
                var button = new Button { Content = AddonClient.Text(catalog, "name", id) + " · " + type };
                button.Click += async (_, _) => await LoadCatalog(addon, type, id);
                CatalogItems.Children.Add(button);
            }
            Status("Choose a catalog.");
        }
        catch (Exception) { Status("Could not load this add-on’s catalogs."); }
    }
    private async Task LoadCatalog(InstalledAddon addon, string type, string id)
    {
        try
        {
            var response = await addons.Get(AddonClient.Endpoint(addon.ManifestUrl, "catalog", type, id));
            CatalogItems.Children.Clear();
            if (!response.TryGetProperty("metas", out var metas) || metas.ValueKind != JsonValueKind.Array) { Status("No titles returned."); return; }
            foreach (var item in metas.EnumerateArray().Take(100))
            {
                var title = AddonClient.Text(item, "name", "Untitled"); var mediaId = AddonClient.Text(item, "id");
                var mediaType = AddonClient.Text(item, "type", type);
                var button = new Button { Content = title, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
                button.Click += async (_, _) => await LoadTitle(addon, mediaType, mediaId, title);
                CatalogItems.Children.Add(button);
            }
            Status("Choose a title. Stream providers are queried from your installed add-ons.");
        }
        catch (Exception) { Status("Catalog request failed. Some catalogs require search or additional configuration."); }
    }
    private async Task LoadTitle(InstalledAddon addon, string type, string id, string title)
    {
        if (type != "series") { await LoadStreams(type, id, title); return; }
        try
        {
            var response = await addons.Get(AddonClient.Endpoint(addon.ManifestUrl, "meta", type, id));
            var meta = response.GetProperty("meta");
            CatalogItems.Children.Clear();
            if (!meta.TryGetProperty("videos", out var videos)) { Status("No episodes available from this metadata provider."); return; }
            foreach (var episode in videos.EnumerateArray())
            {
                var episodeId = AddonClient.Text(episode, "id");
                var name = AddonClient.Text(episode, "title", episodeId);
                var button = new Button { Content = name };
                button.Click += async (_, _) => await LoadStreams(type, episodeId, title + " · " + name);
                CatalogItems.Children.Add(button);
            }
        }
        catch (Exception) { Status("Episode metadata could not be loaded."); }
    }
    private async Task LoadStreams(string type, string id, string title)
    {
        Status("Finding streams…");
        CatalogItems.Children.Clear(); CatalogHeading.Text = title;
        var supported = 0;
        foreach (var addon in store.Addons())
        {
            try
            {
                var response = await addons.Get(AddonClient.Endpoint(addon.ManifestUrl, "stream", type, id));
                if (!response.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array) continue;
                foreach (var stream in streams.EnumerateArray())
                {
                    var url = AddonClient.Text(stream, "url");
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) continue;
                    // Header-dependent streams need an adapter before they can be safely supported.
                    if (stream.TryGetProperty("behaviorHints", out var hints) && hints.TryGetProperty("proxyHeaders", out _)) continue;
                    var button = new Button { Content = addon.Name + " · " + AddonClient.Text(stream, "name", "Play stream") };
                    button.Click += (_, _) => Play(url, title);
                    CatalogItems.Children.Add(button); supported++;
                }
            }
            catch (Exception) { /* An add-on may not implement the stream resource. */ }
        }
        Status(supported > 0 ? "Choose a stream." : "No supported direct streams found. Torrent, external-player, and custom-header sources are not supported yet.");
    }
}
