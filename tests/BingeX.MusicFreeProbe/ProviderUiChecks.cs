using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BingeX;
using LibVLCSharp.Shared;
internal static class ProviderUiChecks
{
    public static int Run(string link,string query)
    {
        var data=Environment.GetEnvironmentVariable("BINGEX_DATA_DIRECTORY");
        if(data==null||!Path.GetFileName(data).StartsWith("musicfree-ui-test-"))return 2;
        Directory.CreateDirectory(data);var passed=false;
        var builder=(AppBuilder)typeof(MainWindow).Assembly.GetType("BingeX.Program")!.GetMethod("CreateAppBuilder")!.Invoke(null,null)!;
        _=Task.Run(async()=>{await Task.Delay(2500);await Dispatcher.UIThread.InvokeAsync(async()=>{
            var window=(MainWindow)((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            object? Call(string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,args);
            object? Get(string name)=>typeof(MainWindow).GetField(name,flags)!.GetValue(window);
            T C<T>(string name)where T:Control=>window.FindControl<T>(name)!;
            void Check(bool value,string message){if(!value)throw new Exception(message);}
            async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(45);while(!ready()){if(DateTime.UtcNow>end)throw new TimeoutException();await Task.Delay(100);}}
            var stage="startup";
            try{
                await Wait(()=>!C<Border>("StartupSplash").IsVisible);C<Slider>("VolumeBar").Value=0;
                await (Task)Call("SetOnlineMode",true)!;Call("Page","Add-ons");Call("SelectAddonCategory",true);C<CheckBox>("MusicFreeTrust").IsChecked=true;
                stage="import";await (Task)Call("InstallMusicFree",link)!;Check(Get("musicFreeCode")!=null,"Import failed");
                Call("Page","Music");await Wait(()=>!(bool)Get("musicFreeWorking")!);
                stage="search";C<TextBox>("MusicFreeSearch").Text=query;await (Task)Call("SearchMusicFree",false)!;Check(C<WrapPanel>("MusicFreeResultsPanel").Children.Count>=2,"Insufficient search results");
                stage="play";C<WrapPanel>("MusicFreeResultsPanel").Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>!(bool)Get("musicFreeWorking")!&&((MediaPlayer)Get("player")!).Time>3000);Check(C<Grid>("MusicNowBar").IsVisible,"Missing music controls");
                var first=Get("currentPath");Call("OpenMusicNow_Click",null,new RoutedEventArgs());Call("CancelMusicLyrics");Check(C<ScrollViewer>("MusicNowPage").IsVisible,"Wrong player destination");
                stage="pause";Call("Play_Click",null,new RoutedEventArgs());await Wait(()=>((MediaPlayer)Get("player")!).State==VLCState.Paused);Call("Play_Click",null,new RoutedEventArgs());await Wait(()=>((MediaPlayer)Get("player")!).IsPlaying);
                stage="seek";await Wait(()=>((MediaPlayer)Get("player")!).IsSeekable);await (Task)Call("QueuePlayerCommand",(Action<MediaPlayer>)(p=>p.Time=8000))!;await Wait(()=>((MediaPlayer)Get("player")!).Time>=8000);
                stage="seek-settle";await Task.Delay(2000);Check(((MediaPlayer)Get("player")!).Time>=8500,"Seek did not hold its position");
                using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(data,"provider-now-playing.png"));}
                stage="next";Check((bool)Call("MoveMusicFree",1)!,"Next rejected");await Wait(()=>!(bool)Get("musicFreeWorking")!&&!Equals(first,Get("currentPath"))&&((MediaPlayer)Get("player")!).Time>3000);Call("CancelMusicLyrics");
                stage="offline";await (Task)Call("SetOnlineMode",false)!;Check(!C<Border>("MusicFreePanel").IsVisible&&C<Grid>("MusicNowBar").IsVisible,"Offline must hide provider and retain controls");
                passed=true;File.WriteAllText(Path.Combine(data,"result.txt"),"PASS: HTTPS import, search, two distinct tracks, muted native playback, dedicated music view, pause/resume, seeking, next track and offline retention.");
            }catch(Exception e){File.WriteAllText(Path.Combine(data,"result.txt"),"FAIL: "+stage+" / "+e.GetType().Name);}
            finally{window.Close();}
        });});
        builder.StartWithClassicDesktopLifetime([]);return passed?0:1;
    }
}
