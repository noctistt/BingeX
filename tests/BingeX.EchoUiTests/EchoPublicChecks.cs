using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BingeX;
internal static class EchoPublicChecks
{
    // Explicit opt-in live provider test; the main harness requires an isolated echo-ui-test-* directory.
    public static async Task Run(MainWindow window,string[] args,EchoPackage package)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object? Call(string name,params object?[] a)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,a);
        object? Get(string name)=>typeof(MainWindow).GetField(name,flags)!.GetValue(window);
        void Set(string name,object value)=>typeof(MainWindow).GetField(name,flags)!.SetValue(window,value);
        T C<T>(string name)where T:Control=>window.FindControl<T>(name)!;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        async Task Wait(Func<bool> ready,string message){var end=DateTime.UtcNow.AddSeconds(60);while(!ready()){if(DateTime.UtcNow>end)throw new Exception(message);await Task.Delay(100);}}
        Set("echoRuntimeFile",Path.GetFullPath(args[1]));C<CheckBox>("EchoTrust").IsChecked=true;C<CheckBox>("EchoCompatibilityCheck").IsChecked=true;C<Slider>("VolumeBar").Value=0;
        await (Task)Call("ActivateEcho",package)!;
        Check(Get("echoBridge")!=null&&C<WrapPanel>("EchoMusicItems").Children.Count>0,"Enhanced provider activation/home failed.");
        Check(C<Button>("EchoSearchButton").IsEnabled,"Inherited search capability must enable the search button.");
        C<TextBox>("EchoQuery").Text=args[2];Call("SearchEcho_Click",null,new RoutedEventArgs());await Wait(()=>!(bool)Get("echoWorking")!,"Public search timed out.");
        var track=C<WrapPanel>("EchoMusicItems").Children.OfType<Button>().FirstOrDefault(x=>x.Content?.ToString()?.EndsWith(" · track")==true);
        Check(track!=null,"Public search must show tracks.");track!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(()=>Get("player") is LibVLCSharp.Shared.MediaPlayer p&&p.IsPlaying&&p.Time>3000,"Public provider playback did not advance.");
        Check(C<Grid>("MusicNowBar").IsVisible&&Get("currentHistoryKey")==null,"Music controls/privacy must be retained.");
        Call("StopEcho");C<CheckBox>("EchoCompatibilityCheck").IsChecked=false;
        await (Task)Call("ActivateEcho",package)!;
        Check((Get("echoBridge")!=null)==(args[3]=="--public-working"),"Turning enhanced mode off must use the expected standard-engine path.");
    }
}
