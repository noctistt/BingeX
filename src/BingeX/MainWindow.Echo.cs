using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace BingeX;

public partial class MainWindow
{
    private bool echoImporting;
    private async Task RefreshEchoPackages()
    {
        try
        {
            var packages=await Task.Run(()=>new EchoPackages(store.DataDirectory).List());
            if(localClosing)return;
            EchoPackageItems.Children.Clear();
            EchoPackageMessage.Text=packages.Count==0?"No music extensions imported.":$"{packages.Count} imported · playback setup pending";
            foreach(var package in packages)
            {
                var row=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new Thickness(0,4)};
                var labels=new StackPanel{Spacing=4};
                labels.Children.Add(new TextBlock{Text=package.Name+" · "+package.Version,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
                labels.Children.Add(new TextBlock{Text=(package.Author.Length>0?package.Author+" · ":"")+"Imported · not active",TextWrapping=TextWrapping.Wrap});
                ToolTip.SetTip(labels,"Package fingerprint: "+package.Sha256);row.Children.Add(labels);
                var remove=new Button{Content="Remove",Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(remove,1);
                remove.Click+=async(_,_)=>
                {
                    if(echoImporting)return;
                    try{new EchoPackages(store.DataDirectory).Remove(package.Sha256);await RefreshEchoPackages();}
                    catch{EchoPackageMessage.Text="Could not remove this extension file. Try again.";}
                };
                row.Children.Add(remove);EchoPackageItems.Children.Add(row);
            }
        }
        catch{if(!localClosing)EchoPackageMessage.Text="Could not read imported music extensions.";}
    }
    private async void ImportEcho_Click(object? sender,RoutedEventArgs e)
    {
        if(!onlineEnabled||echoImporting)return;
        var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Import Echo music extension",AllowMultiple=false,FileTypeFilter=[new FilePickerFileType("Echo extension"){Patterns=["*.eapk"]}]});
        if(localClosing||!onlineEnabled||files.FirstOrDefault()?.TryGetLocalPath() is not {} path)return;
        echoImporting=true;EchoImportButton.IsEnabled=false;EchoPackageMessage.Text="Reading extension package…";
        try
        {
            await Task.Run(()=>new EchoPackages(store.DataDirectory).Import(path));
            await RefreshEchoPackages();
        }
        catch{if(!localClosing)EchoPackageMessage.Text="Could not import this file. Choose a valid Echo .eapk package smaller than 32 MB with name, ID, version and entry-point metadata.";}
        finally{echoImporting=false;if(!localClosing)EchoImportButton.IsEnabled=true;}
    }
}
