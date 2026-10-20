using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private long audioDelayMs;
    private string? externalAudioPath;
    private async void LoadAudio_Click(object? sender, RoutedEventArgs e)
    {
        if (NativePlaybackBusy || player == null || currentPath == null) { PlayerNotice("Open a video before adding audio."); return; }
        var generation = mediaGeneration;
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Choose an audio track", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Audio files") { Patterns = ["*.aac", "*.ac3", "*.eac3", "*.dts", "*.flac", "*.m4a", "*.mp3", "*.ogg", "*.opus", "*.wav", "*.mka"] }]
        });
        if (generation == mediaGeneration && files.FirstOrDefault()?.TryGetLocalPath() is { } path) LoadExternalAudio(path);
    }
    private void LoadExternalAudio(string path)
    {
        if (NativePlaybackBusy || player == null || currentPath == null) { PlayerNotice("Open a video before adding audio."); return; }
        try
        {
            if (!File.Exists(path)) { PlayerNotice("This audio file is no longer available."); return; }
            if (!player.AddSlave(MediaSlaveType.Audio, new Uri(Path.GetFullPath(path)).AbsoluteUri, true))
            { PlayerNotice("This audio file could not be loaded."); return; }
            externalAudioPath = Path.GetFullPath(path);
            ExternalAudioLabel.Text = "Loaded: " + Path.GetFileName(path);
            pendingAudioSelection = null; audioRestoreUntil = null;
            trackSignature = "";
            PlayerNotice("Audio file added. Select its track if it does not switch automatically.");
            RevealPlayerControls();
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        { PlayerNotice("This audio file could not be loaded."); }
    }
    private void AudioDelayChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!playerUiReady) return;
        audioDelayMs = (long)((AudioDelayInput.Value ?? 0) * 1000);
        if (!NativePlaybackBusy && player != null && currentPath != null && !player.SetAudioDelay(audioDelayMs * 1000))
            PlayerNotice("Audio timing could not be changed for this source.");
    }
}
