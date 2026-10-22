using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BingeX;
internal static class AppSettingsChecks
{
    public static void Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object? Get(string n)=>typeof(MainWindow).GetField(n,flags)!.GetValue(window);
        object? Call(string n,params object?[] args)=>typeof(MainWindow).GetMethod(n,flags)!.Invoke(window,args);
        T Control<T>(string n) where T:Control=>window.FindControl<T>(n)!;
        void Require(bool value,string message){if(!value)throw new Exception(message);}
        var store=(LibraryStore)Get("store")!; var saved=store.Preferences();
        Call("AppSettings_Click",null,new RoutedEventArgs());
        Require(Control<ScrollViewer>("AppSettingsPage").IsVisible && !Control<ScrollViewer>("LocalLibraryPage").IsVisible && !Control<ScrollViewer>("LibraryPage").IsVisible,"Settings is a separate page available offline.");
        void PickLanguage(string name,string label)
        {
            var picker=Control<ComboBox>(name);picker.SelectedItem=picker.Items.Cast<object>().Single(x=>x.ToString()==label);
        }
        PickLanguage("SettingsAudioLanguage","Hindi"); PickLanguage("SettingsSubtitleLanguage","Off");
        Control<ComboBox>("SettingsSeek").SelectedIndex=3;
        Control<CheckBox>("SettingsResume").IsChecked=false; Control<CheckBox>("SettingsClickPause").IsChecked=false; Control<CheckBox>("SettingsAnimation").IsChecked=false;
        Control<ComboBox>("SettingsSubSize").SelectedIndex=2; Control<ComboBox>("SettingsSubColor").SelectedIndex=2; Control<CheckBox>("SettingsSubBackground").IsChecked=false;
        Control<ComboBox>("SettingsLibrarySort").SelectedIndex=1; Control<ComboBox>("SettingsLibraryGrouping").SelectedIndex=2; Control<ComboBox>("SettingsSpeed").SelectedIndex=3;
        Control<NumericUpDown>("SettingsVolume").Value=65; Control<CheckBox>("SettingsAutoSwitch").IsChecked=false;
        Call("SaveAppSettings_Click",null,new RoutedEventArgs());
        using(var reopened=new LibraryStore(store.DataDirectory))
        {
            var value=reopened.Preferences();
            Require(value.SeekSeconds==30 && !value.ResumePlayback && !value.ClickToPause && !value.StartupAnimation && value.Volume==65 && value.DefaultSpeedIndex==3 && !value.AutoSwitchSources,"Playback settings survive reopening.");
            Require(value.PreferredAudioLanguage=="hindi" && value.PreferredSubtitleLanguage=="off","Global language defaults survive reopening.");
            Require(value.SubtitleSize==36 && value.SubtitleColor=="Mint" && !value.SubtitleBackground && value.LocalLibrarySort==1 && value.LocalLibraryGrouping==2,"Subtitle and library settings save together without proxy controls overwriting values.");
        }
        var oldContext=Get("currentContext");
        var languageContext=new MediaContext("global-language-fixture","series","Languages","","fixture","ep1");
        typeof(MainWindow).GetField("currentContext",flags)!.SetValue(window,languageContext);
        Require((string)Call("EffectiveAudioLanguage",languageContext)! =="hindi" && ((SubtitlePreference)Call("EffectiveSubtitlePreference")!).Mode=="off","New titles inherit global audio and subtitle defaults.");
        store.SaveShowAudioPreference(languageContext,new("english","English"));store.SaveSubtitlePreference(languageContext,new("embedded","english"));
        Require((string)Call("EffectiveAudioLanguage",languageContext)! =="english" && ((SubtitlePreference)Call("EffectiveSubtitlePreference")!).Language=="english","Saved show choices override global defaults.");
        typeof(MainWindow).GetField("currentContext",flags)!.SetValue(window,oldContext);
        Require(Control<TextBlock>("SeekBackLabel").Text=="30" && Control<ComboBox>("SubtitleSizePicker").SelectedIndex==2 && Control<ComboBox>("LocalGrouping").SelectedIndex==2,"Live controls match saved settings.");
        Call("Page","Library");
        var typing=new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.OemQuestion,KeyModifiers=KeyModifiers.Shift,Source=Control<TextBox>("LocalVideoSearch")};
        Call("OnKey",window,typing);
        Require(!typing.Handled && !Control<ScrollViewer>("AppSettingsPage").IsVisible,"Shortcut help must not intercept question marks in text fields.");
        var help=new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.OemQuestion,KeyModifiers=KeyModifiers.Shift,Source=window};
        Call("OnKey",window,help);
        Require(help.Handled && Control<Expander>("ShortcutsSection").IsExpanded && Control<TextBlock>("ShortcutList").Text!.Contains("30 seconds"),"Question mark opens the shortcut reference with the current seek interval.");
        typeof(MainWindow).GetField("preferences",flags)!.SetValue(window,saved);
        Call("PopulateAppSettings"); Call("SaveAppSettings_Click",null,new RoutedEventArgs());
        Call("Page","Library");
        log("PASS: dedicated offline Settings, persisted playback/subtitle/library choices, live control sync and typing-safe shortcuts help.");
    }
}
