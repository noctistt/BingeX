using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using BingeX;
internal static class PlayerPolishChecks
{
    public static async Task Run(MainWindow window,Action<string> log)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        void Call(string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,args);
        T Control<T>(string name) where T:Control=>window.FindControl<T>(name)!;
        void Require(bool result,string message){if(!result)throw new Exception(message);}
        Call("AudioSettingsTab_Click",null,new RoutedEventArgs());
        Require(Control<Border>("PlayerSettingsPanel").IsVisible && Control<StackPanel>("PlaybackSettingsSection").IsVisible && !Control<StackPanel>("SubtitleSettingsSection").IsVisible,"Audio tab opens its own settings section.");
        Call("SubtitleSettingsTab_Click",null,new RoutedEventArgs());
        Require(Control<Border>("PlayerSettingsPanel").IsVisible && Control<StackPanel>("SubtitleSettingsSection").IsVisible && Control<Button>("SubtitleSettingsTab").Classes.Contains("active"),"Switching tabs keeps the panel open and highlights the current section.");
        Call("SubtitleSettingsTab_Click",null,new RoutedEventArgs());
        Require(Control<Border>("PlayerSettingsPanel").IsVisible,"Clicking the selected tab must not close settings.");
        var native=Control<ComboBox>("AudioTrackPicker").Items.Cast<object>().FirstOrDefault(x=>(string?)x.GetType().GetProperty("DisplayName")?.GetValue(x) is { } name && name.StartsWith("Embedded"));
        Require(native!=null,"Embedded track choices identify their source without changing track IDs.");
        Control<Border>("PlayerSettingsPanel").IsVisible=false;
        var state=window.WindowState; var width=window.Width; var height=window.Height;
        var next=Control<Button>("NextEpisodeButton"); var nextVisible=next.IsVisible;
        try
        {
            window.WindowState=WindowState.Normal;
            window.Width=960; window.Height=680;
            next.IsVisible=true; Call("RevealPlayerControls");
            await Task.Delay(300);
            var controls=Control<Border>("PlayerControls");
            foreach(var button in controls.GetVisualDescendants().OfType<Button>().Where(x=>x.IsEffectivelyVisible))
            {
                var at=button.TranslatePoint(default,controls);
                Require(at!=null && at.Value.X>=-1 && at.Value.X+button.Bounds.Width<=controls.Bounds.Width+1,"A player action is clipped at minimum window width.");
            }
            Require(Control<TextBlock>("PlayerLoadingDetail").Text!.Contains("Layout fixture"),"Opening state retains the actual video title.");
            if(Environment.GetEnvironmentVariable("BINGEX_PLAYER_POLISH_HOLD")=="1")
            {
                window.Width=1240; window.Height=820; window.Title="BingeX player polish preview";
                Call("SubtitleSettingsTab_Click",null,new RoutedEventArgs());
                log("VISUAL: player polish preview ready");
                await Task.Delay(45000);
            }
        }
        finally
        {
            next.IsVisible=nextVisible;window.Width=width;window.Height=height;window.WindowState=state;
            Control<Border>("PlayerSettingsPanel").IsVisible=false;
        }
        log("PASS: audio/subtitle tabs, embedded track labels, minimum-width transport actions and title-specific opening state.");
    }
}
