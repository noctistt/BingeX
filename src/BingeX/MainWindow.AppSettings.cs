using Avalonia.Controls;
using Avalonia.Interactivity;
namespace BingeX;
public partial class MainWindow
{
    private string settingsReturnPage="Library";
    private void SetupAppSettings()
    {
        SetupAppearance();
        SpeedBox.SelectedIndex=preferences.DefaultSpeedIndex;
        AutoNextSource.IsChecked=preferences.AutoSwitchSources;
        AutoNextSource.IsCheckedChanged+=(_,_)=>{preferences=preferences with{AutoSwitchSources=AutoNextSource.IsChecked==true};store.SavePreferences(preferences);};
    }
    private void AppSettings_Click(object? sender,RoutedEventArgs e)
    {
        if(activePage!="Settings")settingsReturnPage=activePage;
        Page("Settings");
    }
    private void PopulateAppSettings()
    {
        PopulateLanguageDefaults();
        PopulateAppearance();
        SettingsSeek.SelectedIndex=Array.IndexOf(new[]{5,10,15,30,60},preferences.SeekSeconds);
        SettingsResume.IsChecked=preferences.ResumePlayback; SettingsClickPause.IsChecked=preferences.ClickToPause;
        SettingsAnimation.IsChecked=preferences.StartupAnimation; SettingsAutoplay.IsChecked=preferences.AutoplayEpisodes;SettingsQueueAutoplay.IsChecked=preferences.AutoplayLocalQueue;
        SettingsSpeed.SelectedIndex=preferences.DefaultSpeedIndex; SettingsVolume.Value=preferences.Volume;
        SettingsSubSize.SelectedIndex=Array.IndexOf(new[]{20,28,36,44},preferences.SubtitleSize);
        SettingsSubColor.SelectedIndex=Array.IndexOf(new[]{"White","Yellow","Mint"},preferences.SubtitleColor);
        SettingsSubBackground.IsChecked=preferences.SubtitleBackground;
        SettingsLibrarySort.SelectedIndex=preferences.LocalLibrarySort; SettingsLibraryGrouping.SelectedIndex=preferences.LocalLibraryGrouping;
        SettingsAutoSwitch.IsChecked=preferences.AutoSwitchSources;
        SettingsSaved.Text="Changes apply when you choose Save settings.";
        ShortcutList.Text=$"Space — Play / pause\nLeft / Right — Seek {preferences.SeekSeconds} seconds\nF / F11 — Fullscreen\nM — Mute\nC — Toggle subtitles\nS — Quick playback settings\nG / H — Subtitle timing ±50 ms\nAlt + Left — Back\nEscape — Close player settings or leave fullscreen\n? — Open this shortcuts reference\nDouble-click video — Fullscreen";
    }
    private void SaveAppSettings_Click(object? sender,RoutedEventArgs e)
    {
        preferences=preferences with{
            AccentColor=accentNames[Math.Clamp(SettingsAccent.SelectedIndex,0,4)],CompactBrowsing=SettingsDensity.SelectedIndex==1,ReducedMotion=SettingsReducedMotion.IsChecked==true,
            PreferredAudioLanguage=(SettingsAudioLanguage.SelectedItem as LanguageChoice)?.Key ?? "",
            PreferredSubtitleLanguage=(SettingsSubtitleLanguage.SelectedItem as LanguageChoice)?.Key ?? "",
            SeekSeconds=new[]{5,10,15,30,60}[Math.Clamp(SettingsSeek.SelectedIndex,0,4)],
            AutoplayLocalQueue=SettingsQueueAutoplay.IsChecked==true,
            ResumePlayback=SettingsResume.IsChecked==true,ClickToPause=SettingsClickPause.IsChecked==true,
            StartupAnimation=SettingsAnimation.IsChecked==true,AutoplayEpisodes=SettingsAutoplay.IsChecked==true,
            DefaultSpeedIndex=Math.Clamp(SettingsSpeed.SelectedIndex,0,5),Volume=(int)(SettingsVolume.Value??80),
            SubtitleSize=new[]{20,28,36,44}[Math.Clamp(SettingsSubSize.SelectedIndex,0,3)],SubtitleColor=new[]{"White","Yellow","Mint"}[Math.Clamp(SettingsSubColor.SelectedIndex,0,2)],SubtitleBackground=SettingsSubBackground.IsChecked==true,
            LocalLibrarySort=Math.Clamp(SettingsLibrarySort.SelectedIndex,0,2),LocalLibraryGrouping=Math.Clamp(SettingsLibraryGrouping.SelectedIndex,0,2),AutoSwitchSources=SettingsAutoSwitch.IsChecked==true};
        ApplyPlayerPreferences(preferences);
    }
    private void ApplyPlayerPreferences(PlayerPreferences desired)
    {
        preferences=desired;
        PopulateAppSettings();
        SubtitleSizePicker.SelectedIndex=SettingsSubSize.SelectedIndex; SubtitleColorPicker.SelectedIndex=SettingsSubColor.SelectedIndex; SubtitleBackgroundCheck.IsChecked=desired.SubtitleBackground;
        VolumeBar.Value=desired.Volume; SpeedBox.SelectedIndex=desired.DefaultSpeedIndex;
        IncludeTorrentSourcesCheck.IsChecked=desired.IncludeTorrentSources;SearchAllProvidersSwitch.IsChecked=desired.SearchAllStreamProviders;
        QueueAutoplayCheck.IsChecked=desired.AutoplayLocalQueue;
        AutoplayEpisodesCheck.IsChecked=desired.AutoplayEpisodes; AutoNextSource.IsChecked=desired.AutoSwitchSources;
        LocalSort.SelectedIndex=desired.LocalLibrarySort; LocalGrouping.SelectedIndex=desired.LocalLibraryGrouping;
        preferences=desired; store.SavePreferences(preferences); PopulateAppSettings(); SettingsSaved.Text="Settings saved. Language defaults apply to newly opened videos.";
        ApplyAppearance(); RefreshLocalLibrary();
        UpdateSeekLabels();
    }
    private void UpdateSeekLabels()
    {
        SeekBackLabel.Text=SeekForwardLabel.Text=preferences.SeekSeconds.ToString();
        DescribeButton(SeekBackButton,$"Back {preferences.SeekSeconds} seconds","Left");
        DescribeButton(SeekForwardButton,$"Forward {preferences.SeekSeconds} seconds","Right");
    }
    private void OpenShortcutHelp()
    {
        AppSettings_Click(null,new()); ShortcutsSection.IsExpanded=true; Avalonia.Threading.Dispatcher.UIThread.Post(()=>ShortcutsSection.BringIntoView(),Avalonia.Threading.DispatcherPriority.Loaded);
    }
}
