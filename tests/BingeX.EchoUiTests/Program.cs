using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BingeX;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var data=Environment.GetEnvironmentVariable("BINGEX_DATA_DIRECTORY");
        if(args.Length!=1||string.IsNullOrWhiteSpace(data)||!Path.GetFileName(data).StartsWith("echo-ui-test-"))return 2;
        Directory.CreateDirectory(data);var passed=false;
        var builder=(AppBuilder)typeof(MainWindow).Assembly.GetType("BingeX.Program")!.GetMethod("CreateAppBuilder")!.Invoke(null,null)!;
        _=Task.Run(async()=>
        {
            await Task.Delay(3000);
            await Dispatcher.UIThread.InvokeAsync(async()=>
            {
                var window=(MainWindow)((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                object? Call(string name,params object?[] a)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,a);
                T C<T>(string name)where T:Control=>window.FindControl<T>(name)!;
                void Check(bool value,string message){if(!value)throw new Exception(message);}
                try
                {
                    var deadline=DateTime.UtcNow.AddSeconds(15);
                    while(C<Border>("StartupSplash").IsVisible){if(DateTime.UtcNow>deadline)throw new Exception("Startup animation did not finish.");await Task.Delay(100);}
                    Check(!C<Border>("EchoPackagesPanel").IsVisible,"Offline mode must hide extensions.");
                    var item=await Task.Run(()=>new EchoPackages(data).Import(args[0]));
                    await (Task)Call("RefreshEchoPackages")!;
                    Check(C<StackPanel>("EchoPackageItems").Children.Count==1,"Imported package must render.");
                    await (Task)Call("SetOnlineMode",true)!;Call("Page","Add-ons");Call("SelectAddonCategory",true);await Task.Delay(200);
                    Check(C<Border>("EchoPackagesPanel").IsEffectivelyVisible&&C<Button>("EchoImportButton").IsEffectivelyVisible,"Music category must show import.");
                    Check(!C<StackPanel>("VideoAddonsSection").IsEffectivelyVisible,"Video addons must remain separate.");
                    using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(data,"echo-addons.png"));}
                    Call("SelectAddonCategory",false);Check(!C<Border>("EchoPackagesPanel").IsEffectivelyVisible,"Video category must hide music packages.");
                    await (Task)Call("SetOnlineMode",false)!;Check(new EchoPackages(data).List().Single().Sha256==item.Sha256,"Offline toggle must retain imported files.");
                    var row=(Grid)C<StackPanel>("EchoPackageItems").Children.Single();row.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Task.Delay(300);Check(new EchoPackages(data).List().Count==0&&C<StackPanel>("EchoPackageItems").Children.Count==0,"Remove must update disk and UI.");
                    passed=true;File.WriteAllText(Path.Combine(data,"result.txt"),"PASS: real Add-ons/Music UI, import rendering, category separation, online toggle retention and removal.");
                }
                catch(Exception e){File.WriteAllText(Path.Combine(data,"result.txt"),"FAIL: "+e);}
                finally{window.Close();}
            });
        });
        builder.StartWithClassicDesktopLifetime([]);return passed?0:1;
    }
}
