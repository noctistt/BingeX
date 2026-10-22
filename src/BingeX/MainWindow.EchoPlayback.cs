using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
namespace BingeX;

public partial class MainWindow
{
    private EchoMusicBridge? echoBridge;
    private EchoPackage? echoPackage;
    private CancellationTokenSource echoWork=new();
    private string? echoRuntimeFile;
    private EchoFeed? echoFeed;
    private readonly Stack<(EchoFeed Feed,string Tab)> echoNavigation=new();
    private string echoTab="",echoNext="";
    private bool echoWorking;
    private int echoEpoch;
    private void SetupEcho()
    {
        try{var file=Path.Combine(store.DataDirectory,"echo","runtime-location.txt");if(File.Exists(file))echoRuntimeFile=File.ReadAllText(file);if(echoRuntimeFile!=null){EchoRuntimeMessage.Text="Music runtime selected. Activate a trusted extension to start.";EchoSetupSummary.Text="Music engine selected. Activate an installed provider to load its catalogue.";EchoStatus.Text="No music provider active. Activate an installed provider in Add-ons → Music to see its catalogue.";}}catch{}
        Closed+=(_,_)=>StopEcho();
    }
    private void StopEcho()
    {
        echoLinkWork.Cancel();
        echoEpoch++;echoWork.Cancel();echoWork.Dispose();echoWork=new();
        if(echoPackage is {} previous)SetEchoPackageState(previous,"Installed · disconnected");
        var old=echoBridge;echoBridge=null;echoPackage=null;echoWorking=false;
        if(old!=null)_=old.DisposeAsync().AsTask();
        ClearEchoBrowser();EchoLoginForms.Children.Clear();EchoUsers.Children.Clear();EchoMusicTitle.Text="Online music catalogue";EchoSearchControls.IsVisible=false;EchoMusicPanel.IsVisible=onlineEnabled;EchoBusy.IsVisible=false;EchoPackageItems.IsEnabled=EchoMusicItems.IsEnabled=EchoTabs.IsEnabled=EchoLoginForms.IsEnabled=EchoUsers.IsEnabled=true;EchoStatus.Text=echoRuntimeFile==null?"Music engine setup is missing. Open Manage extension / login to finish setup before browsing.":"No music provider active. Activate an installed provider in Add-ons → Music to see its catalogue.";
    }
    private void ClearEchoBrowser(bool resetNavigation=true){echoFeed=null;echoTab=echoNext="";EchoTabs.Children.Clear();EchoMusicItems.Children.Clear();EchoMore.IsVisible=false;if(resetNavigation)echoNavigation.Clear();EchoBack.IsVisible=echoNavigation.Count>0;}
    private void DisconnectEcho_Click(object? sender,RoutedEventArgs e){StopEcho();_=RefreshEchoPackages();}
    private void ManageEcho_Click(object? sender,RoutedEventArgs e){if(!onlineEnabled)return;Page("Add-ons");SelectAddonCategory(true);}
    private void OpenEchoMusic_Click(object? sender,RoutedEventArgs e){Page("Music");if(EchoMusicPanel.IsVisible)Avalonia.Threading.Dispatcher.UIThread.Post(()=>EchoMusicPanel.BringIntoView());}
    private async void ChooseEchoRuntime_Click(object? sender,RoutedEventArgs e)
    {
        if(!onlineEnabled||echoWorking)return;
        var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Choose prepared Echo music runtime",AllowMultiple=false,FileTypeFilter=[new FilePickerFileType("Music runtime profile"){Patterns=["*.json"]}]});
        if(localClosing||!onlineEnabled||files.FirstOrDefault()?.TryGetLocalPath() is not {} file)return;
        try
        {
            await Task.Run(()=>EchoRuntimeProfile.Load(file,store.DataDirectory));if(localClosing||!onlineEnabled)return;
            StopEcho();echoRuntimeFile=file;EchoTrust.IsChecked=false;
            var location=Path.Combine(store.DataDirectory,"echo","runtime-location.txt");Directory.CreateDirectory(Path.GetDirectoryName(location)!);File.WriteAllText(location,file);
            EchoRuntimeMessage.Text="Runtime verified. Activate a trusted music extension to start.";
            EchoSetupSummary.Text="Music engine verified. Activate an installed provider to load its catalogue.";
            EchoStatus.Text="Activate an installed provider to load its catalogue.";
        }
        catch{EchoRuntimeMessage.Text="Could not verify this runtime. Choose a prepared echo-runtime.json with its original dependency files.";}
    }
    private async Task ActivateEcho(EchoPackage package)
    {
        if(!onlineEnabled||echoWorking)return;
        if(echoRuntimeFile==null){EchoAdvanced.IsExpanded=true;EchoStatus.Text="Music engine setup is required once before playback. Open Advanced to finish setup.";return;}
        if(EchoTrust.IsChecked!=true){EchoStatus.Text="Confirm that you trust the runtime and extension before activation.";return;}
        StopEcho();SetEchoPackageState(package,"Starting…");var epoch=echoEpoch;var token=echoWork.Token;echoWorking=true;EchoBusy.IsVisible=true;EchoPackageItems.IsEnabled=false;EchoStatus.Text="Starting "+package.Name+"…";
        await RefreshEchoPackages();
        EchoMusicBridge? candidate=null;
        try
        {
            var profile=await Task.Run(()=>EchoRuntimeProfile.Load(echoRuntimeFile,store.DataDirectory),token);token.ThrowIfCancellationRequested();
            candidate=new EchoMusicBridge(profile.Runtime);echoBridge=candidate;
            var path=Path.Combine(store.DataDirectory,"echo","packages",package.Sha256+".eapk");
            await candidate.InitializeAsync(new(path,package.Sha256,package.EntryClass,profile.SdkHash),token);
            token.ThrowIfCancellationRequested();if(localClosing||epoch!=echoEpoch)return;
            echoPackage=package;EchoMusicTitle.Text=package.Name+" · catalogue";EchoMusicPanel.IsVisible=true;EchoSearchControls.IsVisible=true;
            EchoSearchButton.IsEnabled=candidate.Capabilities.Contains("SearchFeedClient");EchoHomeButton.IsEnabled=candidate.Capabilities.Contains("HomeFeedClient");
            if(candidate.Capabilities.Contains("CustomInput")){var forms=await candidate.CallAsync("loginForms",new{},token);token.ThrowIfCancellationRequested();RenderEchoForms(forms);}
            token.ThrowIfCancellationRequested();
            SetEchoPackageState(package,candidate.Capabilities.Contains("CustomInput")?"Login required · session only":"Ready · playback not yet checked");
            if(candidate.Capabilities.Contains("CustomInput"))EchoStatus.Text="Provider active. Sign in below to load its music catalogue, then choose Open Music.";
            else await LoadEchoCatalogue(candidate,token);
            await RefreshEchoPackages();
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(EchoCompatibilityException error){if(epoch==echoEpoch){StopEcho();RecordEchoFailure(package,error);await RefreshEchoPackages();EchoStatus.Text=package.Name+": "+error.Message+" ("+error.Stage+" / "+error.FailureType+")";}}
        catch(Exception error){if(epoch==echoEpoch){StopEcho();RecordEchoFailure(package,error);await RefreshEchoPackages();EchoStatus.Text=error is OperationCanceledException?"Activation timed out. The helper was stopped; activate to retry.":"Could not activate this extension. Check runtime compatibility and try again.";}}
        finally
        {
            if(epoch!=echoEpoch&&candidate!=null)await candidate.DisposeAsync();
            if(epoch==echoEpoch){echoWorking=false;EchoBusy.IsVisible=false;EchoPackageItems.IsEnabled=true;}
        }
    }
    private async Task EchoRequest(Func<EchoMusicBridge,CancellationToken,Task> action,string message)
    {
        if(!onlineEnabled||echoWorking||echoBridge==null)return;
        var owner=echoBridge;var requestPackage=echoPackage;var epoch=echoEpoch;var token=echoWork.Token;echoWorking=true;EchoBusy.IsVisible=true;EchoMusicItems.IsEnabled=EchoTabs.IsEnabled=EchoLoginForms.IsEnabled=EchoUsers.IsEnabled=false;EchoStatus.Text=message;
        try{await action(owner,token);token.ThrowIfCancellationRequested();}
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(EchoCompatibilityException error){if(epoch==echoEpoch){StopEcho();if(requestPackage!=null)RecordEchoFailure(requestPackage,error);await RefreshEchoPackages();EchoStatus.Text=error.Message+" ("+error.Stage+" / "+error.FailureType+")";}}
        catch(Exception error){if(epoch==echoEpoch){StopEcho();if(requestPackage!=null)RecordEchoFailure(requestPackage,error);await RefreshEchoPackages();EchoStatus.Text=error is OperationCanceledException?"The provider took too long to respond. The helper was stopped; activate to retry.":"This request failed. Activate the extension again to reconnect.";}}
        finally{if(epoch==echoEpoch){echoWorking=false;EchoBusy.IsVisible=false;EchoMusicItems.IsEnabled=EchoTabs.IsEnabled=EchoLoginForms.IsEnabled=EchoUsers.IsEnabled=true;}}
    }
    private void RenderEchoForms(JsonElement forms)
    {
        EchoLoginForms.Children.Clear();
        foreach(var form in forms.EnumerateArray())
        {
            var fields=new List<(string Key,TextBox Box,bool Required)>();var panel=new StackPanel{Spacing=8};
            panel.Children.Add(new TextBlock{Text=form.GetProperty("label").GetString(),FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
            foreach(var field in form.GetProperty("fields").EnumerateArray())
            {
                string key=field.GetProperty("key").GetString()!,label=field.GetProperty("label").GetString()??key,type=field.GetProperty("type").GetString()??"";
                var box=new TextBox{Watermark=label,MaxLength=16384};if(type.Contains("Password",StringComparison.OrdinalIgnoreCase)||key.Contains("token",StringComparison.OrdinalIgnoreCase)||key.Contains("password",StringComparison.OrdinalIgnoreCase))box.PasswordChar='●';
                Avalonia.Automation.AutomationProperties.SetName(box,label);panel.Children.Add(box);fields.Add((key,box,field.GetProperty("required").GetBoolean()));
            }
            var login=new Button{Content="Sign in for this session"};var keyForm=form.GetProperty("key").GetString()!;
            login.Click+=async(_,_)=>
            {
                if(echoWorking||echoBridge==null)return;
                if(fields.Any(x=>x.Required&&string.IsNullOrWhiteSpace(x.Box.Text))){EchoStatus.Text="Fill in the required login fields.";return;}
                var values=fields.ToDictionary(x=>x.Key,x=>x.Box.Text??"");foreach(var f in fields)f.Box.Text="";
                await EchoRequest(async(bridge,token)=>
                {
                    ClearEchoBrowser();EchoUsers.Children.Clear();var users=await bridge.CallAsync("login",new{key=keyForm,data=values},token);values.Clear();token.ThrowIfCancellationRequested();
                    if(users.GetArrayLength()==1){await bridge.CallAsync("selectUser",new{handle=users[0].GetProperty("handle").GetString()},token);await LoadEchoCatalogue(bridge,token);}
                    else{foreach(var user in users.EnumerateArray()){var handle=user.GetProperty("handle").GetString();var choose=new Button{Content=user.GetProperty("name").GetString(),Margin=new Thickness(0,0,8,0)};choose.Click+=async(_,_)=>await EchoRequest(async(b,t)=>{ClearEchoBrowser();await b.CallAsync("selectUser",new{handle},t);EchoUsers.Children.Clear();await LoadEchoCatalogue(b,t);},"Selecting account…");EchoUsers.Children.Add(choose);}EchoStatus.Text=users.GetArrayLength()==0?"No accounts returned. Activate again to retry.":"Choose an account.";}
                },"Signing in…");values.Clear();
            };
            panel.Children.Add(login);EchoLoginForms.Children.Add(panel);
        }
    }
    private async Task OpenEchoFeed(EchoMusicBridge bridge,EchoFeed feed,CancellationToken token,bool nested=false,string? selectedTab=null)
    {
        token.ThrowIfCancellationRequested();
        ClearEchoBrowser(!nested);echoFeed=feed;
        foreach(var tab in feed.Tabs){var button=new Button{Content=tab.Title,Margin=new Thickness(0,0,8,4)};button.Click+=async(_,_)=>await EchoRequest(async(b,t)=>await EchoPage(b,tab.Id,false,t),"Loading music…");EchoTabs.Children.Add(button);}
        if(feed.Feed.Length>0)await EchoPage(bridge,selectedTab??feed.Tabs.FirstOrDefault()?.Id??"",false,token);else EchoStatus.Text="No music returned.";
    }
    private async Task LoadEchoCatalogue(EchoMusicBridge bridge,CancellationToken token)
    {
        if(echoPackage!=null){SetEchoPackageState(echoPackage,"Ready · playback not yet checked");await RefreshEchoPackages();}
        EchoStatus.Text="Loading the provider's music catalogue…";
        if(bridge.Capabilities.Contains("HomeFeedClient")){var raw=await bridge.CallAsync("home",new{},token);await OpenEchoFeed(bridge,raw.Deserialize<EchoFeed>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!,token);}
        else if(bridge.Capabilities.Contains("SearchFeedClient"))await OpenEchoFeed(bridge,await bridge.SearchAsync("",token),token);
        else EchoStatus.Text="This provider does not offer a catalogue or search supported by BingeX yet.";
        token.ThrowIfCancellationRequested();if(bridge.Capabilities.Contains("SearchFeedClient")||bridge.Capabilities.Contains("HomeFeedClient"))OpenEchoMusic_Click(null,new RoutedEventArgs());
    }
    private async Task EchoPage(EchoMusicBridge bridge,string tab,bool more,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(echoFeed==null)return;
        if(!more){EchoMusicItems.Children.Clear();echoNext="";EchoMore.IsVisible=false;}echoTab=tab;
        var page=await bridge.PageAsync(echoFeed.Feed,tab,more?echoNext:null,token);token.ThrowIfCancellationRequested();
        foreach(var item in page.Items)RenderEchoItem(item,0);
        echoNext=page.Next;EchoMore.IsVisible=page.Next.Length>0;EchoStatus.Text=page.Items.Count==0?"The provider returned no catalogue items here. Try searching by song, album or artist.":"Catalogue loaded. Open Music to choose a track or album.";
    }
    private void RenderEchoItem(EchoItem item,int depth)
    {
        if(depth>4||EchoMusicItems.Children.Count>=1000)return;
        if(item.Kind=="group"){var heading=new TextBlock{Text=item.Title,FontSize=18,Margin=new Thickness(0,18,0,10),FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap};heading.Bind(WidthProperty,new Avalonia.Data.Binding("Bounds.Width"){Source=EchoMusicItems});EchoMusicItems.Children.Add(heading);foreach(var child in item.Items??[])RenderEchoItem(child,depth+1);return;}
        var button=new Button{Content=item.Title+" · "+item.Kind,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch,HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
        button.IsEnabled=item.Kind is "album" or "track" or "category" or "artist" or "playlist";
        var track=item.Kind=="track";
        button.Margin=new Thickness(0,0,10,8);button.Padding=new Thickness(12);button.MinHeight=48;
        if(track)button.Bind(WidthProperty,new Avalonia.Data.Binding("Bounds.Width"){Source=EchoMusicItems});else button.Width=184;
        button.ContentTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<string>((_,_)=>
        {
            if(track)
            {
                var row=new Grid{ColumnDefinitions=new("32,*")};
                row.Children.Add(new TextBlock{Text="▶",FontSize=18,Foreground=Brush.Parse("#C4A5FF"),VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center});
                var label=new TextBlock{Text=item.Title,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeight.SemiBold,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};Grid.SetColumn(label,1);row.Children.Add(label);return row;
            }
            var panel=new StackPanel{Spacing=8};
            panel.Children.Add(new TextBlock{Text=track?"▶":item.Kind=="artist"?"♬":"♫",FontSize=track?18:36,Foreground=Brush.Parse("#C4A5FF")});
            var title=new TextBlock{Text=item.Title,TextWrapping=TextWrapping.Wrap,MaxWidth=track?560:152,FontWeight=FontWeight.SemiBold};panel.Children.Add(title);
            if(!track)panel.Children.Add(new TextBlock{Text=item.Kind,Foreground=Brush.Parse("#AAA0BC"),FontSize=12});
            return panel;
        });
        Avalonia.Automation.AutomationProperties.SetName(button,(track?"Play ":"Open ")+item.Title);
        ToolTip.SetTip(button,item.Title);
        button.Click+=async(_,_)=>await EchoRequest(async(bridge,token)=>
        {
            if(item.Kind=="album"){var raw=await bridge.CallAsync("album",new{handle=item.Handle},token);token.ThrowIfCancellationRequested();if(echoFeed!=null)echoNavigation.Push((echoFeed,echoTab));await OpenEchoFeed(bridge,raw.Deserialize<EchoFeed>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!,token,true);}
            else if(item.Kind is "artist" or "playlist"){var raw=await bridge.CallAsync(item.Kind,new{handle=item.Handle},token);token.ThrowIfCancellationRequested();if(echoFeed!=null)echoNavigation.Push((echoFeed,echoTab));await OpenEchoFeed(bridge,raw.Deserialize<EchoFeed>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!,token,true);}
            else if(item.Kind=="category"&&item.Feed is {Length:>0}){if(echoFeed!=null)echoNavigation.Push((echoFeed,echoTab));await OpenEchoFeed(bridge,new(item.Feed,[]),token,true);}
            else if(item.Kind=="track")
            {
                if(NativePlaybackBusy){EchoStatus.Text="Wait for the current player change, then try again.";return;}
                var variants=await bridge.VariantsAsync(item.Handle!,token);var variant=variants.OrderByDescending(x=>x.Quality).FirstOrDefault();
                if(variant==null){EchoStatus.Text="No supported audio version was returned.";return;}
                var sources=await bridge.ResolveAsync(variant.Handle,token);token.ThrowIfCancellationRequested();var source=sources.FirstOrDefault();
                if(source==null){EchoStatus.Text="No playable audio source was returned.";return;}
                var headers=new Dictionary<string,string>(source.Headers,StringComparer.OrdinalIgnoreCase);if(headers.Count==0)headers["Accept"]="audio/*";
                Play(source.Url,item.Title,requestHeaders:headers,skipHistory:true,musicPlayback:true);EchoStatus.Text="Playing "+item.Title;
            }
        },"Loading "+item.Title+"…");
        EchoMusicItems.Children.Add(button);
    }
    private async void SearchEcho_Click(object? sender,RoutedEventArgs e)=>await EchoRequest(async(b,t)=>await OpenEchoFeed(b,await b.SearchAsync(EchoQuery.Text??"",t),t),"Searching music…");
    private async void HomeEcho_Click(object? sender,RoutedEventArgs e)=>await EchoRequest(async(b,t)=>{var raw=await b.CallAsync("home",new{},t);await OpenEchoFeed(b,raw.Deserialize<EchoFeed>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!,t);},"Loading music home…");
    private async void MoreEcho_Click(object? sender,RoutedEventArgs e)=>await EchoRequest(async(b,t)=>await EchoPage(b,echoTab,true,t),"Loading more music…");
    private async void BackEcho_Click(object? sender,RoutedEventArgs e)=>await EchoRequest(async(b,t)=>{if(echoNavigation.TryPop(out var previous))await OpenEchoFeed(b,previous.Feed,t,true,previous.Tab);},"Loading previous music results…");
}
