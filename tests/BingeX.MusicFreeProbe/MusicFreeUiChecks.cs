using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BingeX;
using LibVLCSharp.Shared;
internal static class MusicFreeUiChecks
{
    public static int Run()
    {
        var data=Environment.GetEnvironmentVariable("BINGEX_DATA_DIRECTORY");if(data==null||!Path.GetFileName(data).StartsWith("musicfree-ui-test-"))return 2;
        Directory.CreateDirectory(data);var passed=false;
        var builder=(AppBuilder)typeof(MainWindow).Assembly.GetType("BingeX.Program")!.GetMethod("CreateAppBuilder")!.Invoke(null,null)!;
        _=Task.Run(async()=>{await Task.Delay(3000);await Dispatcher.UIThread.InvokeAsync(async()=>{
            var window=(MainWindow)((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            object? Call(string name,params object?[] a)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,a);
            object? Get(string name)=>typeof(MainWindow).GetField(name,flags)!.GetValue(window);
            T C<T>(string name)where T:Control=>window.FindControl<T>(name)!;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);}
            async Task Wait(Func<bool> ready,string message){var end=DateTime.UtcNow.AddSeconds(45);while(!ready()){if(DateTime.UtcNow>end)throw new Exception(message);await Task.Delay(100);}}
            var stage="startup";
            try{
                await Wait(()=>!C<Border>("StartupSplash").IsVisible,"Startup timed out");C<Slider>("VolumeBar").Value=0;
                Check(!C<Border>("MusicFreePanel").IsVisible,"Offline mode must hide online music");
                await (Task)Call("SetOnlineMode",true)!;Call("Page","Add-ons");Call("SelectAddonCategory",true);
                const string link="https://raw.githubusercontent.com/maotoumao/MusicFreePlugins/3fa57cf37c100f32ca96e9199c6b008e79db28c1/dist/audiomack/index.js";
                stage="trust";await (Task)Call("InstallMusicFree",link)!;Check(Get("musicFreeCode")==null,"Import must require trust before execution");
                C<CheckBox>("MusicFreeTrust").IsChecked=true;
                stage="import";await (Task)Call("InstallMusicFree",link)!;Check(Get("musicFreeCode")!=null,"Provider import failed");
                Check(new MusicFreeProviderStore(data).Read()?.Provider.Trusted==true,"Explicit provider trust must be retained");
                stage="explore";Call("Page","Music");
                await Wait(()=>!(bool)Get("musicFreeWorking")!&&C<WrapPanel>("MusicFreeResultsPanel").Children.Count>0,"Automatic Explore did not load");
                Check(C<WrapPanel>("MusicFreeBrowseFilters").IsVisible&&C<ComboBox>("MusicFreeSection").ItemCount>=2,"Provider browse sections missing");
                await Wait(()=>C<WrapPanel>("MusicFreeResultsPanel").Bounds.Height>150,"Explore cards did not lay out");await Wait(()=>C<WrapPanel>("MusicFreeResultsPanel").GetVisualDescendants().OfType<Image>().Any(x=>x.Source!=null),"Explore artwork did not load");
                using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(data,"musicfree-explore.png"));}
                stage="genre";C<ComboBox>("MusicFreeGenre").SelectedIndex=1;await Wait(()=>!(bool)Get("musicFreeWorking")!&&C<WrapPanel>("MusicFreeResultsPanel").Children.Count>0,"Genre change did not load");
                stage="recent";C<ComboBox>("MusicFreeSection").SelectedIndex=1;await Wait(()=>!(bool)Get("musicFreeWorking")!&&C<WrapPanel>("MusicFreeResultsPanel").Children.Count>0,"Recently Added did not load");
                stage="search";C<TextBox>("MusicFreeSearch").Text="Kevin MacLeod";await (Task)Call("SearchMusicFree",false)!;
                Check(C<WrapPanel>("MusicFreeResultsPanel").Children.Count>1,"Live search returned too few cards");
                await Wait(()=>C<WrapPanel>("MusicFreeResultsPanel").Bounds.Height>150,"Track cards did not lay out");await Wait(()=>C<WrapPanel>("MusicFreeResultsPanel").GetVisualDescendants().OfType<Image>().Any(x=>x.Source!=null),"Artwork did not load");
                using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(data,"musicfree-search.png"));}
                stage="play";C<WrapPanel>("MusicFreeResultsPanel").Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Wait(()=>!(bool)Get("musicFreeWorking")!&&((MediaPlayer)Get("player")!).Time>3000,"Music did not start");
                Check(C<Grid>("MusicNowBar").IsVisible,"Persistent music bar missing");var first=Get("currentPath");
                stage="now-playing";Call("OpenMusicNow_Click",null,new RoutedEventArgs());Call("CancelMusicLyrics");
                Check(C<Border>("MusicNowView").IsVisible&&C<Expander>("MusicNowDetails").IsExpanded,"Now Playing view missing");
                Check(C<TextBlock>("MusicNowTrackTitle").Text?.Length>0&&C<TextBlock>("MusicNowTrackArtist").Text?.Length>0,"Track/artist metadata missing");
                await Wait(()=>C<Image>("MusicNowArtwork").Source!=null,"Now Playing artwork missing");
                var lyricFile=Path.Combine(data,"fixture.lrc");File.WriteAllText(lyricFile,"[00:00]Fixture introduction\n[00:10]Fixture middle\n[00:30]Fixture ending");await (Task)Call("LoadMusicLyricsFile",lyricFile,(long)Get("musicLyricEpoch")!)!;
                await Wait(()=>(int)Get("musicLyricIndex")!>=0,"Lyrics did not highlight");await Wait(()=>C<Image>("MusicNowArtwork").Bounds.Height>150&&C<StackPanel>("MusicLyricLines").Bounds.Height>30,"Now Playing details did not lay out");await Task.Delay(250);
                using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height))){image.Render(window);image.Save(Path.Combine(data,"music-now-playing.png"));}
                stage="seek";await (Task)Call("QueuePlayerCommand",(Action<MediaPlayer>)(native=>native.Time=15000))!;await Wait(()=>((MediaPlayer)Get("player")!).Time>15000,"Seek failed");await Wait(()=>(int)Get("musicLyricIndex")!>=1,"Lyrics did not follow seek");C<NumericUpDown>("MusicLyricsOffset").Value=10000;await Wait(()=>(int)Get("musicLyricIndex")! == 0,"Lyrics delay did not apply");C<NumericUpDown>("MusicLyricsOffset").Value=0;C<Expander>("MusicNowDetails").IsExpanded=false;Call("CancelMusicLyrics");
                stage="provider-switch";
                const string alternate="module.exports={platform:'Alternate fixture',version:'1',search:async()=>({data:[{id:'fixture',title:'Alternate result',artist:'Fixture'}],isEnd:true}),getMediaSource:async()=>({url:'https://example.org/fixture.mp4'})};";
                var providers=new MusicFreeProviderStore(data);providers.Save(new MusicFreeProvider("Alternate fixture","1","",Trusted:true),alternate);providers.Select("Audiomack");Call("ReloadMusicFreeProviders");Call("UpdateMusicFreeProvider");
                C<ComboBox>("MusicFreeProviderPicker").SelectedItem="Alternate fixture";
                await Wait(()=>!(bool)Get("musicFreeWorking")!&&((MusicFreeProvider)Get("musicFreeProvider")!).Name=="Alternate fixture","Provider selection did not settle");
                Check(C<WrapPanel>("MusicFreeResultsPanel").Children.Count==0&&!C<WrapPanel>("MusicFreeBrowseFilters").IsVisible&&!C<Button>("MusicFreeExploreButton").IsVisible,"Old provider results or genres leaked");
                Check(Equals(first,Get("currentPath")),"Provider switch must preserve playback");
                C<TextBox>("MusicFreeSearch").Text="fixture";await (Task)Call("SearchMusicFree",false)!;Check(C<WrapPanel>("MusicFreeResultsPanel").Children.Count==1,"Search-only provider must remain usable");
                stage="next";Check((bool)Call("MoveMusicFree",1)!,"Next action rejected");await Wait(()=>!(bool)Get("musicFreeWorking")!&&!Equals(first,Get("currentPath"))&&((MediaPlayer)Get("player")!).Time>3000,"Next track failed");
                Check(C<NumericUpDown>("MusicLyricsOffset").Value==0&&Get("musicLyrics")==null,"Old lyrics leaked into next track");C<Expander>("MusicNowDetails").IsExpanded=false;Call("CancelMusicLyrics");
                stage="restore-provider";C<ComboBox>("MusicFreeProviderPicker").SelectedItem="Audiomack";await Wait(()=>!(bool)Get("musicFreeWorking")!&&C<WrapPanel>("MusicFreeBrowseFilters").IsVisible,"Original provider did not restore");
                Check(providers.Read()?.Provider.Name=="Audiomack"&&providers.ReadAll().Count==2,"Selected provider and installed providers must persist");
                stage="history";var store=(LibraryStore)Get("store")!;Check(store.History().Count==0,"Online music must not enter video history");
                stage="navigation";Call("Page","Library");await Task.Delay(500);Check(C<Grid>("MusicNowBar").IsVisible,"Music controls must survive navigation");
                stage="offline";await (Task)Call("SetOnlineMode",false)!;Check(!C<Border>("MusicFreePanel").IsVisible&&new MusicFreeProviderStore(data).Read()!=null,"Offline must hide music and retain provider");
                stage="cancel";await (Task)Call("SetOnlineMode",true)!;Call("Page","Music");var pending=(Task)Call("SearchMusicFree",false)!;await (Task)Call("SetOnlineMode",false)!;await pending;Check(!(bool)Get("musicFreeWorking")!,"Cancellation must settle state");
                passed=true;File.WriteAllText(Path.Combine(data,"result.txt"),"PASS: automatic Trending Explore, genre changes, Recently Added, live search/artwork, provider switching with cleared results, search-only fallback, preserved playback and original-provider next track, persisted selection and multiple providers, native playback/seeking, Now Playing artwork/artist metadata, independent lyrics highlight/seek/offset and next-track reset, history exclusion, navigation, offline retention and cancellation.");
            }catch(Exception e){File.WriteAllText(Path.Combine(data,"result.txt"),"FAIL stage "+stage+": "+e.GetType().Name+" / "+e.Message+"\nInstall: "+C<TextBlock>("MusicFreeInstallMessage").Text+"\nMusic: "+C<TextBlock>("MusicFreeMessage").Text);}
            finally{window.Close();}
        });});
        builder.StartWithClassicDesktopLifetime([]);return passed?0:1;
    }
}
