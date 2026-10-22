using Avalonia.Interactivity;
namespace BingeX;
public partial class MainWindow
{
    private bool storageBusy;
    private async void RefreshStorage_Click(object? sender,RoutedEventArgs e)=>await UpdateStorage(null);
    private async void ClearTorrentStorage_Click(object? sender,RoutedEventArgs e)=>await UpdateStorage(false);
    private async void ClearThumbnailStorage_Click(object? sender,RoutedEventArgs e)=>await UpdateStorage(true);
    private async Task UpdateStorage(bool? clear)
    {
        if(storageBusy || localClosing)return;
        storageBusy=true; StorageBusy.IsVisible=true;
        RefreshStorageButton.IsEnabled=ClearTorrentStorageButton.IsEnabled=ClearThumbnailStorageButton.IsEnabled=false;
        StorageMessage.Text=clear==null ? "Measuring cached files…" : "Clearing unused cached files…";
        try
        {
            var data=store.DataDirectory;
            var protect=currentPath!=null && currentContext==null ? store.ThumbnailPath(currentPath) : null;
            if(clear==true)thumbnailWork.Cancel();
            var result=await Task.Run(()=>
            {
                var cleanup=clear.HasValue ? StorageCache.Clear(data,clear.Value,protect) : null;
                return (Cleanup:cleanup,Torrents:StorageCache.Measure(data,false),Thumbnails:StorageCache.Measure(data,true));
            });
            if(localClosing)return;
            TorrentStorageUsage.Text=Usage(result.Torrents,"files"); ThumbnailStorageUsage.Text=Usage(result.Thumbnails,"images");
            var partial=result.Torrents.Skipped+result.Thumbnails.Skipped>0;
            StorageMessage.Text=result.Cleanup is {} cleaned
                ? $"Removed {cleaned.Removed} "+(clear==true?"thumbnail(s)":"unused torrent cache(s)")+$". {cleaned.Skipped} active, locked or inaccessible item(s) kept."
                : partial?"Some files could not be measured; totals are partial.":"Cache sizes updated.";
            if(partial && result.Cleanup!=null)StorageMessage.Text+=" Some size totals are partial.";
            if(clear==true){thumbnailAttempts.Clear();RefreshHistory();RefreshLocalLibrary();}
            if(clear.HasValue)Status(StorageMessage.Text);
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException)
        {if(!localClosing)StorageMessage.Text="Cache folders could not be accessed. Existing files were kept where possible. Try Refresh.";}
        finally
        {
            storageBusy=false;
            if(!localClosing){StorageBusy.IsVisible=false;RefreshStorageButton.IsEnabled=ClearTorrentStorageButton.IsEnabled=ClearThumbnailStorageButton.IsEnabled=true;}
        }
        static string Usage(CacheUsage usage,string unit)=>$"{Size(usage.Bytes)} · {usage.Files} {unit}";
        static string Size(long bytes)=>bytes>=1073741824?$"{bytes/1073741824d:0.##} GB":bytes>=1048576?$"{bytes/1048576d:0.##} MB":bytes>=1024?$"{bytes/1024d:0.##} KB":$"{bytes} bytes";
    }
}
