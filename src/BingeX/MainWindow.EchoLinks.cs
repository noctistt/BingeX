using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
namespace BingeX;
public partial class MainWindow
{
    private CancellationTokenSource echoLinkWork=new();
    private Func<EchoLinks> echoLinksFactory=()=>new EchoLinks();
    private async void AddEchoLink_Click(object? sender,RoutedEventArgs e)=>await ReadEchoLink(EchoLinkInput.Text??"");
    private async Task ReadEchoLink(string link)
    {
        if(!onlineEnabled||echoImporting)return;
        echoLinkWork.Cancel();echoLinkWork.Dispose();echoLinkWork=new();var token=echoLinkWork.Token;
        echoImporting=true;EchoLinkButton.IsEnabled=EchoImportButton.IsEnabled=false;EchoLinkChoices.Children.Clear();EchoLinkChoices.IsEnabled=false;EchoPackageMessage.Text="Reading music extension link…";
        try
        {
            using var links=echoLinksFactory();var choices=await links.Read(link,token);token.ThrowIfCancellationRequested();if(localClosing)return;
            EchoLinkChoices.Children.Clear();
            if(choices.Count==1&&new Uri(choices[0].Url).AbsolutePath.EndsWith(".eapk",StringComparison.OrdinalIgnoreCase))
            {
                await links.Install(choices[0],store.DataDirectory,token);token.ThrowIfCancellationRequested();EchoLinkInput.Text="";await RefreshEchoPackages();EchoPackageMessage.Text="Extension installed. Choose Activate to use it.";
            }
            else
            {
                foreach(var choice in choices){var button=new Button{Content="Install "+choice.Name,Margin=new Thickness(0,0,8,8)};button.Click+=async(_,_)=>await InstallEchoChoice(choice);EchoLinkChoices.Children.Add(button);}
                EchoPackageMessage.Text=$"{choices.Count} extensions found. Choose one to install.";
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){if(!localClosing)EchoPackageMessage.Text="Link import cancelled.";}
        catch{if(!localClosing)EchoPackageMessage.Text="Could not read this link. Use an HTTPS Echo repository, GitHub release or direct .eapk link. The host may also be unavailable or rate limited.";}
        finally{echoImporting=false;if(!localClosing){EchoLinkButton.IsEnabled=EchoImportButton.IsEnabled=true;EchoLinkChoices.IsEnabled=true;}}
    }
    private async Task InstallEchoChoice(EchoLinkChoice choice)
    {
        if(!onlineEnabled||echoImporting)return;
        echoLinkWork.Cancel();echoLinkWork.Dispose();echoLinkWork=new();var token=echoLinkWork.Token;echoImporting=true;EchoLinkChoices.IsEnabled=EchoLinkButton.IsEnabled=EchoImportButton.IsEnabled=false;
        EchoPackageMessage.Text="Downloading and checking extension…";
        try
        {
            using var links=echoLinksFactory();await links.Install(choice,store.DataDirectory,token);token.ThrowIfCancellationRequested();if(localClosing)return;
            await RefreshEchoPackages();EchoLinkInput.Text="";EchoPackageMessage.Text="Extension installed. Choose Activate to use it.";
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){if(!localClosing)EchoPackageMessage.Text="Link import cancelled.";}
        catch{if(!localClosing)EchoPackageMessage.Text="Could not install this extension. Check that the release has one compatible .eapk file, or paste its direct download link.";}
        finally{echoImporting=false;if(!localClosing){EchoLinkChoices.IsEnabled=EchoLinkButton.IsEnabled=EchoImportButton.IsEnabled=true;}}
    }
}
