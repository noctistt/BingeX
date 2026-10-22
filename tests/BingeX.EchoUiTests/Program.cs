using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BingeX;

internal static class Program
{
    private sealed class LinkFixture(byte[] package):System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var response=new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content=request.RequestUri!.AbsolutePath=="/repo.json"?new System.Net.Http.StringContent("[{\"name\":\"Fixture music\",\"updateUrl\":\"https://api.github.com/repos/fixture/music/releases\"}]"):
                request.RequestUri.Host=="api.github.com"?new System.Net.Http.StringContent("{\"assets\":[{\"name\":\"fixture.eapk\",\"browser_download_url\":\"https://files.example/fixture.eapk\"}]}"):
                new System.Net.Http.ByteArrayContent(package);
            return Task.FromResult(response);
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        var data=Environment.GetEnvironmentVariable("BINGEX_DATA_DIRECTORY");
        if(args.Length is not(1 or 3)||string.IsNullOrWhiteSpace(data)||!Path.GetFileName(data).StartsWith("echo-ui-test-"))return 2;
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
                object? Get(string name)=>typeof(MainWindow).GetField(name,flags)!.GetValue(window);
                void Set(string name,object value)=>typeof(MainWindow).GetField(name,flags)!.SetValue(window,value);
                T C<T>(string name)where T:Control=>window.FindControl<T>(name)!;
                void Check(bool value,string message){if(!value)throw new Exception(message);}
                async Task Wait(Func<bool> ready,string message){var end=DateTime.UtcNow.AddSeconds(45);while(!ready()){if(DateTime.UtcNow>end)throw new Exception(message);await Task.Delay(100);}}
                void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                try
                {
                    var deadline=DateTime.UtcNow.AddSeconds(15);
                    while(C<Border>("StartupSplash").IsVisible){if(DateTime.UtcNow>deadline)throw new Exception("Startup animation did not finish.");await Task.Delay(100);}
                    Check(!C<Border>("EchoPackagesPanel").IsVisible,"Offline mode must hide extensions.");
                    var item=await Task.Run(()=>new EchoPackages(data).Import(args[0]));
                    await (Task)Call("RefreshEchoPackages")!;
                    Check(C<StackPanel>("EchoPackageItems").Children.Count==1,"Imported package must render.");
                    await (Task)Call("SetOnlineMode",true)!;Call("Page","Add-ons");Call("SelectAddonCategory",true);await Task.Delay(200);
                    Check(C<Border>("EchoPackagesPanel").IsEffectivelyVisible&&C<TextBox>("EchoLinkInput").IsEffectivelyVisible,"Music category must show link import.");
                    Check(!C<Expander>("EchoAdvanced").IsExpanded,"Manual setup must stay under Advanced by default.");
                    Check(!C<StackPanel>("VideoAddonsSection").IsEffectivelyVisible,"Video addons must remain separate.");
                    Set("echoLinksFactory",new Func<EchoLinks>(()=>new EchoLinks(new LinkFixture(File.ReadAllBytes(args[0])))));
                    await (Task)Call("ReadEchoLink","https://repo.example/repo.json")!;
                    Check(C<WrapPanel>("EchoLinkChoices").Children.Count==1,"Repository links must show install choices.");Click(C<WrapPanel>("EchoLinkChoices").Children.OfType<Button>().Single());await Wait(()=>!(bool)Get("echoImporting")!,"Link install did not finish.");
                    Check(new EchoPackages(data).List().Count==1&&C<TextBox>("EchoLinkInput").Text=="","Link install must retain a validated package and clear the input.");
                    using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(data,"echo-addons.png"));}
                    Call("SelectAddonCategory",false);Check(!C<Border>("EchoPackagesPanel").IsEffectivelyVisible,"Video category must hide music packages.");
                    if(args.Length==3)
                    {
                        Check(new Uri(args[2]).Host=="127.0.0.1","Only synthetic loopback accounts may be used.");
                        Set("echoRuntimeFile",Path.GetFullPath(args[1]));Call("SelectAddonCategory",true);
                        await (Task)Call("ActivateEcho",item)!;Check(Get("echoBridge")==null,"Trust must be explicit.");C<CheckBox>("EchoTrust").IsChecked=true;
                        await (Task)Call("ActivateEcho",item)!;Check(Get("echoBridge") is EchoMusicBridge,"Extension did not activate.");
                        var helper=(EchoMusicBridge)Get("echoBridge")!;
                        var form=C<StackPanel>("EchoLoginForms").Children.OfType<StackPanel>().First();var fields=form.Children.OfType<TextBox>().ToArray();
                        Check(fields.Length==3&&fields[2].PasswordChar=='●',"Actual custom login form must mask the password.");
                        fields[0].Text=args[2];fields[1].Text="fixture";fields[2].Text="probe-password";Click(form.Children.OfType<Button>().Single());
                        await Wait(()=>!(bool)Get("echoWorking")!,"Login did not finish.");Check(Get("echoBridge")!=null&&fields.All(x=>string.IsNullOrEmpty(x.Text)),"Login must succeed and clear input controls.");
                        Check(C<ScrollViewer>("MusicPage").IsVisible&&C<StackPanel>("EchoMusicItems").Children.Count>0,"Successful login must automatically open the provider catalogue.");
                        Click(C<StackPanel>("EchoMusicItems").Children.OfType<Button>().First(x=>x.Content?.ToString()?.EndsWith(" · artist")==true));await Wait(()=>!(bool)Get("echoWorking")!,"Artist catalogue did not open.");Check(Get("echoBridge")!=null&&C<StackPanel>("EchoMusicItems").Children.OfType<Button>().Any(),"Artist feed must contain playable/browsable items.");Call("BackEcho_Click",null,new RoutedEventArgs());await Wait(()=>!(bool)Get("echoWorking")!,"Artist back navigation did not finish.");
                        Click(C<StackPanel>("EchoMusicItems").Children.OfType<Button>().First(x=>x.Content?.ToString()?.EndsWith(" · playlist")==true));await Wait(()=>!(bool)Get("echoWorking")!,"Playlist did not open.");Check(Get("echoBridge")!=null&&C<StackPanel>("EchoMusicItems").Children.OfType<Button>().Single().Content?.ToString()?.Contains("Playlist tone")==true,"Playlist tracks must be browsable.");Call("BackEcho_Click",null,new RoutedEventArgs());await Wait(()=>!(bool)Get("echoWorking")!,"Playlist back navigation did not finish.");
                        Call("Page","Music");C<TextBox>("EchoQuery").Text="";Call("SearchEcho_Click",null,new RoutedEventArgs());
                        await Wait(()=>!(bool)Get("echoWorking")!,"Search did not finish.");Check(Get("echoBridge")!=null,"Search failed.");
                        Click(C<WrapPanel>("EchoTabs").Children.OfType<Button>().Single(x=>x.Content?.ToString()=="Tracks"));await Wait(()=>!(bool)Get("echoWorking")!,"Track tab did not load.");
                        Check(C<StackPanel>("EchoMusicItems").Children.Count==50&&C<Button>("EchoMore").IsVisible,"First track page must render with continuation.");Click(C<Button>("EchoMore"));await Wait(()=>!(bool)Get("echoWorking")!,"Continuation did not load.");
                        Check(C<StackPanel>("EchoMusicItems").Children.Count==51,"Continuation must append its track.");
                        Click(C<WrapPanel>("EchoTabs").Children.OfType<Button>().Single(x=>x.Content?.ToString()=="Albums"));await Wait(()=>!(bool)Get("echoWorking")!,"Album tab did not load.");Click(C<StackPanel>("EchoMusicItems").Children.OfType<Button>().Single());await Wait(()=>!(bool)Get("echoWorking")!,"Album did not open.");
                        Check(C<StackPanel>("EchoMusicItems").Children.Count==1,"Album should show its track.");
                        Call("BackEcho_Click",null,new RoutedEventArgs());await Wait(()=>!(bool)Get("echoWorking")!,"Back to results did not finish.");Check(C<WrapPanel>("EchoTabs").Children.Count>0,"Back must restore the result tabs.");Click(C<StackPanel>("EchoMusicItems").Children.OfType<Button>().Single());await Wait(()=>!(bool)Get("echoWorking")!,"Album did not reopen.");
                        using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(data,"echo-music.png"));}
                        Click(C<StackPanel>("EchoMusicItems").Children.OfType<Button>().Single());await Wait(()=>Get("player") is LibVLCSharp.Shared.MediaPlayer p&&p.IsPlaying&&p.Time>1000,"Extension audio did not play in the actual app.");
                        Check(Get("currentHistoryKey")==null&&(bool)Get("suppressCurrentHistory")!,"Authenticated source must not become a history key.");
                        Call("SaveProgress");Check(((LibraryStore)Get("store")!).History().Count==0,"Extension music must stay out of video history.");
                        Call("NavigateBack_Click",null,new RoutedEventArgs());Check(C<ScrollViewer>("MusicPage").IsVisible,"Back must return to Music.");
                        C<TextBox>("EchoQuery").Text="slow";Call("SearchEcho_Click",null,new RoutedEventArgs());await Task.Delay(100);Call("StopEcho");
                        await Wait(()=>helper.IsStopped,"Disconnect must terminate an in-flight helper.");Check(C<StackPanel>("EchoLoginForms").Children.Count==0&&C<StackPanel>("EchoMusicItems").Children.Count==0,"Disconnect must clear session forms and stale handles.");
                        await (Task)Call("ActivateEcho",item)!;Check(Get("echoBridge") is EchoMusicBridge,"Clean reactivation must work after cancellation.");
                    }
                    await (Task)Call("SetOnlineMode",false)!;Check(new EchoPackages(data).List().Single().Sha256==item.Sha256,"Offline toggle must retain imported files.");
                    var row=(Grid)C<StackPanel>("EchoPackageItems").Children.Single();Click(row.Children.OfType<Button>().Single(x=>x.Content?.ToString()=="Remove"));
                    await Task.Delay(300);Check(new EchoPackages(data).List().Count==0&&C<StackPanel>("EchoPackageItems").Children.Count==0,"Remove must update disk and UI.");
                    passed=true;File.WriteAllText(Path.Combine(data,"result.txt"),args.Length==3?"PASS: repository-link install, collapsed Advanced controls, actual extension activation, trust gating, custom session login, paged browsing, album navigation, native playback, history exclusion, cancellation/restart, offline retention and removal.":"PASS: real Add-ons/Music UI, repository-link install, import rendering, category separation, online toggle retention and removal.");
                }
                catch(Exception e){File.WriteAllText(Path.Combine(data,"result.txt"),"FAIL: "+e+"\nMusic status: "+C<TextBlock>("EchoStatus").Text);}
                finally{window.Close();}
            });
        });
        builder.StartWithClassicDesktopLifetime([]);return passed?0:1;
    }
}
