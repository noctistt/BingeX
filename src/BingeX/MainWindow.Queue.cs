using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
namespace BingeX;
public partial class MainWindow
{
    private readonly LocalPlaybackQueue localQueue=new();
    private CancellationTokenSource queueFolderWork=new();
    private bool queueFolderBusy;
    private void SetupLocalQueue()
    {
        QueueAutoplayCheck.IsChecked=preferences.AutoplayLocalQueue;
        QueueAutoplayCheck.IsCheckedChanged+=(_,_)=>{preferences=preferences with{AutoplayLocalQueue=QueueAutoplayCheck.IsChecked==true};store.SavePreferences(preferences);};
        Closed+=(_,_)=>queueFolderWork.Cancel();
        RenderLocalQueue();
    }
    private static bool QueueMedia(string path)=>LocalLibraryScanner.IsVideo(path) || new[]{".mp3",".flac",".wav",".m4a",".aac",".ogg",".opus",".wma"}.Contains(Path.GetExtension(path).ToLowerInvariant());
    private void QueuePage_Click(object? sender,RoutedEventArgs e){Page("Queue");RenderLocalQueue();}
    private void RenderLocalQueue()
    {
        QueueItems.Children.Clear();
        QueueCount.Text=$"{localQueue.Items.Count} item(s) · Session queue (up to {LocalPlaybackQueue.Limit})";
        QueueEmpty.IsVisible=localQueue.Items.Count==0;
        foreach(var entry in localQueue.Items.Select((path,index)=>(path,index)))
        {
            var row=new Grid{ColumnDefinitions=new("*,Auto,Auto,Auto"),Margin=new Thickness(0,0,0,8)};
            var text=new StackPanel{Spacing=4};text.Children.Add(new TextBlock{Text=$"{entry.index+1}. "+Path.GetFileNameWithoutExtension(entry.path),TextWrapping=TextWrapping.Wrap,FontWeight=FontWeight.SemiBold});
            text.Children.Add(new TextBlock{Text=entry.path,FontSize=11,TextWrapping=TextWrapping.Wrap,Foreground=Brush.Parse("#AAA0BA")});
            var play=new Button{Content=text,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};
            play.Classes.Set("primary",localQueue.CurrentPath==entry.path);play.Click+=(_,_)=>PlayQueueItem(entry.path);row.Children.Add(play);
            Button Action(string label,string tip,int column,Action action,bool enabled=true)
            {
                var button=new Button{Content=label,Margin=new Thickness(6,0,0,0),Padding=new Thickness(12,8),VerticalAlignment=VerticalAlignment.Center,IsEnabled=enabled};
                ToolTip.SetTip(button,tip);Avalonia.Automation.AutomationProperties.SetName(button,tip);Grid.SetColumn(button,column);button.Click+=(_,_)=>action();row.Children.Add(button);return button;
            }
            Action("↑","Move up",1,()=>{localQueue.Move(entry.path,-1);RenderLocalQueue();},entry.index>0);
            Action("↓","Move down",2,()=>{localQueue.Move(entry.path,1);RenderLocalQueue();},entry.index<localQueue.Items.Count-1);
            Action("×","Remove from queue; keep file",3,()=>{localQueue.Remove(entry.path);RenderLocalQueue();});
            QueueItems.Children.Add(row);
        }
        UpdateQueueActions();
    }
    private void UpdateQueueActions()
    {
        var active=currentContext==null && currentPath!=null && localQueue.IsCurrent(currentPath);
        LocalQueueActions.IsVisible=localQueue.Items.Count>0;
        QueueOpenButton.Content=$"Queue ({localQueue.Items.Count})";
        QueuePreviousButton.IsEnabled=active && !NativePlaybackBusy && !playbackStarting && localQueue.CurrentIndex>0;
        QueueNextButton.IsEnabled=active && !NativePlaybackBusy && !playbackStarting && localQueue.CurrentIndex>=0 && localQueue.CurrentIndex<localQueue.Items.Count-1;
    }
    private bool PlayQueueItem(string path)
    {
        if(NativePlaybackBusy || playbackStarting)return false;
        if(!File.Exists(path)){QueueMessage.Text="This queue item is missing. Choose another item or reconnect its drive.";Status(QueueMessage.Text);return false;}
        localQueue.Select(path);Play(path,Path.GetFileNameWithoutExtension(path));RenderLocalQueue();return true;
    }
    private bool MoveQueue(int direction)
    {
        if(currentContext!=null || !localQueue.IsCurrent(currentPath))return false;
        var next=localQueue.Adjacent(direction,File.Exists);
        if(next!=null)return PlayQueueItem(next);
        Status("No available "+(direction>0?"next":"previous")+" queue item. Missing files are skipped.");return false;
    }
    private void QueuePrevious_Click(object? sender,RoutedEventArgs e)=>MoveQueue(-1);
    private void QueueNext_Click(object? sender,RoutedEventArgs e)=>MoveQueue(1);
    private bool ContinueLocalQueue()=>preferences.AutoplayLocalQueue && MoveQueue(1);
    private void AddQueueFiles(IEnumerable<string> paths,bool playFirst)
    {
        var files=paths.Where(x=>File.Exists(x) && QueueMedia(x)).ToArray();
        var added=localQueue.Add(files);QueueMessage.Text=$"Added {added} item(s). Duplicates, unsupported files and items beyond the queue limit are ignored.";
        RenderLocalQueue();if(playFirst && files.FirstOrDefault() is {} first)PlayQueueItem(first);
    }
    private async void AddQueueFiles_Click(object? sender,RoutedEventArgs e)
    {
        var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Add local media to queue",AllowMultiple=true});
        if(!localClosing)AddQueueFiles(files.Select(x=>x.TryGetLocalPath()).OfType<string>(),false);
    }
    private async void AddQueueFolder_Click(object? sender,RoutedEventArgs e)
    {
        if(queueFolderBusy)return;
        var folders=await StorageProvider.OpenFolderPickerAsync(new(){Title="Add videos from a folder",AllowMultiple=false});
        if(folders.FirstOrDefault()?.TryGetLocalPath() is not {} folder || localClosing)return;
        queueFolderWork.Cancel();queueFolderWork.Dispose();queueFolderWork=new();var token=queueFolderWork.Token;
        queueFolderBusy=true;QueueFolderBusy.IsVisible=true;QueueCancelScan.IsVisible=true;
        try
        {
            var result=await Task.Run(()=>LocalLibraryScanner.Scan(folder,token),token);token.ThrowIfCancellationRequested();
            if(localClosing)return;AddQueueFiles(LocalPlaybackQueue.FolderOrder(result.Videos.Select(x=>x.Path)),false);
            if(!result.Complete)QueueMessage.Text+=" Some folders could not be scanned.";
        }
        catch(OperationCanceledException){if(!localClosing)QueueMessage.Text="Folder scan cancelled.";}
        catch{if(!localClosing)QueueMessage.Text="Could not scan that folder. Existing queue items were kept.";}
        finally{queueFolderBusy=false;if(!localClosing){QueueFolderBusy.IsVisible=false;QueueCancelScan.IsVisible=false;}}
    }
    private void CancelQueueScan_Click(object? sender,RoutedEventArgs e)=>queueFolderWork.Cancel();
    private void ClearQueue_Click(object? sender,RoutedEventArgs e){queueFolderWork.Cancel();localQueue.Clear();RenderLocalQueue();QueueMessage.Text="Queue cleared. The current video keeps playing; files are unchanged.";}
}
