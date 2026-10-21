using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LibVLCSharp.Shared;

namespace BingeX;

public partial class MainWindow
{
    private long audioDelayMs;
    private string? externalAudioPath;
    private IReadOnlyList<StreamAudioOption> currentStreamAudioOptions = [];
    private bool settingStreamAudio;
    private string? SetupStreamAudio(IReadOnlyList<StreamAudioOption>? options, string? fallback)
    {
        currentStreamAudioOptions = options ?? [];
        var saved = pendingAudioSelection;
        var language=saved?.Language ?? "";
        if(language.Length==0 && saved==null && currentContext!=null)language=store.ShowAudioPreference(currentContext).Language;
        var languageMatch=currentStreamAudioOptions.FirstOrDefault(x=>language.Length>0 && SubtitlePreference.LanguageKey(x.Language)==language);
        var selected = currentStreamAudioOptions.FirstOrDefault(x => saved?.Id == -2 && x.Label == saved.Name)
            ?? languageMatch ?? currentStreamAudioOptions.FirstOrDefault();
        if(languageMatch!=null)pendingAudioSelection=null;
        if (saved?.Id == -2) pendingAudioSelection = null;
        settingStreamAudio = true;
        try
        {
            StreamAudioSection.IsVisible = currentStreamAudioOptions.Count > 0;
            StreamAudioPicker.ItemsSource = currentStreamAudioOptions;
            StreamAudioPicker.SelectedItem = selected;
        }
        finally { settingStreamAudio = false; }
        return selected?.Url ?? fallback;
    }
    private async void StreamAudioChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (settingStreamAudio || NativePlaybackBusy || player == null || StreamAudioPicker.SelectedItem is not StreamAudioOption option) return;
        var generation = mediaGeneration;
        StreamAudioPicker.IsEnabled = false;
        PlayerNotice("Loading audio: " + option.Label + "…");
        var success = false;
        try
        {
            await QueuePlayerCommand(native => success = native.AddSlave(MediaSlaveType.Audio, option.Url, true));
            if (generation != mediaGeneration) return;
            if (success)
            {
                currentStreamAudio = option.Url;
                externalAudioPath = null;
                pendingAudioSelection = null; audioRestoreUntil = null; trackSignature = "";
                if (currentHistoryKey != null) store.SaveAudioSelection(currentHistoryKey, new(-2, option.Label));
                RememberShowAudio(option.Language,option.Label);
                PlayerNotice("Audio: " + option.Label);
            }
            else
            {
                settingStreamAudio = true;
                try { StreamAudioPicker.SelectedItem = currentStreamAudioOptions.FirstOrDefault(x => x.Url == currentStreamAudio); }
                finally { settingStreamAudio = false; }
                PlayerNotice("This audio language could not be loaded. Try another track.");
            }
        }
        catch
        {
            if (generation == mediaGeneration)
            {
                settingStreamAudio = true;
                try { StreamAudioPicker.SelectedItem = currentStreamAudioOptions.FirstOrDefault(x => x.Url == currentStreamAudio); }
                finally { settingStreamAudio = false; }
                PlayerNotice("This audio language could not be loaded. Try another track.");
            }
        }
        finally { StreamAudioPicker.IsEnabled = true; }
    }
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
