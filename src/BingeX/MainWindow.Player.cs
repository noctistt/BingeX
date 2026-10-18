using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private readonly DispatcherTimer overlayTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private PlayerPreferences preferences = new();
    private DateTime lastPlayerInteraction = DateTime.UtcNow;
    private DateTime noticeUntil;
    private WindowState beforeFullscreen = WindowState.Normal;
    private SubtitleDocument? textSubtitles;
    private string? textSubtitleName;
    private bool useTextSubtitles;
    private bool playerUiReady;
    private bool updatingTracks;
    private bool playbackEnded;
    private long subtitleDelayMs;
    private long lastDuration;
    private long lastPosition;
    private int trackTicks;
    private int lastNonzeroVolume = 80;
    private long mediaGeneration;
    private string trackSignature = "";
    private record TrackOption(int Id, string Name) { public override string ToString() => Name; }
    private const int TextSubtitleId = int.MinValue;

    private void SetupPlayerControls()
    {
        preferences = store.Preferences();
        VolumeBar.Value = preferences.Volume;
        SubtitleSizePicker.SelectedIndex = Array.IndexOf(new[] { 20, 28, 36, 44 }, preferences.SubtitleSize);
        SubtitleColorPicker.SelectedIndex = Array.IndexOf(new[] { "White", "Yellow", "Mint" }, preferences.SubtitleColor);
        SubtitleBackgroundCheck.IsChecked = preferences.SubtitleBackground;
        if (preferences.Volume > 0) lastNonzeroVolume = preferences.Volume;
        UpdateVolumeIcon(preferences.Volume);
        playerUiReady = true;
        ApplySubtitleAppearance();
        PlayerOverlay.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        PlayerOverlay.PointerMoved += (_, _) => RevealPlayerControls();
        PlayerOverlay.PointerPressed += (_, e) =>
        {
            RevealPlayerControls();
            if (Inside(e.Source, PlayerControls) || Inside(e.Source, PlayerSettingsPanel)) return;
            PlayerOverlay.Focus();
            if (e.ClickCount == 2 && e.GetCurrentPoint(PlayerOverlay).Properties.IsLeftButtonPressed)
            {
                SetFullscreen(WindowState != WindowState.FullScreen);
                e.Handled = true;
            }
        };
        PlayerOverlay.SizeChanged += (_, _) => ApplySubtitleAppearance();
        DragDrop.SetAllowDrop(this, true);
        DragDrop.SetAllowDrop(PlayerOverlay, true);
        AddHandler(DragDrop.DragOverEvent, MediaDragOver);
        AddHandler(DragDrop.DropEvent, MediaDropped);
        PlayerOverlay.AddHandler(DragDrop.DragOverEvent, MediaDragOver);
        PlayerOverlay.AddHandler(DragDrop.DropEvent, MediaDropped);
        PropertyChanged += (_, e) => { if (e.Property == WindowStateProperty) ApplyFullscreenLayout(); };
        overlayTimer.Tick += (_, _) => UpdatePlayerOverlay();
        overlayTimer.Start();
    }
    private static bool Inside(object? source, Control parent) => source is Visual visual &&
        (ReferenceEquals(visual, parent) || visual.GetVisualAncestors().Any(x => ReferenceEquals(x, parent)));

    private void ResetMediaControls(bool preserveSubtitles)
    {
        mediaGeneration++;
        lastDuration = 0; lastPosition = 0; playbackEnded = false;
        if (!preserveSubtitles)
        {
            textSubtitles = null; textSubtitleName = null; useTextSubtitles = false;
            subtitleDelayMs = 0; SubtitleDelayInput.Value = 0;
        }
        CaptionPanel.IsVisible = false;
        PlayerSettingsPanel.IsVisible = false;
        PlaybackNotice.IsVisible = false;
        trackSignature = ""; trackTicks = 0;
        AudioTrackPicker.ItemsSource = new[] { new TrackOption(-1, "Waiting for audio tracks…") };
        SubtitleTrackPicker.ItemsSource = new[] { new TrackOption(-1, "Off") };
        AudioTrackPicker.IsEnabled = false;
        RevealPlayerControls();
    }
    private void PlayerEnded(object? sender, EventArgs e)
    {
        var generation = mediaGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != mediaGeneration || player == null || player.State is VLCState.Playing or VLCState.Opening) return;
            playbackEnded = true;
            if (lastDuration > 0) lastPosition = lastDuration;
            SaveProgress();
            UpdatePlayIcon("Replay");
            TimeLabel.Text = Clock(lastDuration) + " / " + Clock(lastDuration);
            updatingSeek = true;
            SeekBar.Value = 1000;
            updatingSeek = false;
            CaptionPanel.IsVisible = false;
            RevealPlayerControls();
        });
    }
    private void RevealPlayerControls()
    {
        lastPlayerInteraction = DateTime.UtcNow;
        PlayerControls.IsVisible = true;
        PlayerOverlay.Cursor = Cursor.Default;
        PositionCaptions();
    }
    private void PlayerNotice(string message)
    {
        PlaybackNoticeText.Text = message;
        noticeUntil = DateTime.UtcNow.AddSeconds(4);
        PlaybackNotice.IsVisible = activePage == "Player";
        Status(message);
    }
    private void UpdatePlayerOverlay()
    {
        if (activePage != "Player") return;
        if (DateTime.UtcNow >= noticeUntil) PlaybackNotice.IsVisible = false;
        var idle = DateTime.UtcNow - lastPlayerInteraction;
        var hide = WindowState == WindowState.FullScreen && player?.IsPlaying == true && !playbackEnded &&
            !PlayerSettingsPanel.IsVisible && !PlayerControls.IsPointerOver && !seekDragging && idle.TotalSeconds > 3;
        if (PlayerControls.IsVisible == hide)
        {
            PlayerControls.IsVisible = !hide;
            PlayerOverlay.Cursor = hide ? new Cursor(StandardCursorType.None) : Cursor.Default;
            PositionCaptions();
        }
        if (useTextSubtitles && textSubtitles != null && player != null && !playbackEnded)
        {
            var text = textSubtitles.TextAt(Math.Max(0, player.Time), subtitleDelayMs);
            CaptionText.Text = text;
            CaptionPanel.IsVisible = text.Length > 0;
        }
        else CaptionPanel.IsVisible = false;
    }
    private void PositionCaptions() => CaptionPanel.Margin = new Thickness(32, 0, 32,
        PlayerControls.IsVisible ? Math.Max(155, PlayerControls.Bounds.Height + 12) : 40);

    private void SetFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            Page("Player");
            if (WindowState != WindowState.FullScreen) beforeFullscreen = WindowState;
            WindowState = WindowState.FullScreen;
        }
        else WindowState = beforeFullscreen == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
        ApplyFullscreenLayout();
        RevealPlayerControls();
    }
    private void ApplyFullscreenLayout()
    {
        var full = WindowState == WindowState.FullScreen && activePage == "Player";
        Sidebar.IsVisible = !full;
        PageHeader.IsVisible = !full;
        StatusFooter.IsVisible = !full;
        RootLayout.ColumnDefinitions[0].Width = new GridLength(full ? 0 : 205);
        MainContent.Margin = full ? new Thickness(0) : new Thickness(30, 24);
        VideoFrame.CornerRadius = new CornerRadius(full ? 0 : 12);
        FullscreenIcon.Data = Geometry.Parse(full
            ? "M 3,9 L 9,9 L 9,3 M 15,3 L 15,9 L 21,9 M 21,15 L 15,15 L 15,21 M 9,21 L 9,15 L 3,15"
            : "M 3,9 L 3,3 L 9,3 M 15,3 L 21,3 L 21,9 M 21,15 L 21,21 L 15,21 M 9,21 L 3,21 L 3,15");
        DescribeButton(FullscreenButton, full ? "Exit fullscreen" : "Fullscreen", "F");
    }
    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        PlayerSettingsPanel.IsVisible = !PlayerSettingsPanel.IsVisible;
        if (PlayerSettingsPanel.IsVisible) { RefreshTracks(); SettingsScroll.Offset = new Vector(0, 0); }
        RevealPlayerControls();
    }
    private static void DescribeButton(Button button, string label, string shortcut)
    {
        ToolTip.SetTip(button, label + " (" + shortcut + ")");
        Avalonia.Automation.AutomationProperties.SetName(button, label);
    }
    private string playIconState = "";
    private void UpdatePlayIcon(string state)
    {
        if (playIconState == state) return;
        playIconState = state;
        PlayIcon.Data = Geometry.Parse(state switch
        {
            "Pause" => "M 5,3 L 10,3 L 10,21 L 5,21 Z M 14,3 L 19,3 L 19,21 L 14,21 Z",
            "Replay" => "M 12,3 A 9,9 0 1 1 3,12 L 6,12 A 6,6 0 1 0 12,6 L 12,10 L 5,4 L 12,0 Z",
            _ => "M 6,3 L 21,12 L 6,21 Z"
        });
        DescribeButton(PlayButton, state, "Space");
    }
    private void UpdateVolumeIcon(int volume)
    {
        VolumeIcon.Data = Geometry.Parse(volume == 0
            ? "M 3,9 L 7,9 L 12,4 L 12,20 L 7,15 L 3,15 Z M 16,9 L 22,15 M 22,9 L 16,15"
            : "M 3,9 L 7,9 L 12,4 L 12,20 L 7,15 L 3,15 Z M 16,8 Q 20,12 16,16 M 19,4 Q 26,12 19,20");
        DescribeButton(MuteButton, volume == 0 ? "Unmute" : "Mute", "M");
    }
    private void Captions_Click(object? sender, RoutedEventArgs e)
    {
        PlayerSettingsPanel.IsVisible = true;
        RefreshTracks();
        // Wait for the newly visible panel to measure before scrolling to its track controls.
        Dispatcher.UIThread.Post(() => AudioSettingsHeading.BringIntoView(), DispatcherPriority.Loaded);
        RevealPlayerControls();
    }
    private void RefreshTracks()
    {
        if (player == null || currentPath == null || player.State is VLCState.Opening or VLCState.Stopped) return;
        var audio = player.AudioTrackDescription.Where(x => x.Id >= 0).Select(x => new TrackOption(x.Id, x.Name)).ToArray();
        if (useTextSubtitles && player.Spu >= 0) player.SetSpu(-1);
        var subtitles = new List<TrackOption> { new(-1, "Off") };
        subtitles.AddRange(player.SpuDescription.Where(x => x.Id >= 0).Select(x => new TrackOption(x.Id, x.Name)));
        if (textSubtitles != null) subtitles.Add(new(TextSubtitleId, "External · " + textSubtitleName));
        var signature = string.Join('|', audio.Select(x => $"a{x.Id}:{x.Name}")) + string.Join('|', subtitles.Select(x => $"s{x.Id}:{x.Name}"));
        updatingTracks = true;
        try
        {
            if (signature != trackSignature)
            {
                trackSignature = signature;
                AudioTrackPicker.ItemsSource = audio.Length > 0 ? audio : new[] { new TrackOption(-1, "No audio tracks") };
                SubtitleTrackPicker.ItemsSource = subtitles;
                AudioTrackPicker.IsEnabled = audio.Length > 0;
            }
            AudioTrackPicker.SelectedItem = (AudioTrackPicker.ItemsSource as IEnumerable<TrackOption>)?.FirstOrDefault(x => x.Id == player.AudioTrack);
            SubtitleTrackPicker.SelectedItem = (SubtitleTrackPicker.ItemsSource as IEnumerable<TrackOption>)?.FirstOrDefault(x => x.Id == (useTextSubtitles ? TextSubtitleId : player.Spu));
        }
        finally { updatingTracks = false; }
    }
    private void AudioTrackChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (updatingTracks || player == null || AudioTrackPicker.SelectedItem is not TrackOption track) return;
        if (!player.SetAudioTrack(track.Id)) PlayerNotice("This audio track could not be selected.");
        else PlayerNotice("Audio: " + track.Name);
    }
    private void SubtitleTrackChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (updatingTracks || player == null || SubtitleTrackPicker.SelectedItem is not TrackOption track) return;
        useTextSubtitles = track.Id == TextSubtitleId;
        if (useTextSubtitles)
        {
            player.SetSpu(-1);
            PlayerNotice("Subtitles: " + track.Name);
        }
        else if (!player.SetSpu(track.Id)) PlayerNotice("This subtitle track could not be selected.");
        else PlayerNotice("Subtitles: " + track.Name);
        if (!useTextSubtitles) player.SetSpuDelay(subtitleDelayMs * 1000);
        UpdatePlayerOverlay();
    }
    private async Task LoadSubtitle(string path)
    {
        if (player == null || currentPath == null) { PlayerNotice("Open a video before adding subtitles."); return; }
        var generation = mediaGeneration;
        try
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".srt" or ".vtt")
            {
                if (new FileInfo(path).Length > 8 * 1024 * 1024) { PlayerNotice("This subtitle file is too large to load."); return; }
                var contents = await File.ReadAllTextAsync(path);
                var document = await Task.Run(() => SubtitleDocument.Parse(contents));
                if (generation != mediaGeneration) return;
                textSubtitles = document; textSubtitleName = Path.GetFileName(path); useTextSubtitles = true;
                player.SetSpu(-1);
                trackSignature = "";
                RefreshTracks();
                PlayerNotice("Subtitles loaded: " + textSubtitleName);
            }
            else
            {
                if (!player.AddSlave(MediaSlaveType.Subtitle, new Uri(path).AbsoluteUri, true)) { PlayerNotice("This subtitle file could not be loaded."); return; }
                useTextSubtitles = false;
                trackSignature = "";
                PlayerNotice("Subtitle file added. Select its track if it does not appear automatically.");
            }
        }
        catch (FormatException) { PlayerNotice("No readable subtitle cues were found in this file."); }
        catch (Exception) { if (generation == mediaGeneration) PlayerNotice("Could not read this subtitle file."); }
        RevealPlayerControls();
    }
    private void SubtitleDelayChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!playerUiReady) return;
        subtitleDelayMs = (long)((SubtitleDelayInput.Value ?? 0) * 1000);
        if (player != null && !useTextSubtitles) player.SetSpuDelay(subtitleDelayMs * 1000);
        UpdatePlayerOverlay();
    }
    private void SubtitleStyleChanged(object? sender, SelectionChangedEventArgs e) => ApplySubtitleAppearance();
    private void SubtitleBackgroundChanged(object? sender, RoutedEventArgs e) => ApplySubtitleAppearance();
    private void ApplySubtitleAppearance()
    {
        if (!playerUiReady) return;
        var size = new[] { 20, 28, 36, 44 }[Math.Clamp(SubtitleSizePicker.SelectedIndex, 0, 3)];
        var color = new[] { "White", "Yellow", "Mint" }[Math.Clamp(SubtitleColorPicker.SelectedIndex, 0, 2)];
        preferences = preferences with { SubtitleSize = size, SubtitleColor = color, SubtitleBackground = SubtitleBackgroundCheck.IsChecked == true };
        CaptionText.FontSize = size * Math.Clamp(PlayerOverlay.Bounds.Height / 720, .75, 2);
        CaptionText.Foreground = Brush.Parse(color switch { "Yellow" => "#FFE66D", "Mint" => "#A7F3D0", _ => "White" });
        CaptionPanel.Background = preferences.SubtitleBackground ? Brush.Parse("#B0000000") : Brushes.Transparent;
        PositionCaptions();
    }
    private void VolumeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name != "Value") return;
        var volume = (int)VolumeBar.Value;
        preferences = preferences with { Volume = volume };
        if (volume > 0) lastNonzeroVolume = volume;
        if (player != null) player.Volume = volume;
        UpdateVolumeIcon(volume);
    }
    private void Mute_Click(object? sender, RoutedEventArgs e)
    {
        VolumeBar.Value = VolumeBar.Value > 0 ? 0 : lastNonzeroVolume;
        RevealPlayerControls();
    }
    private static bool IsSubtitleFile(string path) => new[] { ".srt", ".vtt", ".ass", ".ssa", ".sub", ".idx" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    private void MediaDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        RevealPlayerControls();
    }
    private async void MediaDropped(object? sender, DragEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        var files = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().Select(x => x.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToArray() ?? [];
        if (files.Length == 0) { Status("Drop a local media or subtitle file."); return; }
        var media = files.FirstOrDefault(x => !IsSubtitleFile(x));
        if (media != null) Play(media, Path.GetFileName(media));
        var subtitle = files.FirstOrDefault(IsSubtitleFile);
        if (subtitle != null) await LoadSubtitle(subtitle);
    }
    private void OpenLaunchRequest(string[] args)
    {
        string? media = null, subtitle = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--subtitle" && i + 1 < args.Length)
            {
                var candidate = args[++i];
                if (File.Exists(candidate)) subtitle = candidate;
            }
            else if (media == null && File.Exists(args[i])) media = args[i];
        }
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        if (media != null) Play(media, Path.GetFileName(media));
        if (subtitle != null) _ = LoadSubtitle(subtitle);
        RevealPlayerControls();
    }
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || activePage != "Player") return;
        var ancestors = e.Source is Visual visual ? visual.GetVisualAncestors().Prepend(visual).ToArray() : [];
        if (ancestors.Any(x => x is TextBox or ComboBox or NumericUpDown or Slider)) return;
        switch (e.Key)
        {
            case Key.Space when !ancestors.Any(x => x is Button or CheckBox): Play_Click(null, new()); break;
            case Key.Left: Skip(-10000); break;
            case Key.Right: Skip(10000); break;
            case Key.F: case Key.F11: SetFullscreen(WindowState != WindowState.FullScreen); break;
            case Key.M: Mute_Click(null, new()); break;
            case Key.S: Settings_Click(null, new()); break;
            case Key.G: SubtitleDelayInput.Value = Math.Max(-300, (SubtitleDelayInput.Value ?? 0) - .05m); PlayerNotice("Subtitle delay: " + SubtitleDelayInput.Value?.ToString("0.00") + "s"); break;
            case Key.H: SubtitleDelayInput.Value = Math.Min(300, (SubtitleDelayInput.Value ?? 0) + .05m); PlayerNotice("Subtitle delay: " + SubtitleDelayInput.Value?.ToString("0.00") + "s"); break;
            case Key.Escape:
                if (PlayerSettingsPanel.IsVisible) PlayerSettingsPanel.IsVisible = false;
                else if (WindowState == WindowState.FullScreen) SetFullscreen(false);
                else return;
                RevealPlayerControls();
                break;
            default: return;
        }
        e.Handled = true;
    }
}
