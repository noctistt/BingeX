using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
namespace BingeX;
public partial class MainWindow
{
    private PlayerPreferences? pendingSettingsBackup;
    private async void ExportSettings_Click(object? sender,RoutedEventArgs e)
    {
        try
        {
            var file=await StorageProvider.SaveFilePickerAsync(new(){Title="Export BingeX settings",SuggestedFileName="BingeX-settings.json",DefaultExtension="json",ShowOverwritePrompt=true,FileTypeChoices=[new FilePickerFileType("BingeX settings"){Patterns=["*.json"]}]});
            if(file==null || localClosing)return;
            await using var output=await file.OpenWriteAsync();
            output.SetLength(0);
            await using var writer=new StreamWriter(output);
            await writer.WriteAsync(PreferenceBackup.Export(store.Preferences()));
            BackupMessage.Text="Settings exported. Provider URLs, credentials, media folders and watch history were excluded.";
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {BackupMessage.Text="Could not export settings. Choose a writable file location.";}
    }
    private async void ImportSettings_Click(object? sender,RoutedEventArgs e)
    {
        try
        {
            var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Import BingeX settings",AllowMultiple=false,FileTypeFilter=[new FilePickerFileType("BingeX settings"){Patterns=["*.json"]}]});
            if(files.FirstOrDefault() is not {} file || localClosing)return;
            await using var input=await file.OpenReadAsync();
            using var memory=new MemoryStream();var buffer=new byte[4096];
            while(true){var count=await input.ReadAsync(buffer);if(count==0)break;if(memory.Length+count>PreferenceBackup.MaxBytes)throw new InvalidDataException();memory.Write(buffer,0,count);}
            StageSettingsBackup(PreferenceBackup.Import(System.Text.Encoding.UTF8.GetString(memory.ToArray())),"Import settings preview");
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException)
        {CancelSettingsPreview();BackupMessage.Text="Could not import this file. Choose a valid BingeX settings backup (version 1, up to 64 KB). No settings were changed.";}
    }
    private void ResetSettings_Click(object? sender,RoutedEventArgs e)=>StageSettingsBackup(new(),"Reset preferences preview");
    private void StageSettingsBackup(PlayerPreferences proposed,string title)
    {
        pendingSettingsBackup=proposed;
        var changes=PreferenceBackup.Changes(preferences,proposed);
        BackupPreviewTitle.Text=title;
        BackupPreviewChanges.Text=changes.Length==0?"These preferences already match your saved settings.":changes;
        ApplySettingsBackupButton.IsEnabled=changes.Length>0;BackupPreview.IsVisible=true;
        BackupMessage.Text="Review changes below. Apply replaces preferences and discards unsaved settings edits; Cancel keeps your current preferences.";
    }
    private void ApplySettingsBackup_Click(object? sender,RoutedEventArgs e)
    {
        if(pendingSettingsBackup is not {} proposed)return;
        ApplyPlayerPreferences(proposed);CancelSettingsPreview();
        BackupMessage.Text="Preferences applied. Your media, history, provider data, online-mode state and saved show/track choices were kept.";
    }
    private void CancelSettingsPreview_Click(object? sender,RoutedEventArgs e){CancelSettingsPreview();BackupMessage.Text="Preview cancelled. No preferences were changed.";}
    private void CancelSettingsPreview(){pendingSettingsBackup=null;BackupPreview.IsVisible=false;}
}
